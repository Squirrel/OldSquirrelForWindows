using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using NSync.Core;

namespace NSync.Client
{
    public class UpdateInfo
    {
        public Version Version { get; protected set; }
        public ReleaseEntry CurrentlyInstalledVersion { get; protected set; }
        public ReleaseEntry FutureReleaseEntry { get; protected set; }
        public IEnumerable<ReleaseEntry> ReleasesToApply { get; protected set; }

        readonly string packageDirectory;

        protected UpdateInfo(ReleaseEntry currentlyInstalledVersion, IEnumerable<ReleaseEntry> releasesToApply, string packageDirectory)
        {
            // NB: When bootstrapping, CurrentlyInstalledVersion is null!
            CurrentlyInstalledVersion = currentlyInstalledVersion;
            Version = currentlyInstalledVersion != null ? currentlyInstalledVersion.Version : null;
            ReleasesToApply = releasesToApply ?? Enumerable.Empty<ReleaseEntry>();
            FutureReleaseEntry = ReleasesToApply.MaxBy(x => x.Version).FirstOrDefault();

            this.packageDirectory = packageDirectory;
        }

        public Dictionary<ReleaseEntry, string> FetchReleaseNotes()
        {
            return ReleasesToApply
                .Select(x => new { Entry = x, Readme = x.GetReleaseNotes(packageDirectory) })
                .ToDictionary(k => k.Entry, v => v.Readme);
        }

        public static UpdateInfo Create(ReleaseEntry currentVersion, IEnumerable<ReleaseEntry> availableReleases, string packageDirectory)
        {
            Contract.Requires(availableReleases != null);
            Contract.Requires(!String.IsNullOrEmpty(packageDirectory));

            var latestFull = availableReleases.MaxBy(x => x.Version).FirstOrDefault(x => !x.IsDelta);
            if (latestFull == null) {
                throw new Exception("There should always be at least one full release");
            }

            if (currentVersion == null) {
                return new UpdateInfo(currentVersion, new[] { latestFull }, packageDirectory);
            }

            var newerThanUs = availableReleases.Where(x => x.Version > currentVersion.Version);
            var deltasSize = newerThanUs.Where(x => x.IsDelta).Sum(x => x.Filesize);

            return (deltasSize < latestFull.Filesize && deltasSize > 0)
                ? new UpdateInfo(currentVersion, newerThanUs.Where(x => x.IsDelta).ToArray(), packageDirectory)
                : new UpdateInfo(currentVersion, new[] { latestFull }, packageDirectory);
        }
    }
}