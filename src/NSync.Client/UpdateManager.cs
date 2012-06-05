using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Text;
using NSync.Core;
using ReactiveUI;
using IEnableLogger = NSync.Core.IEnableLogger;

namespace NSync.Client
{
    public interface IUpdateManager
    {
        IObservable<UpdateInfo> CheckForUpdate();
        IObservable<Unit> DownloadReleases(IEnumerable<ReleaseEntry> releasesToDownload);
        IObservable<Unit> ApplyReleases(IEnumerable<ReleaseEntry> releasesToApply);
    }

    public class UpdateManager : IEnableLogger, IUpdateManager
    {
        readonly IFileSystemFactory fileSystem;
        readonly string rootAppDirectory;
        readonly IUrlDownloader urlDownloader;
        readonly string updateUrlOrPath;

        public UpdateManager(string urlOrPath, 
            string applicationName,
            string rootDirectory = null,
            IFileSystemFactory fileSystem = null,
            IUrlDownloader urlDownloader = null)
        {
            updateUrlOrPath = urlOrPath;

            rootAppDirectory = Path.Combine(rootDirectory ?? getLocalAppDataDirectory(), applicationName);
            this.fileSystem = fileSystem ?? new AnonFileSystem(
                s => new DirectoryInfoWrapper(new DirectoryInfo(s)),
                s => new FileInfoWrapper(new FileInfo(s)),
                s => new FileWrapper(),
                s => new DirectoryInfoWrapper(new DirectoryInfo(s).CreateRecursive()),
                s => new DirectoryInfo(s).Delete(true),
                Utility.CopyToAsync);

            this.urlDownloader = urlDownloader;
        }

        public IObservable<UpdateInfo> CheckForUpdate()
        {
            IEnumerable<ReleaseEntry> localReleases = Enumerable.Empty<ReleaseEntry>();

            try {
                var fi = fileSystem.GetFileInfo(Path.Combine(rootAppDirectory, "packages", "RELEASES"));
                var file = fi.OpenRead();

                // NB: sr disposes file
                using (var sr = new StreamReader(file, Encoding.UTF8)) {
                    localReleases = ReleaseEntry.ParseReleaseFile(sr.ReadToEnd());
                }
            } catch (Exception ex) {
                // Something has gone wrong, we'll start from scratch.
                this.Log().WarnException("Failed to load local release list", ex);
                initializeClientAppDirectory();
            }

            var releaseFile = isHttpUrl(updateUrlOrPath) ?
                urlDownloader.DownloadUrl(String.Format("{0}/{1}", updateUrlOrPath, "RELEASES")) :
                Observable.Return(File.ReadAllText(Path.Combine(updateUrlOrPath, "RELEASES")));

            var ret =  releaseFile
                .Select(ReleaseEntry.ParseReleaseFile)
                .Select(releases => determineUpdateInfo(localReleases, releases))
                .Multicast(new AsyncSubject<UpdateInfo>());

            ret.Connect();
            return ret;
        }

        public IObservable<Unit> DownloadReleases(IEnumerable<ReleaseEntry> releasesToDownload)
        {
            IObservable<Unit> downloadResult;

            if (isHttpUrl(updateUrlOrPath)) {
                var urls = releasesToDownload.Select(x => String.Format("{0}/{1}", updateUrlOrPath, x.Filename));
                var paths = releasesToDownload.Select(x => Path.Combine(rootAppDirectory, "packages", x.Filename));

                downloadResult = urlDownloader.QueueBackgroundDownloads(urls, paths);
            } else {
                downloadResult = releasesToDownload.ToObservable()
                    .Select(x => Observable.Defer(() =>
                        fileSystem.CopyAsync(
                            Path.Combine(updateUrlOrPath, x.Filename),
                            Path.Combine(rootAppDirectory, "packages", x.Filename))))
                    .Merge(2);
            }

            return downloadResult.SelectMany(_ => checksumPackages(releasesToDownload));
        }


        public IObservable<Unit> ApplyReleases(IEnumerable<ReleaseEntry> releasesToApply)
        {
            return Observable.Throw<Unit>(new NotImplementedException());
        }

        void initializeClientAppDirectory()
        {
            var pkgDir = Path.Combine(rootAppDirectory, "packages");
            if (fileSystem.GetDirectoryInfo(pkgDir).Exists) {
                fileSystem.DeleteDirectoryRecursive(pkgDir);
            }
            fileSystem.CreateDirectoryRecursive(pkgDir);
        }

