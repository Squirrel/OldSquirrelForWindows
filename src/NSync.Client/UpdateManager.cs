using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NSync.Core;

namespace NSync.Client
{
    public class UpdateManager : IEnableLogger
    {
        Func<string, Stream> openPath;
        Func<string, IObservable<string>> downloadUrl;
        string updateUrl;

        public UpdateManager(string url, 
            Func<string, Stream> openPathMock = null,
            Func<string, IObservable<string>> downloadUrlMock = null)
        {
            updateUrl = url;
            openPath = openPathMock;
            downloadUrl = downloadUrlMock;
        }

        public IObservable<UpdateInfo> CheckForUpdate()
        {
            IEnumerable<ReleaseEntry> localReleases;

            using (var sr = new StreamReader(openPath(Path.Combine("packages", "RELEASES")))) {
                localReleases = ReleaseEntry.ParseReleaseFile(sr.ReadToEnd());
            }

            var ret = downloadUrl(updateUrl)
                .Select(ReleaseEntry.ParseReleaseFile)
                .Select(releases => determineUpdateInfo(localReleases, releases))
                .Multicast(new AsyncSubject<UpdateInfo>());

            ret.Connect();
            return ret;
        }

        UpdateInfo determineUpdateInfo(IEnumerable<ReleaseEntry> localReleases, IEnumerable<ReleaseEntry> remoteReleases)
        {
            if (localReleases.Count() == remoteReleases.Count()) {
                this.Log().Info("No updates, remote and local are the same");
                return null;
            }

            if (localReleases.Max(x => x.Version) >= remoteReleases.Max(x => x.Version)) {
                this.Log().Warn("hwhat, local version is greater than remote version");
                return null;
            }

            return UpdateInfo.Create(findCurrentVersion(localReleases), remoteReleases);
        }

        ReleaseEntry findCurrentVersion(IEnumerable<ReleaseEntry> localReleases)
        {
            return localReleases.MaxBy(x => x.Version).Single(x => !x.IsDelta);
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