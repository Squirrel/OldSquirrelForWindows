using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NSync.Core;
using NuGet;
using ReactiveUI;
using IEnableLogger = NSync.Core.IEnableLogger;

namespace NSync.Client
{
    public interface IUpdateManager
    {
        IObservable<UpdateInfo> CheckForUpdate(bool ignoreDeltaUpdates = false);
        IObservable<Unit> DownloadReleases(IEnumerable<ReleaseEntry> releasesToDownload);
        IObservable<Unit> ApplyReleases(UpdateInfo updatesToApply);
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
            Contract.Requires(!String.IsNullOrEmpty(urlOrPath));
            Contract.Requires(!String.IsNullOrEmpty(applicationName));

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

        public IObservable<UpdateInfo> CheckForUpdate(bool ignoreDeltaUpdates = false)
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
                .SelectMany(releases => determineUpdateInfo(localReleases, releases, ignoreDeltaUpdates))
                .Multicast(new AsyncSubject<UpdateInfo>());

            ret.Connect();
            return ret;
        }

        public IObservable<Unit> DownloadReleases(IEnumerable<ReleaseEntry> releasesToDownload)
        {
            Contract.Requires(releasesToDownload != null);

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

            return downloadResult.SelectMany(_ => checksumAllPackages(releasesToDownload));
        }

        public IObservable<Unit> ApplyReleases(UpdateInfo updatesToApply)
        {
            Contract.Requires(updatesToApply != null);
            var fullPackageToApply = createFullPackagesFromDeltas(updatesToApply.ReleasesToApply, updatesToApply.CurrentlyInstalledVersion);

            return fullPackageToApply.SelectMany(release => Observable.Start(() => {
                var pkg = new ZipPackage(Path.Combine(rootAppDirectory, "packages", release.Filename));
                var target = new DirectoryInfo(Path.Combine(rootAppDirectory, "app-" + release.Version));
                target.Create();

                // NB: We sort this list in order to guarantee that if a Net20
                // and a Net40 version of a DLL get shipped, we always end up
                // with the 4.0 version.
                pkg.GetFiles()
                    .Where(x => x.Path.StartsWith("lib", StringComparison.InvariantCultureIgnoreCase))
                    .OrderBy(x => x.Path)
                    .ForEach(x => {
                        var m = Regex.Match(x.Path, @".*\\([^\\]+)$");
                        var targetPath = Path.Combine(target.FullName, m.Groups[1].Value);

                        if (File.Exists(targetPath)) {
                            File.Delete(targetPath);
                        }

                        using (var inf = x.GetStream())
                        using (var of = File.Open(targetPath, FileMode.CreateNew, FileAccess.Write)) {
                            this.Log().Info("Writing {0} to app directory", targetPath);
                            inf.CopyTo(of);
                        }
                    });
            }));
        }

        void initializeClientAppDirectory()
        {
            var pkgDir = Path.Combine(rootAppDirectory, "packages");
            if (fileSystem.GetDirectoryInfo(pkgDir).Exists) {
                fileSystem.DeleteDirectoryRecursive(pkgDir);
            }
            fileSystem.CreateDirectoryRecursive(pkgDir);
        }

        IObservable<UpdateInfo> determineUpdateInfo(IEnumerable<ReleaseEntry> localReleases, IEnumerable<ReleaseEntry> remoteReleases, bool ignoreDeltaUpdates)
        {
            localReleases = localReleases ?? Enumerable.Empty<ReleaseEntry>();

            if (remoteReleases == null) {
                this.Log().Warn("Release information couldn't be determined due to remote corrupt RELEASES file");
                return Observable.Throw<UpdateInfo>(new Exception("Corrupt remote RELEASES file"));
            }

            if (localReleases.Count() == remoteReleases.Count()) {
                this.Log().Info("No updates, remote and local are the same");
                return Observable.Return<UpdateInfo>(null);
            }

            if (ignoreDeltaUpdates) {
                remoteReleases = remoteReleases.Where(x => !x.IsDelta);
            }

            if (localReleases.IsEmpty()) {
                this.Log().Warn("First run or local directory is corrupt, starting from scratch");

                var latestFullRelease = findCurrentVersion(remoteReleases);
                return Observable.Return(UpdateInfo.Create(findCurrentVersion(localReleases), new[] {latestFullRelease}));
            }

            if (localReleases.Max(x => x.Version) >= remoteReleases.Max(x => x.Version)) {
                this.Log().Warn("hwhat, local version is greater than remote version");

                var latestFullRelease = findCurrentVersion(remoteReleases);
                return Observable.Return(UpdateInfo.Create(findCurrentVersion(localReleases), new[] {latestFullRelease}));
            }

            return Observable.Return(UpdateInfo.Create(findCurrentVersion(localReleases), remoteReleases));
        }

