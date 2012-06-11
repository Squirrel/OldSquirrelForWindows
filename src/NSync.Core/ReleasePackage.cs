using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Ionic.Zip;
using MarkdownSharp;
using NuGet;

namespace NSync.Core
{
    public class ReleasePackage : IEnableLogger
    {
        public ReleasePackage(string inputPackageFile)
        {
            InputPackageFile = inputPackageFile;
        }

        public string InputPackageFile { get; protected set; }
        public string ReleasePackageFile { get; protected set; }

        public string CreateReleasePackage(string outputFile, string packagesRootDir = null)
        {
            Contract.Requires(!String.IsNullOrEmpty(outputFile));

            if (ReleasePackageFile != null) {
                return ReleasePackageFile;
            }

            var package = new ZipPackage(InputPackageFile);
            var dependencies = findAllDependentPackages(package, packagesRootDir);

            var tempPath = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            tempPath.Create();

            try {
                using(var zf = new ZipFile(InputPackageFile)) {
                    zf.ExtractAll(tempPath.FullName);
                }
    
                dependencies.ForEach(pkg => {
                    this.Log().Info("Scanning {0}", pkg.Id);

                    pkg.GetFiles()
                        .Where(x => x.Path.StartsWith("lib", true, CultureInfo.InvariantCulture))
                        .ForEach(file => {
                            var outPath = new FileInfo(Path.Combine(tempPath.FullName, file.Path));

                            outPath.Directory.CreateRecursive();

                            using (var of = File.Create(outPath.FullName)) {
                                this.Log().Info("Writing {0} to {1}", file.Path, outPath);
                                file.GetStream().CopyTo(of);
                            }
                        });
                });

                var specPath = tempPath.GetFiles("*.nuspec").First().FullName;
                removeDependenciesFromPackageSpec(specPath);
                removeSilverlightAssemblies(tempPath);
                removeDeveloperDocumentation(tempPath);
                renderReleaseNotesMarkdown(specPath);

                using (var zf = new ZipFile(outputFile)) {
                    zf.AddDirectory(tempPath.FullName);
                    zf.Save();
                }

                ReleasePackageFile = outputFile;
                return ReleasePackageFile;
            } finally {
                tempPath.Delete(true);
            }
        }

        public ReleasePackage CreateDeltaPackage(ReleasePackage baseFixture, string outputFile)
        {
            Contract.Requires(baseFixture != null);
            Contract.Requires(!String.IsNullOrEmpty(outputFile) && !File.Exists(outputFile));

            var baseTempPath = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            baseTempPath.Create();

            var tempPath = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            tempPath.Create();

            try {
                using (var zf = new ZipFile(baseFixture.ReleasePackageFile)) {
                    zf.ExtractAll(baseTempPath.FullName);
                }
                
                using (var zf = new ZipFile(ReleasePackageFile)) {
                    zf.ExtractAll(tempPath.FullName);
                }

                var baseLibFiles = baseTempPath.GetAllFilesRecursively()
                    .Where(x => x.FullName.ToLowerInvariant().Contains("lib" + Path.DirectorySeparatorChar))
                    .ToDictionary(k => k.FullName.Replace(baseTempPath.FullName, ""), v => v.FullName);

                var libDir = tempPath.GetDirectories().First(x => x.Name.ToLowerInvariant() == "lib");
                libDir.GetAllFilesRecursively().ForEach(libFile => {
                    var relativePath = libFile.FullName.Replace(tempPath.FullName, "");

                    if (!baseLibFiles.ContainsKey(relativePath)) {
                        this.Log().Info("{0} not found in base package, marking as new", relativePath);
                        return;
                    }

                    var oldData = File.ReadAllBytes(baseLibFiles[relativePath]);
                    var newData = File.ReadAllBytes(libFile.FullName);

                    if (bytesAreIdentical(oldData, newData)) {
                        this.Log().Info("{0} hasn't changed, writing dummy file", relativePath);
                        File.Create(libFile.FullName + ".diff").Dispose();
                        libFile.Delete();
                        return;
                    }

                    this.Log().Info("Delta patching {0} => {1}", baseLibFiles[relativePath], libFile.FullName);
                    using (var of = File.Create(libFile.FullName + ".diff")) {
                        BinaryPatchUtility.Create(oldData, newData, of);
                        libFile.Delete();
                    }
                });

                using (var zf = new ZipFile(outputFile)) {
                    zf.AddDirectory(tempPath.FullName);
                    zf.Save();
                }
            } finally {
                baseTempPath.Delete(true);
                tempPath.Delete(true);
            }

            return new ReleasePackage(outputFile);
        }

