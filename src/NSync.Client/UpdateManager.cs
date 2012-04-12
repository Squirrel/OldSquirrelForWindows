using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NSync.Core;

namespace NSync.Client
{
    public class UpdateManager
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
                return null;
            }
        }
    }

    public class UpdateInfo
    {
        public string Version { get; protected set; }
    }
}