        ReleaseEntry findCurrentVersion(IEnumerable<ReleaseEntry> localReleases)
        {
            if (!localReleases.Any()) {
                return null;
            }

            return localReleases.MaxBy(x => x.Version).SingleOrDefault(x => !x.IsDelta);
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

        IObservable<Unit> checksumAllPackages(IEnumerable<ReleaseEntry> releasesDownloaded)
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
                    targetPackage.Delete();
                    throw new Exception("Checksummed file size doesn't match: " + targetPackage.FullName);
                } 

                using (var file = targetPackage.OpenRead()) {
                    var hash = Utility.CalculateStreamSHA1(file);
                    if (hash != downloadedRelease.SHA1) {
                        this.Log().Error("File SHA1 should be {0}, is {1}", downloadedRelease.SHA1, hash);
                        targetPackage.Delete();
                        throw new Exception("Checksum doesn't match: " + targetPackage.FullName);
                    }
                }
            }, RxApp.TaskpoolScheduler);
        }

        IObservable<ReleaseEntry> createFullPackagesFromDeltas(IEnumerable<ReleaseEntry> releasesToApply, ReleaseEntry currentVersion)
        {
            if (!releasesToApply.Any() || releasesToApply.All(x => !x.IsDelta)) {
                return Observable.Return(releasesToApply.MaxBy(x => x.Version).First());
            }

            if (!releasesToApply.Any(x => x.IsDelta)) {
                return Observable.Throw<ReleaseEntry>(new Exception("Cannot apply combinations of delta and full packages"));
            }

            var ret = Observable.Start(() => {
                var basePkg = new ReleasePackage(Path.Combine(rootAppDirectory, "packages", currentVersion.Filename));
                var deltaPkg = new ReleasePackage(Path.Combine(rootAppDirectory, "packages", releasesToApply.First().Filename));

                return basePkg.ApplyDeltaPackage(deltaPkg,
                    Regex.Replace(deltaPkg.InputPackageFile, @"-delta.nupkg$", ".nupkg", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
            }, RxApp.TaskpoolScheduler);

            if (releasesToApply.Count() == 1) {
                return ret.Select(x => ReleaseEntry.GenerateFromFile(x.InputPackageFile));
            }

            return ret.SelectMany(x =>
                createFullPackagesFromDeltas(releasesToApply.Skip(1), ReleaseEntry.GenerateFromFile(File.OpenRead(x.InputPackageFile), x.InputPackageFile)));
        }
    }

    public class UpdateInfo
    {
        public Version Version { get; protected set; }
        public ReleaseEntry CurrentlyInstalledVersion { get; protected set; }
        public IEnumerable<ReleaseEntry> ReleasesToApply { get; protected set; }

        protected UpdateInfo(ReleaseEntry currentlyInstalledVersion, IEnumerable<ReleaseEntry> releasesToApply)
        {
            // NB: When bootstrapping, CurrentlyInstalledVersion is null!
            CurrentlyInstalledVersion = currentlyInstalledVersion;
            Version = currentlyInstalledVersion != null ? currentlyInstalledVersion.Version : null;
            ReleasesToApply = releasesToApply ?? Enumerable.Empty<ReleaseEntry>();
        }

        public static UpdateInfo Create(ReleaseEntry currentVersion, IEnumerable<ReleaseEntry> availableReleases)
        {
            Contract.Requires(availableReleases != null);

            var latestFull = availableReleases.MaxBy(x => x.Version).FirstOrDefault(x => !x.IsDelta);
            if (latestFull == null) {
                throw new Exception("There should always be at least one full release");
            }

            if (currentVersion == null) {
                return new UpdateInfo(currentVersion, new[] { latestFull });
            }

            var newerThanUs = availableReleases.Where(x => x.Version > currentVersion.Version);
            var deltasSize = newerThanUs.Where(x => x.IsDelta).Sum(x => x.Filesize);

            return (deltasSize < latestFull.Filesize && deltasSize > 0)
                ? new UpdateInfo(currentVersion, newerThanUs.Where(x => x.IsDelta).ToArray())
                : new UpdateInfo(currentVersion, new[] { latestFull });
        }
    }
}