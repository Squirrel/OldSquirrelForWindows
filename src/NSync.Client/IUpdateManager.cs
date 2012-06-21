using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NSync.Core;

namespace NSync.Client
{
    public interface IUpdateManager
    {
        IDisposable AcquireUpdateLock();
        IObservable<UpdateInfo> CheckForUpdate(bool ignoreDeltaUpdates = false);
        IObservable<Unit> DownloadReleases(IEnumerable<ReleaseEntry> releasesToDownload);
        IObservable<Unit> ApplyReleases(UpdateInfo updateInfo);
    }

    public static class UpdateManagerMixins
    {
        public static IObservable<ReleaseEntry> UpdateApp(this IUpdateManager This)
        {
            IDisposable theLock;

            try {
                theLock = This.AcquireUpdateLock();
            } catch (TimeoutException _) {
                // TODO: Bad Programmer!
                return Observable.Return(default(ReleaseEntry));
            } catch (Exception ex) {
                return Observable.Throw<ReleaseEntry>(ex);
            }

            var ret = This.CheckForUpdate()
                .SelectMany(x => This.DownloadReleases(x.ReleasesToApply).Select(_ => x))
                .SelectMany(x => This.ApplyReleases(x).Select(_ => x.ReleasesToApply.MaxBy(y => y.Version).FirstOrDefault()))
                .Finally(() => theLock.Dispose())
                .Multicast(new AsyncSubject<ReleaseEntry>());

            ret.Connect();
            return ret;
        }
    }
}