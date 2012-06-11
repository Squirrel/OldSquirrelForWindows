using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Threading;
using ReactiveUI;

namespace NSync.Core
{
    public static class Utility
    {
        public static IEnumerable<FileInfo> GetAllFilesRecursively(this DirectoryInfo rootPath)
        {
            Contract.Requires(rootPath != null);

            return rootPath.GetDirectories()
                .SelectMany(GetAllFilesRecursively)
                .Concat(rootPath.GetFiles());
        }

        public static DirectoryInfo CreateRecursive(this DirectoryInfo This)
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

            return This;
        }

        public static string CalculateStreamSHA1(Stream file)
        {
            Contract.Requires(file != null && file.CanRead);

            var sha1 = SHA1.Create();
            return BitConverter.ToString(sha1.ComputeHash(file)).Replace("-", String.Empty);
        }

        public static IObservable<Unit> CopyToAsync(string from, string to)
        {
            Contract.Requires(!String.IsNullOrEmpty(from) && File.Exists(from));
            Contract.Requires(!String.IsNullOrEmpty(to));

            // XXX: SafeCopy
            return Observable.Start(() => File.Copy(@from, to, true), RxApp.TaskpoolScheduler);
        }

        public static void Retry(this Action block, int retries = 2)
        {
            Contract.Requires(retries > 0);

            Func<object> thunk = () => {
                block();
                return null;
            };

            thunk.Retry(retries);
        }

        public static T Retry<T>(this Func<T> block, int retries = 2)
        {
            Contract.Requires(retries > 0);

            while (true) {
                try {
                    T ret = block();
                    return ret;
                } catch (Exception) {
                    if (retries == 0) {
                        throw;
                    }

                    retries--;
                    Thread.Sleep(250);
                }
            }
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
            Contract.Requires(!String.IsNullOrEmpty(directoryPath) && Directory.Exists(directoryPath));

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