        public ReleasePackage ApplyDeltaPackage(ReleasePackage deltaPackage, string outputFile)
        {
            Contract.Requires(deltaPackage != null);
            Contract.Requires(!String.IsNullOrEmpty(outputFile) && !File.Exists(outputFile));

            string workingPath;
            string deltaPath;

            using (Utility.WithTempDirectory(out deltaPath))
            using (Utility.WithTempDirectory(out workingPath))
            using (var deltaZip = new ZipFile(deltaPackage.InputPackageFile))
            using (var baseZip = new ZipFile(InputPackageFile)) {
                deltaZip.ExtractAll(deltaPath);
                baseZip.ExtractAll(workingPath);

                var pathsVisited = new List<string>();
                new DirectoryInfo(deltaPath).GetAllFilesRecursively()
                    .Select(x => x.FullName.Replace(deltaPath + Path.DirectorySeparatorChar, ""))
                    .Where(x => x.StartsWith("lib", StringComparison.InvariantCultureIgnoreCase))
                    .ForEach(file => {
                        var inputFile = Path.Combine(deltaPath, file);
                        var finalTarget = Path.Combine(workingPath, Regex.Replace(file, @".diff$", ""));
                        var outPath = Path.GetTempFileName();

                        pathsVisited.Add(Regex.Replace(file, @".diff$", "").ToLowerInvariant());

                        if (new FileInfo(inputFile).Length == 0) {
                            this.Log().Info("{0} exists unchanged, skipping", file);
                            return;
                        }

                        if (file.EndsWith(".diff")) {
                            using (var of = File.OpenWrite(outPath))
                            using (var inf = File.OpenRead(finalTarget)) {
                                this.Log().Info("Applying Diff to {0}", file);
                                BinaryPatchUtility.Apply(inf, () => File.OpenRead(inputFile), of);
                            }
                        } else {
                            using (var of = File.OpenWrite(outPath))
                            using (var inf = File.OpenRead(inputFile)) {
                                this.Log().Info("Adding new file: {0}", file);
                                inf.CopyTo(of);
                            }
                        }

                        File.Delete(finalTarget);
                        File.Move(outPath, finalTarget);
                    });

                new DirectoryInfo(workingPath).GetAllFilesRecursively()
                    .Select(x => x.FullName.Replace(workingPath + Path.DirectorySeparatorChar, "").ToLowerInvariant())
                    .Where(x => x.StartsWith("lib", StringComparison.InvariantCultureIgnoreCase) && !pathsVisited.Contains(x))
                    .ForEach(x => { 
                        this.Log().Info("{0} was in old package but not in new one, deleting", x);
                        File.Delete(Path.Combine(workingPath, x));
                    });

                new DirectoryInfo(deltaPath).GetAllFilesRecursively()
                    .Select(x => x.FullName.Replace(deltaPath + Path.DirectorySeparatorChar, ""))
                    .Where(x => !x.StartsWith("lib", StringComparison.InvariantCultureIgnoreCase))
                    .ForEach(x => {
                        this.Log().Info("Updating metadata file: {0}", x);
                        File.Copy(Path.Combine(deltaPath, x), Path.Combine(workingPath, x), true);
                    });

                using (var zf = new ZipFile(outputFile)) {
                    zf.AddDirectory(workingPath);
                    zf.Save();
                }
            }

            return new ReleasePackage(outputFile);
        }

        void removeDeveloperDocumentation(DirectoryInfo expandedRepoPath)
        {
            expandedRepoPath.GetAllFilesRecursively()
                .Where(x => x.Name.EndsWith(".dll", true, CultureInfo.InvariantCulture))
                .Select(x => new FileInfo(x.FullName.ToLowerInvariant().Replace(".dll", ".xml")))
                .Where(x => x.Exists)
                .ForEach(x => x.Delete());
        }

