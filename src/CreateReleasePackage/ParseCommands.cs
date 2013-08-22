using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Options;
using NuGet;

namespace CreateReleasePackage
{
    public static class ParseCommands
    {
        public static Dictionary<string, string> ParseOptions(string[] args)
        {
            bool showHelp = false;
            string targetDir = null;
            string packagesDir = null;
            string templateSource = null;

            var opts = new OptionSet() {
                { "p|packages-directory=", "(Optional) The NuGet packages directory to use, omit to use default", v => packagesDir = v },
                { "o|output-directory=", "The target directory to put the generated file", v => targetDir = v },
                { "preprocess-template=", "The template file to parse. Part of Create-Release.ps1, ignore this", v => templateSource = v },
                { "h|help", "Show this message and exit", v => showHelp = v != null },
            };

            var filename = opts.Parse(args).FirstOrDefault();

            if (String.IsNullOrWhiteSpace(filename)) {
                Console.Error.WriteLine("Please specify an existing NuGet package");
                showHelp = true;
            } else if (!File.Exists(filename)) {
                Console.Error.WriteLine("File '{0}' doesn't exist. Please specify an existing NuGet package", filename);
                showHelp = true;
            }

            if (templateSource != null) {
                Console.WriteLine((string) processTemplateFile(filename, templateSource));
                return null;
            }

            if (String.IsNullOrWhiteSpace(targetDir))
            {
                Console.Error.WriteLine("No value specified for Release directory");
                showHelp = true;
            } else if (!Directory.Exists(targetDir)) {
                Console.Error.WriteLine("Directory '{0}' doesn't exist. Please specify an existing Release directory", filename);
                showHelp = true;
            }

            if (String.IsNullOrWhiteSpace(packagesDir)) {
                Console.Error.WriteLine("No value specified for packages directory");
                showHelp = true;
            } else if (!Directory.Exists(packagesDir)) {
                Console.Error.WriteLine("Directory '{0}' doesn't exist. Please specify an existing packages directory", filename);
                showHelp = true;
            }

            if (showHelp) {
                Console.WriteLine("\nCreateReleasePackage - take a NuGet package and create a Release Package");
                Console.WriteLine(@"Usage: CreateReleasePackage.exe [Options] \path\to\app.nupkg");

                Console.WriteLine("Options:");
                foreach(var v in opts) {
                    if (v.GetNames().Length != 2) {
                        Console.WriteLine("  --{0} - {1}", v.GetNames()[0], v.Description);
                    } else {
                        Console.WriteLine("  -{0}/--{1} - {2}", v.GetNames()[0], v.GetNames()[1], v.Description);
                    }
                }

                return null;
            }

            return new Dictionary<string, string>() {
                { "input", filename },
                { "target", targetDir },
                { "pkgdir", packagesDir ?? ""},
            };
        }

        static string processTemplateFile(string packageFile, string templateFile)
        {
            var zp = new ZipPackage(packageFile);
            var noBetaRegex = new Regex(@"-.*$");

            checkIfNuspecHasRequiredFields(zp, packageFile);

            var toSub = new[] {
                new {Name = "Authors", Value = String.Join(", ", zp.Authors ?? Enumerable.Empty<string>())},
                new {Name = "Description", Value = zp.Description},
                new {Name = "IconUrl", Value = zp.IconUrl != null ? zp.IconUrl.ToString() : ""},
                new {Name = "LicenseUrl", Value = zp.LicenseUrl != null ? zp.LicenseUrl.ToString() : ""},
                new {Name = "ProjectUrl", Value = zp.ProjectUrl != null ? zp.ProjectUrl.ToString() : ""},
                new {Name = "Summary", Value = zp.Summary ?? zp.Title },
                new {Name = "Title", Value = zp.Title},
                new {Name = "Version", Value = noBetaRegex.Replace(zp.Version.ToString(), "") },
            };

            var output = toSub.Aggregate(new StringBuilder(File.ReadAllText(templateFile)), (acc, x) =>
                { acc.Replace(String.Format("$(var.NuGetPackage_{0})", x.Name), x.Value); return acc; });

            var ret = Path.GetTempFileName();
            File.WriteAllText(ret, output.ToString(), Encoding.UTF8);
            return ret;
        }

        static void checkIfNuspecHasRequiredFields(IPackageMetadata zp, string packageFile)
        {
            if (String.IsNullOrWhiteSpace(zp.Id))
                throw new Exception(String.Format("Invalid 'id' value in nuspec file at '{0}'", packageFile));

            if (String.IsNullOrWhiteSpace(zp.Version.ToString()))
                throw new Exception(String.Format("Invalid 'version' value in nuspec file at '{0}'", packageFile));

            if (zp.Authors.All(String.IsNullOrWhiteSpace))
                throw new Exception(String.Format("Invalid 'authors' value in nuspec file at '{0}'", zp.Authors));

            if (String.IsNullOrWhiteSpace(zp.Description))
                throw new Exception(String.Format("Invalid 'description' value in nuspec file at '{0}'", zp.Description));
        }
    }
}