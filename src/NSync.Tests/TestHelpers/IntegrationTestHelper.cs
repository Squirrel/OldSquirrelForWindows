using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using NSync.Core;
using ReactiveUI;

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

        public static IDisposable WithTempDirectory(out string path)
        {
            var di = new DirectoryInfo(Environment.GetEnvironmentVariable("TEMP"));
            if (!di.Exists) {
                throw new Exception("%TEMP% isn't defined, go set it");
            }

            var tempDir = di.CreateSubdirectory(Guid.NewGuid().ToString());
            path = tempDir.FullName;

            return Disposable.Create(() =>
                DeleteDirectory(tempDir.FullName));
        }

        public static void DeleteDirectory(string directoryPath)
        {
            // From http://stackoverflow.com/questions/329355/cannot-delete-directory-with-directory-deletepath-true/329502#329502
            string[] files = Directory.GetFiles(directoryPath);
            string[] dirs = Directory.GetDirectories(directoryPath);

            foreach (string file in files) {
                File.SetAttributes(file, FileAttributes.Normal);
                string filePath = file;
                (new Action(() => File.Delete(Path.Combine(directoryPath, filePath)))).Retry();
            }

            foreach (string dir in dirs) {
                DeleteDirectory(Path.Combine(directoryPath, dir));
            }

            File.SetAttributes(directoryPath, FileAttributes.Normal);
            Directory.Delete(directoryPath, false);
        }
    }
}
