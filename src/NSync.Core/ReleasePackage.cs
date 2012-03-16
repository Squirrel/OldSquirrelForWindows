using System;
using System.Collections.Generic;
using System.Linq;
using NuGet;

namespace NSync.Core
{
    public class ReleasePackage : IEnableLogger
    {
        readonly string packageFile;
        public ReleasePackage(string inputPackageFile)
        {
            packageFile = inputPackageFile;
        }

        public void CreateReleasePackage(string outputFile)
        {
            throw new NotImplementedException();
        }

        IEnumerable<IPackage> findAllDependentPackages(IPackage package = null, string packagesRootDir = null)
        {
            package = package ?? new ZipPackage(packageFile);

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
                localPackages = Utility.GetAllFilesRecursively(packagesRootDir)
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