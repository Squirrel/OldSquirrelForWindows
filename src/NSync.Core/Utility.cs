using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NSync.Core
{
    public static class Utility
    {
        public static IEnumerable<FileInfo> GetAllFilesRecursively(this DirectoryInfo rootPath)
        {
            return rootPath.GetDirectories()
                .SelectMany(GetAllFilesRecursively)
                .Concat(rootPath.GetFiles());
        }

        public static void CreateRecursive(this DirectoryInfo This)
        {
            This.FullName.Split(Path.DirectorySeparatorChar).scan("", (acc, x) =>
            {
                var path = Path.Combine(acc, x);

                if (path[path.Length - 1] == Path.VolumeSeparatorChar)
                {
                    path += Path.DirectorySeparatorChar;
                }

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                return (new DirectoryInfo(path)).FullName;
            });
        }
        
        static TAcc scan<T, TAcc>(this IEnumerable<T> This, TAcc initialValue, Func<TAcc, T, TAcc> accFunc)
        {
            TAcc acc = initialValue;

            foreach (var x in This)
            {
                acc = accFunc(acc, x);
            }

            return acc;
        }

    }
}