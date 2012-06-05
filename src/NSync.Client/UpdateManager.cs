using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using NSync.Core;

namespace NSync.Client
{
    public interface IUpdateManager
    {
        IObservable<UpdateInfo> CheckForUpdate();
        IObservable<Unit> ApplyReleases(IEnumerable<ReleaseEntry> releasesToApply);
    }

    public class UpdateManager : IEnableLogger, IUpdateManager
    {
        readonly IFileSystemFactory fileSystem;
        readonly string rootAppDirectory;
        readonly Func<string, IObservable<string>> downloadUrl;
        readonly string updateUrl;

        public UpdateManager(string url, 
            string applicationName,
            string rootDirectory = null,
            IFileSystemFactory fileSystem = null,
            Func<string, IObservable<string>> downloadUrl = null)
        {
            updateUrl = url;

            rootAppDirectory = Path.Combine(rootDirectory ?? getLocalAppDataDirectory(), applicationName);
            this.fileSystem = fileSystem ?? new AnonFileSystem(
                s => new DirectoryInfoWrapper(new DirectoryInfo(s)),
                s => new FileInfoWrapper(new FileInfo(s)),
                s => new FileWrapper(),
                s => new DirectoryInfoWrapper(new DirectoryInfo(s).CreateRecursive()),
                s => new DirectoryInfo(s).Delete(true));

            this.downloadUrl = downloadUrl;
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

            var ret = downloadUrl(updateUrl)
                .Select(ReleaseEntry.ParseReleaseFile)
                .Select(releases => determineUpdateInfo(localReleases, releases))
                .Multicast(new AsyncSubject<UpdateInfo>());

            ret.Connect();
            return ret;
        }

        void initializeClientAppDirectory()
        {
            var pkgDir = Path.Combine(rootAppDirectory, "packages");
            if (fileSystem.GetDirectoryInfo(pkgDir).Exists) {
                fileSystem.DeleteDirectoryRecursive(pkgDir);
            }
            fileSystem.CreateDirectoryRecursive(pkgDir);
        }

        public IObservable<Unit> ApplyReleases(IEnumerable<ReleaseEntry> releasesToApply)
        {
            foreach (var p in releasesToApply) {
                var file = p.Filename;
                // TODO: determine if we can use delta package
                // TODO: download optimal package
                // TODO: verify integrity of packages
                // TODO: apply package changes to destination

                // Q: is file in release relative path or can it support absolute path?
                // Q: have left destination parameter out of this call
                //      - shall NSync take care of the switching between current exe and new exe?
                //      - pondering how to do this right now
            }

            return Observable.Throw<Unit>(new NotImplementedException());
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