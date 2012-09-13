using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Ionic.Zip;
using MarkdownSharp;
using NuGet;
using ReactiveUI;

namespace Shimmer.Core
{
    public class ReleasePackage : IEnableLogger
    {
        public ReleasePackage(string inputPackageFile, bool isReleasePackage = false)
        {
            InputPackageFile = inputPackageFile;

            if (isReleasePackage) {
                ReleasePackageFile = inputPackageFile;
            }
        }

        public string InputPackageFile { get; protected set; }
        public string ReleasePackageFile { get; protected set; }

        public string SuggestedReleaseFileName {
            get {
                var zp = new ZipPackage(InputPackageFile);
                return String.Format("{0}-{1}-full.nupkg", zp.Id, zp.Version);
            }
        }

        public string CreateReleasePackage(string outputFile, string packagesRootDir = null)
        {
            Contract.Requires(!String.IsNullOrEmpty(outputFile));

            if (ReleasePackageFile != null) {
                return ReleasePackageFile;
            }

            // Recursively walk the dependency tree and extract all of the 
            // dependent packages into the a temporary directory.
            var package = new ZipPackage(InputPackageFile);
            var dependencies = findAllDependentPackages(package, packagesRootDir);

            string tempPath = null;

            using (Utility.WithTempDirectory(out tempPath)) {
                var tempDir = new DirectoryInfo(tempPath);

                using(var zf = new ZipFile(InputPackageFile)) {
                    zf.ExtractAll(tempPath);
                }
    
                extractDependentPackages(dependencies, tempDir);

                var specPath = tempDir.GetFiles("*.nuspec").First().FullName;

                removeDependenciesFromPackageSpec(specPath);
                removeSilverlightAssemblies(tempDir);
                removeDeveloperDocumentation(tempDir);

                renderReleaseNotesMarkdown(specPath);

                addDeltaFilesToContentTypes(tempDir.FullName);

                using (var zf = new ZipFile(outputFile)) {
                    zf.AddDirectory(tempPath);
                    zf.Save();
                }

                ReleasePackageFile = outputFile;
                return ReleasePackageFile;
            }
        }