        void removeSilverlightAssemblies(DirectoryInfo expandedRepoPath)
        {
            // NB: Nuke Silverlight. We can't tell as easily if other
            // profiles can be removed because you can load net20 DLLs
            // inside .NET 4.0 apps
            var libPath = expandedRepoPath.GetDirectories().First(x => x.Name.ToLowerInvariant() == "lib");
            this.Log().Debug(libPath.FullName);
            libPath.GetDirectories().Where(x => x.Name.ToLowerInvariant().StartsWith("sl")).Do(
                x => this.Log().Info("Deleting {0}", x.Name)).ForEach(x => x.Delete(true));
        }

        void renderReleaseNotesMarkdown(string specPath)
        {
            var doc = new XmlDocument();
            doc.Load(specPath);

            // XXX: This code looks full tart
            var metadata = doc.DocumentElement.ChildNodes.OfType<XmlElement>().First(x => x.Name.ToLowerInvariant() == "metadata");
            var releaseNotes = metadata.ChildNodes.OfType<XmlElement>().FirstOrDefault(x => x.Name.ToLowerInvariant() == "releasenotes");

            if (releaseNotes == null) {
                this.Log().Info("No release notes found in {0}", specPath);
                return;
            }

            var md = new Markdown();
            releaseNotes.InnerText = String.Format("<![CDATA[\n" + "{0}\n" + "]]>", md.Transform(releaseNotes.InnerText));

            doc.Save(specPath);
        }

        void removeDependenciesFromPackageSpec(string specPath)
        {
            var xdoc = new XmlDocument();
            xdoc.Load(specPath);

            var metadata = xdoc.DocumentElement.FirstChild;
            metadata.RemoveChild(
                metadata.ChildNodes.OfType<XmlElement>().First(x => x.Name.ToLowerInvariant() == "dependencies"));

            xdoc.Save(specPath);
        }

        IEnumerable<IPackage> findAllDependentPackages(IPackage package = null, string packagesRootDir = null)
        {
            package = package ?? new ZipPackage(InputPackageFile);

            return package.Dependencies.SelectMany(x => {
                var ret = findPackageFromName(x.Id, x.VersionSpec, packagesRootDir);
                if (ret == null) {
                    return Enumerable.Empty<IPackage>();
                }

                return findAllDependentPackages(ret).StartWith(ret);
            });
        }

        IPackage findPackageFromName(string id, IVersionSpec versionSpec, string packagesRootDir = null, IQueryable<IPackage> machineCache = null)
        {
            var localPackages = Enumerable.Empty<IPackage>().AsQueryable();
            machineCache = machineCache ?? Enumerable.Empty<IPackage>().AsQueryable();

            if (packagesRootDir != null) {
                localPackages = new DirectoryInfo(packagesRootDir).GetAllFilesRecursively()
                    .Where(x => x.Name.ToLowerInvariant().EndsWith("nupkg"))
                    .Select(x => new ZipPackage(x.FullName))
                    .ToArray().AsQueryable();
            }

            return findPackageFromNameInList(id, versionSpec, localPackages) ?? findPackageFromNameInList(id, versionSpec, machineCache);
        }

        IPackage findPackageFromNameInList(string id, IVersionSpec versionSpec, IQueryable<IPackage> packageList)
        {
            return packageList.Where(x => x.Id == id).ToArray().FirstOrDefault(x => {
                if (((versionSpec != null) && (versionSpec.MinVersion != null)) && (versionSpec.MaxVersion != null)) {
                    if ((!versionSpec.IsMaxInclusive || !versionSpec.IsMinInclusive) && (versionSpec.MaxVersion == versionSpec.MinVersion)) {
                        return false;
                    }

                    if (versionSpec.MaxVersion < versionSpec.MinVersion) {
                        return false;
                    }
                }

                return true;
            });
        }

        bool bytesAreIdentical(byte[] oldData, byte[] newData)
        {
            if (oldData == null || newData == null) {
                return oldData == newData;
            }
            if (oldData.LongLength != newData.LongLength) {
                return false;
            }

            for(long i = 0; i < newData.LongLength; i++) {
                if (oldData[i] != newData[i]) {
                    return false;
                }
            }

            return true;
        }

    }
}