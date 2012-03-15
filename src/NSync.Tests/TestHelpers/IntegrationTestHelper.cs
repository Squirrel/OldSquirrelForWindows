using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace NSync.Tests.TestHelpers
{
    public static class IntegrationTestHelper
    {
        public static string GetPath(params string[] paths)
        {
            var ret = GetIntegrationTestRootDirectory();
            return (new FileInfo(paths.Aggregate (ret, Path.Combine))).FullName;
        }

        public static string GetIntegrationTestRootDirectory()
        {
            // XXX: This is an evil hack, but it's okay for a unit test
            // We can't use Assembly.Location because unit test runners love
            // to move stuff to temp directories
            var st = new StackFrame(true);
            var di = new DirectoryInfo(Path.Combine(Path.GetDirectoryName(st.GetFileName()), ".."));

            return di.FullName;
        }

        public static bool SkipTestOnXPAndVista()
        {
            int osVersion = Environment.OSVersion.Version.Major*100 + Environment.OSVersion.Version.Minor;
            return (osVersion < 601);
        }

        public static IEnumerable<FileInfo> GetAllFilesRecursively(string rootPath)
        {
            var di = new DirectoryInfo(rootPath);

            return di.GetDirectories()
                .SelectMany(x => GetAllFilesRecursively(x.FullName))
                .Concat(di.GetFiles());
        }
    }
}