        public ReleasePackage CreateDeltaPackage(ReleasePackage baseFixture, string outputFile)
        {
            Contract.Requires(baseFixture != null && baseFixture.ReleasePackageFile != null);
            Contract.Requires(!String.IsNullOrEmpty(outputFile) && !File.Exists(outputFile));

            string baseTempPath = null;
            string tempPath = null;

            using (Utility.WithTempDirectory(out baseTempPath))
            using (Utility.WithTempDirectory(out tempPath)) {
                var baseTempInfo = new DirectoryInfo(baseTempPath);
                var tempInfo = new DirectoryInfo(tempPath);

                using (var zf = new ZipFile(baseFixture.ReleasePackageFile)) {
                    zf.ExtractAll(baseTempInfo.FullName);
                }
                
                using (var zf = new ZipFile(ReleasePackageFile)) {
                    zf.ExtractAll(tempInfo.FullName);
                }

                // Collect a list of relative paths under 'lib' and map them 
                // to their full name. We'll use this later to determine in
                // the new version of the package whether the file exists or 
                // not.
                var baseLibFiles = baseTempInfo.GetAllFilesRecursively()
                    .Where(x => x.FullName.ToLowerInvariant().Contains("lib" + Path.DirectorySeparatorChar))
                    .ToDictionary(k => k.FullName.Replace(baseTempInfo.FullName, ""), v => v.FullName);

                var newLibDir = tempInfo.GetDirectories().First(x => x.Name.ToLowerInvariant() == "lib");

                // NB: There are three cases here that we'll handle:
                //
                // 1. Exists only in new => leave it alone, we'll use it directly.
                // 2. Exists in both old and new => write a dummy file so we know 
                //    to keep it.
                // 3. Exists in old but changed in new => create a delta file
                //
                // The fourth case of "Exists only in old => delete it in new" 
                // is handled when we apply the delta package
                newLibDir.GetAllFilesRecursively().ForEach(libFile => {
                    var relativePath = libFile.FullName.Replace(tempInfo.FullName, "");

                    if (!baseLibFiles.ContainsKey(relativePath)) {
                        this.Log().Info("{0} not found in base package, marking as new", relativePath);
                        return;
                    }

                    var oldData = File.ReadAllBytes(baseLibFiles[relativePath]);
                    var newData = File.ReadAllBytes(libFile.FullName);

                    if (bytesAreIdentical(oldData, newData)) {
                        this.Log().Info("{0} hasn't changed, writing dummy file", relativePath);

                        File.Create(libFile.FullName + ".diff").Dispose();
                        File.Create(libFile.FullName + ".shasum").Dispose();
                        libFile.Delete();
                        return;
                    }

                    this.Log().Info("Delta patching {0} => {1}", baseLibFiles[relativePath], libFile.FullName);
                    using (var of = File.Create(libFile.FullName + ".diff")) {
                        BinaryPatchUtility.Create(oldData, newData, of);

                        var rl = ReleaseEntry.GenerateFromFile(new MemoryStream(newData), libFile.Name + ".shasum");
                        File.WriteAllText(libFile.FullName + ".shasum", rl.EntryAsString, Encoding.UTF8);
                        libFile.Delete();
                    }
                });

                addDeltaFilesToContentTypes(tempInfo.FullName);

                using (var zf = new ZipFile(outputFile)) {
                    zf.AddDirectory(tempInfo.FullName);
                    zf.Save();
                }
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

                var deltaPathRelativePaths = new DirectoryInfo(deltaPath).GetAllFilesRecursively()
                    .Select(x => x.FullName.Replace(deltaPath + Path.DirectorySeparatorChar, ""))
                    .ToArray();

                // Apply all of the .diff files
                deltaPathRelativePaths
                    .Where(x => x.StartsWith("lib", StringComparison.InvariantCultureIgnoreCase))
                    .ForEach(file => {
                        pathsVisited.Add(Regex.Replace(file, @".diff$", "").ToLowerInvariant());
                        applyDiffToFile(deltaPath, file, workingPath);
                    });

                // Delete all of the files that were in the old package but 
                // not in the new one.
                new DirectoryInfo(workingPath).GetAllFilesRecursively()
                    .Select(x => x.FullName.Replace(workingPath + Path.DirectorySeparatorChar, "").ToLowerInvariant())
                    .Where(x => x.StartsWith("lib", StringComparison.InvariantCultureIgnoreCase) && !pathsVisited.Contains(x))
                    .ForEach(x => { 
                        this.Log().Info("{0} was in old package but not in new one, deleting", x);
                        File.Delete(Path.Combine(workingPath, x));
                    });

                // Update all the files that aren't in 'lib' with the delta 
                // package's versions (i.e. the nuspec file, etc etc).
                deltaPathRelativePaths
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

        void applyDiffToFile(string deltaPath, string relativeFilePath, string workingDirectory)
        {
            var inputFile = Path.Combine(deltaPath, relativeFilePath);
            var finalTarget = Path.Combine(workingDirectory, Regex.Replace(relativeFilePath, @".diff$", ""));

            var tempTargetFile = Path.GetTempFileName();

            // NB: Zero-length diffs indicate the file hasn't actually changed
            if (new FileInfo(inputFile).Length == 0) {
                this.Log().Info("{0} exists unchanged, skipping", relativeFilePath);
                return;
            }

            if (relativeFilePath.EndsWith(".diff", StringComparison.InvariantCultureIgnoreCase)) {
                using (var of = File.OpenWrite(tempTargetFile))
                using (var inf = File.OpenRead(finalTarget)) {
                    this.Log().Info("Applying Diff to {0}", relativeFilePath);
                    BinaryPatchUtility.Apply(inf, () => File.OpenRead(inputFile), of);
                }

                try {
                    verifyPatchedFile(relativeFilePath, inputFile, tempTargetFile);
                } catch (Exception) {
                    File.Delete(tempTargetFile);
                    throw;
                }
            } else {
                using (var of = File.OpenWrite(tempTargetFile))
                using (var inf = File.OpenRead(inputFile)) {
                    this.Log().Info("Adding new file: {0}", relativeFilePath);
                    inf.CopyTo(of);
                }
            }

            File.Delete(finalTarget);
            File.Move(tempTargetFile, finalTarget);
        }

        void verifyPatchedFile(string relativeFilePath, string inputFile, string tempTargetFile)
        {
            var shaFile = Regex.Replace(inputFile, @"\.diff$", ".shasum");
            var expectedReleaseEntry = ReleaseEntry.ParseReleaseEntry(File.ReadAllText(shaFile, Encoding.UTF8));
            var actualReleaseEntry = ReleaseEntry.GenerateFromFile(tempTargetFile);

            if (expectedReleaseEntry.Filesize != actualReleaseEntry.Filesize) {
                this.Log().Warn("Patched file {0} has incorrect size, expected {1}, got {2}", relativeFilePath,
                    expectedReleaseEntry.Filesize, actualReleaseEntry.Filesize);
                throw new ChecksumFailedException() {Filename = relativeFilePath};
            }

            if (expectedReleaseEntry.SHA1 != actualReleaseEntry.SHA1) {
                this.Log().Warn("Patched file {0} has incorrect SHA1, expected {1}, got {2}", relativeFilePath,
                    expectedReleaseEntry.SHA1, actualReleaseEntry.SHA1);
                throw new ChecksumFailedException() {Filename = relativeFilePath};
            }
        }

        void extractDependentPackages(IEnumerable<IPackage> dependencies, DirectoryInfo tempPath)
        {
            dependencies.ForEach(pkg => {
                this.Log().Info("Scanning {0}", pkg.Id);

                pkg.GetFiles().Where(x => x.Path.StartsWith("lib", true, CultureInfo.InvariantCulture)).ForEach(file => {
                    var outPath = new FileInfo(Path.Combine(tempPath.FullName, file.Path));

                    outPath.Directory.CreateRecursive();

                    using (var of = File.Create(outPath.FullName)) {
                        this.Log().Info("Writing {0} to {1}", file.Path, outPath);
                        file.GetStream().CopyTo(of);
                    }
                });
            });
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
            // NB: Nuke Silverlight and WinRT. We can't tell as easily if other
            // profiles can be removed because you can load net20 DLLs inside 
            // .NET 4.0 apps
            var libPath = expandedRepoPath.GetDirectories().First(x => x.Name.ToLowerInvariant() == "lib");
            this.Log().Debug(libPath.FullName);

            var bannedFrameworks = new[] {"sl", "winrt", "netcore", };

            libPath.GetDirectories()
                .Where(x => bannedFrameworks.Any(y => x.Name.ToLowerInvariant().StartsWith(y, StringComparison.InvariantCultureIgnoreCase)))
                .Do(x => this.Log().Info("Deleting {0}", x.Name))
                .ForEach(x => x.Delete(true));
        }

        void renderReleaseNotesMarkdown(string specPath)
        {
            var doc = new XmlDocument();
            doc.Load(specPath);

            // XXX: This code looks full tart
            var metadata = doc.DocumentElement.ChildNodes
                .OfType<XmlElement>()
                .First(x => x.Name.ToLowerInvariant() == "metadata");

            var releaseNotes = metadata.ChildNodes
                .OfType<XmlElement>()
                .FirstOrDefault(x => x.Name.ToLowerInvariant() == "releasenotes");

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
            var dependenciesNode = metadata.ChildNodes.OfType<XmlElement>().FirstOrDefault(x => x.Name.ToLowerInvariant() == "dependencies");
            if (dependenciesNode != null) {
                metadata.RemoveChild(dependenciesNode);
            }

            xdoc.Save(specPath);
        }

        IEnumerable<IPackage> findAllDependentPackages(IPackage package = null, string packagesRootDir = null)
        {
            package = package ?? new ZipPackage(InputPackageFile);

            var deps = package.DependencySets.SelectMany(x => x.Dependencies);
            return deps.SelectMany(dependency => {
                var ret = findPackageFromName(dependency.Id, dependency.VersionSpec, packagesRootDir);

                if (ret == null) {
                    this.Log().Error("Couldn't find file for package in {1}: {0}", dependency.Id, packagesRootDir);
                    return Enumerable.Empty<IPackage>();
                }

                return findAllDependentPackages(ret, packagesRootDir).StartWith(ret).Distinct(y => y.GetFullName() + y.Version);
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
            // Apply a VersionSpec to a specific Version (this code is nicked 
            // from NuGet)
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

        void addDeltaFilesToContentTypes(string rootDirectory)
        {
            var elements = new[] {
                new { Extension = "diff", ContentType = "application/octet" },
                new { Extension = "exe", ContentType = "application/octet" },
                new { Extension = "dll", ContentType = "application/octet" },
                new { Extension = "shasum", ContentType = "text/plain" },
            };

            var doc = new XmlDocument();
            var path = Path.Combine(rootDirectory, "[Content_Types].xml");
            doc.Load(path);

            var typesElement = doc.FirstChild.NextSibling;
            if (typesElement.Name.ToLowerInvariant() != "types") {
                throw new Exception("Invalid ContentTypes file, expected root node should be 'Types'");
            }

            var existingTypes = typesElement.ChildNodes.OfType<XmlElement>()
                .ToDictionary(k => k.GetAttribute("Extension").ToLowerInvariant(), k => k.GetAttribute("ContentType").ToLowerInvariant());

            elements
                .Where(x => !existingTypes.ContainsKey(x.Extension.ToLowerInvariant()))
                .Select(element => {
                    var ret = doc.CreateElement("Default", typesElement.NamespaceURI);
                    var ext = doc.CreateAttribute("Extension"); ext.Value = element.Extension;
                    var ct = doc.CreateAttribute("ContentType"); ct.Value = element.ContentType;
                    new[] { ext, ct }.ForEach(x => ret.Attributes.Append(x));

                    return ret;
                }).ForEach(x => typesElement.AppendChild(x));

            using (var sw = new StreamWriter(path, false, Encoding.UTF8)) {
                doc.Save(sw);
            }
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

    public class ChecksumFailedException : Exception
    {
        public string Filename { get; set; }
    }
}