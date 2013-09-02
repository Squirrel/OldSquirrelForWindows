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
using NuGet;
using ReactiveUIMicro;
using Shimmer.Core.Extensions;

namespace Shimmer.Core
{
    public interface IReleasePackage
    {
        string InputPackageFile { get; }
        string ReleasePackageFile { get; }
        string SuggestedReleaseFileName { get; }

        string CreateReleasePackage(string outputFile, string packagesRootDir = null, Func<string, string> releaseNotesProcessor = null);
    }

    public static class VersionComparer
    {
        public static bool Compare(IVersionSpec versionSpec, SemanticVersion version)
        {
            if (versionSpec == null)
                return true; // I CAN'T DEAL WITH THIS

            bool minVersion;
            if (versionSpec.MinVersion == null) {
                minVersion = true; // no preconditon? LET'S DO IT
            } else if (versionSpec.IsMinInclusive) {
                minVersion = version >= versionSpec.MinVersion;
            } else {
                minVersion = version > versionSpec.MinVersion;
            }

            bool maxVersion;
            if (versionSpec.MaxVersion == null) {
                maxVersion = true; // no preconditon? LET'S DO IT
            } else if (versionSpec.IsMaxInclusive) {
                maxVersion = version <= versionSpec.MaxVersion;
            } else {
                maxVersion = version < versionSpec.MaxVersion;
            }

            return maxVersion && minVersion;
        }
    }

    public class ReleasePackage : IEnableLogger, IReleasePackage
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

        public Version Version { get { return InputPackageFile.ToVersion(); } }

        public string CreateReleasePackage(string outputFile, string packagesRootDir = null, Func<string, string> releaseNotesProcessor = null)
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
                removeNonDesktopAssemblies(tempDir);
                removeDeveloperDocumentation(tempDir);

                if (releaseNotesProcessor != null) {
                    renderReleaseNotesMarkdown(specPath, releaseNotesProcessor);
                }

                addDeltaFilesToContentTypes(tempDir.FullName);

                using (var zf = new ZipFile(outputFile)) {
                    zf.AddDirectory(tempPath);
                    zf.Save();
                }

                ReleasePackageFile = outputFile;
                return ReleasePackageFile;
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

        void removeNonDesktopAssemblies(DirectoryInfo expandedRepoPath)
        {
            // NB: Nuke Silverlight, WinRT, WindowsPhone and Xamarin assemblies. 
            // We can't tell as easily if other profiles can be removed because 
            // you can load net20 DLLs inside .NET 4.0 apps
            var libPath = expandedRepoPath.GetDirectories().First(x => x.Name.ToLowerInvariant() == "lib");
            this.Log().Debug(libPath.FullName);

            var bannedFrameworks = new[] {"sl", "winrt", "netcore", "win8", "windows8", "MonoAndroid", "MonoTouch", "MonoMac", "wp", };

            libPath.GetDirectories()
                .Where(x => bannedFrameworks.Any(y => x.Name.ToLowerInvariant().StartsWith(y, StringComparison.InvariantCultureIgnoreCase)))
                .Do(x => this.Log().Info("Deleting {0}", x.Name))
                .ForEach(x => x.Delete(true));
        }

        void renderReleaseNotesMarkdown(string specPath, Func<string, string> releaseNotesProcessor)
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

            releaseNotes.InnerText = String.Format("<![CDATA[\n" + "{0}\n" + "]]>",
                releaseNotesProcessor(releaseNotes.InnerText));

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
            }).ToArray();
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

        static IPackage findPackageFromNameInList(string id, IVersionSpec versionSpec, IQueryable<IPackage> packageList)
        {
            return packageList.Where(x => x.Id == id).ToArray()
                .FirstOrDefault(x => VersionComparer.Compare(versionSpec, x.Version));
        }

        static internal void addDeltaFilesToContentTypes(string rootDirectory)
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
    }

    public class ChecksumFailedException : Exception
    {
        public string Filename { get; set; }
    }
}