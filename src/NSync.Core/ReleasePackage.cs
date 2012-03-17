using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using Ionic.Zip;
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
            if (ReleasePackageFile != null) {
                return ReleasePackageFile;
            }

            var package = new ZipPackage(InputPackageFile);
            var dependencies = findAllDependentPackages(package, packagesRootDir);

            var tempPath = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            tempPath.Create();

            try {
                var zf = new ZipFile(InputPackageFile);
                zf.ExtractAll(tempPath.FullName);
    
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

                removeDependenciesFromPackageSpec(tempPath.GetFiles("*.nuspec").First().FullName);

                // NB: Nuke Silverlight. We can't tell as easily if other
                // profiles can be removed because you can load net20 DLLs
                // inside .NET 4.0 apps
                var libPath = tempPath.GetDirectories().First(x => x.Name.ToLowerInvariant() == "lib");
                this.Log().Debug(libPath.FullName);
                libPath.GetDirectories()
                    .Where(x => x.Name.ToLowerInvariant().StartsWith("sl"))
                    .Do(x => this.Log().Info("Deleting {0}", x.Name))
                    .ForEach(x => x.Delete(true));
    
                zf = new ZipFile(outputFile);
                zf.AddDirectory(tempPath.FullName);
                zf.Save();

                ReleasePackageFile = outputFile;
                return ReleasePackageFile;
            } finally {
                tempPath.Delete(true);
            }
        }

        public void CreateDeltaPackage(ReleasePackage baseFixture, string outputFile)
        {
            var baseTempPath = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            baseTempPath.Create();

            var tempPath = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            tempPath.Create();

            try {
                var zf = new ZipFile(baseFixture.ReleasePackageFile);
                zf.ExtractAll(baseTempPath.FullName);

                zf = new ZipFile(ReleasePackageFile);
                zf.ExtractAll(tempPath.FullName);

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

                zf = new ZipFile(outputFile);
                zf.AddDirectory(tempPath.FullName);
                zf.Save();
            } finally {
                baseTempPath.Delete(true);
                tempPath.Delete(true);
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
            IQueryable<IPackage> localPackages = Enumerable.Empty<IPackage>().AsQueryable();
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


    }
}