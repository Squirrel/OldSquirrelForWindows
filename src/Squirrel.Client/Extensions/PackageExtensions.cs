using System.Diagnostics.Contracts;
using System.Linq;
using NuGet;

namespace Shimmer.Client.Extensions
{
    public static class PackageExtensions
    {
        public static FrameworkVersion DetectFrameworkVersion(this IPackage package)
        {
            Contract.Requires(package != null);

            return package.GetFiles().Any(x => x.Path.Contains("lib") && x.Path.Contains("45"))
                ? FrameworkVersion.Net45
                : FrameworkVersion.Net40;
        }
    }
}
