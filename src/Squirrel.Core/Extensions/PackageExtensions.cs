using System;
using NuGet;

namespace Shimmer.Core.Extensions
{
    public static class PackageExtensions
    {
        public static string ExtractTitle(this IPackage package)
        {
            if (package == null)
                return String.Empty;

            var title = package.Title;
            return !String.IsNullOrWhiteSpace(title) ? title : package.Id;
        }
    }
}