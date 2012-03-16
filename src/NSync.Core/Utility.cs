using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NSync.Core
{
    public static class Utility
    {
        public static IEnumerable<FileInfo> GetAllFilesRecursively(string rootPath)
        {
            var di = new DirectoryInfo(rootPath);

            return di.GetDirectories()
                .SelectMany(x => GetAllFilesRecursively(x.FullName))
                .Concat(di.GetFiles());
        }
    }
}