        UpdateInfo determineUpdateInfo(IEnumerable<ReleaseEntry> localReleases, IEnumerable<ReleaseEntry> remoteReleases)
        {
            if (localReleases.Count() == remoteReleases.Count()) {
                this.Log().Info("No updates, remote and local are the same");
                return null;
            }

            if (localReleases.IsEmpty()) {
                this.Log().Warn("First run or local directory is corrupt, starting from scratch");

                var latestFullRelease = remoteReleases.Where(x => !x.IsDelta).MaxBy(x => x.Version);
                return UpdateInfo.Create(findCurrentVersion(localReleases), latestFullRelease.Take(1));
            }

            if (localReleases.Max(x => x.Version) >= remoteReleases.Max(x => x.Version)) {
                this.Log().Warn("hwhat, local version is greater than remote version");

                var latestFullRelease = remoteReleases.Where(x => !x.IsDelta).MaxBy(x => x.Version);
                return UpdateInfo.Create(findCurrentVersion(localReleases), latestFullRelease.Take(1));
            }

            return UpdateInfo.Create(findCurrentVersion(localReleases), remoteReleases);
        }

        ReleaseEntry findCurrentVersion(IEnumerable<ReleaseEntry> localReleases)
        {
            return localReleases.MaxBy(x => x.Version).Single(x => !x.IsDelta);
        }

        static string getLocalAppDataDirectory()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
        
        static bool isHttpUrl(string urlOrPath)
        {
            try {
                var url = new Uri(urlOrPath);
                return new[] {"https", "http"}.Contains(url.Scheme.ToLowerInvariant());
            } catch (Exception) {
                return false;
            }
        }

        IObservable<Unit> checksumPackages(IEnumerable<ReleaseEntry> releasesDownloaded)
        {
            return releasesDownloaded.ToObservable()
                .Select(x => Observable.Defer(() => checksumPackage(x)))
                .Merge(3)
                .Aggregate(Unit.Default, (acc, _) => acc);
        }

        IObservable<Unit> checksumPackage(ReleaseEntry downloadedRelease)
        {
            return Observable.Start(() => {
                var targetPackage = fileSystem.GetFileInfo(
                    Path.Combine(rootAppDirectory, "packages", downloadedRelease.Filename));

                if (!targetPackage.Exists) {
                    this.Log().Error("File should exist but doesn't", targetPackage.FullName);
                    throw new Exception("Checksummed file doesn't exist: " + targetPackage.FullName);
                }

                if (targetPackage.Length != downloadedRelease.Filesize) {
                    this.Log().Error("File Length should be {0}, is {1}", downloadedRelease.Filesize, targetPackage.Length);
                    File.Delete(targetPackage.FullName);
                    throw new Exception("Checksummed file size doesn't match: " + targetPackage.FullName);
                } 

                using (var file = targetPackage.OpenRead()) {
                    var hash = Utility.CalculateStreamSHA1(file);
                    if (hash != downloadedRelease.SHA1) {
                        this.Log().Error("File SHA1 should be {0}, is {1}", downloadedRelease.SHA1, hash);
                        File.Delete(targetPackage.FullName);
                        throw new Exception("Checksum doesn't match: " + targetPackage.FullName);
                    }
                }
            }, RxApp.TaskpoolScheduler);
        }
    }

    public class UpdateInfo
    {
        public Version Version { get; protected set; }
        public IEnumerable<ReleaseEntry> ReleasesToApply { get; protected set; }

        protected UpdateInfo(ReleaseEntry latestRelease, IEnumerable<ReleaseEntry> releasesToApply)
        {
            Version = latestRelease.Version;
            ReleasesToApply = releasesToApply;
        }

        public static UpdateInfo Create(ReleaseEntry currentVersion, IEnumerable<ReleaseEntry> availableReleases)
        {
            var newerThanUs = availableReleases.Where(x => x.Version > currentVersion.Version);
            var latestFull = availableReleases.MaxBy(x => x.Version).Single(x => !x.IsDelta);
            var deltasSize = newerThanUs.Where(x => x.IsDelta).Sum(x => x.Filesize);

            return (deltasSize > latestFull.Filesize)
                ? new UpdateInfo(latestFull, newerThanUs.Where(x => x.IsDelta).ToArray())
                : new UpdateInfo(latestFull, new[] {latestFull});
        }
    }
}