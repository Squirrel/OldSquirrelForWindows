using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Squirrel.Core;

namespace Squirrel.Client
{
    [ContractClass(typeof(UpdateManagerContracts))]
    public interface IUpdateManager : IDisposable
    {
        /// <summary>
        /// Fetch the remote store for updates and compare against the current 
        /// version to determine what updates to download.
        /// </summary>
        /// <param name="ignoreDeltaUpdates">Set this flag if applying a release
        /// fails to fall back to a full release, which takes longer to download
        /// but is less error-prone.</param>
        /// <param name="progress">A Observer which can be used to report Progress - 
        /// will return values from 0-100 and Complete, or Throw</param>
        /// <returns>An UpdateInfo object representing the updates to install.
        /// </returns>
        IObservable<UpdateInfo> CheckForUpdate(bool ignoreDeltaUpdates, IObserver<int> progress);

        /// <summary>
        /// Download a list of releases into the local package directory.
        /// </summary>
        /// <param name="releasesToDownload">The list of releases to download, 
        /// almost always from UpdateInfo.ReleasesToApply.</param>
        /// <param name="progress">A Observer which can be used to report Progress - 
        /// will return values from 0-100 and Complete, or Throw</param>
        /// <returns>A completion Observable - either returns a single 
        /// Unit.Default then Complete, or Throw</returns>
        IObservable<Unit> DownloadReleases(IEnumerable<ReleaseEntry> releasesToDownload, IObserver<int> progress);

        /// <summary>
        /// Take an already downloaded set of releases and apply them, 
        /// copying in the new files from the NuGet package and rewriting 
        /// the application shortcuts.
        /// </summary>
        /// <param name="updateInfo">The UpdateInfo instance acquired from 
        /// CheckForUpdate</param>
        /// <param name="progress">A Observer which can be used to report Progress - 
        /// will return values from 0-100 and Complete, or Throw</param>
        /// <returns>A list of EXEs that should be started if this is a new 
        /// installation.</returns>
        IObservable<List<string>> ApplyReleases(UpdateInfo updateInfo, IObserver<int> progress);
    }

    [ContractClassFor(typeof(IUpdateManager))]
    public abstract class UpdateManagerContracts : IUpdateManager
    {
        public IDisposable AcquireUpdateLock()
        {
            return default(IDisposable);
        }

        public IObservable<UpdateInfo> CheckForUpdate(bool ignoreDeltaUpdates = false, IObserver<int> progress = null)
        {
            return default(IObservable<UpdateInfo>);
        }

        public IObservable<Unit> DownloadReleases(IEnumerable<ReleaseEntry> releasesToDownload, IObserver<int> progress = null)
        {
            // XXX: Why doesn't this work?
            Contract.Requires(releasesToDownload != null);
            Contract.Requires(releasesToDownload.Any());
            return default(IObservable<Unit>);
        }

        public IObservable<List<string>> ApplyReleases(UpdateInfo updateInfo, IObserver<int> progress = null)
        {
            Contract.Requires(updateInfo != null);
            return default(IObservable<List<string>>);
        }

        public void Dispose()
        {
        }
    }

    public static class RxHatersMixin
    {
        public static Task<ReleaseEntry> UpdateAppAsync(this IUpdateManager This, Action<int> progress)
        {
            var checkSubj = new Subject<int>();
            var downloadSubj = new Subject<int>();
            var applySubj = new Subject<int>();

            var ret = This.CheckForUpdate(false, checkSubj)
                .SelectMany(x => This.DownloadReleases(x.ReleasesToApply, downloadSubj).TakeLast(1).Select(_ => x))
                .SelectMany(x => This.ApplyReleases(x, applySubj).TakeLast(1).Select(_ => x.ReleasesToApply.MaxBy(y => y.Version).LastOrDefault()))
                .PublishLast();

            var allProgress = Observable.Merge(
                    checkSubj.Select(x => (double)x / 3.0),
                    downloadSubj.Select(x => (double)x / 3.0),
                    applySubj.Select(x => (double)x / 3.0))
                .Scan(0.0, (acc, x) => acc + x);

            allProgress.Subscribe(x => progress((int) x));

            ret.Connect();
            return ret.ToTask();
        }

        /// <summary>
        /// Fetch the remote store for updates and compare against the current 
        /// version to determine what updates to download.
        /// </summary>
        /// <param name="ignoreDeltaUpdates">Set this flag if applying a release
        /// fails to fall back to a full release, which takes longer to download
        /// but is less error-prone.</param>
        /// <param name="progress">An Action which can be used to report Progress - 
        /// will return values from 0-100</param>
        /// <returns>An UpdateInfo object representing the updates to install.
        /// </returns>
        public static Task<UpdateInfo> CheckForUpdateAsync(this IUpdateManager This, bool ignoreDeltaUpdates, Action<int> progress)
        {
            var subj = new Subject<int>();
            subj.Subscribe(progress);
            return This.CheckForUpdate(ignoreDeltaUpdates, subj).ToTask();
        }

        /// <summary>
        /// Download a list of releases into the local package directory.
        /// </summary>
        /// <param name="releasesToDownload">The list of releases to download, 
        /// almost always from UpdateInfo.ReleasesToApply.</param>
        /// <param name="progress">An Action which can be used to report Progress - 
        /// will return values from 0-100</param>
        /// <returns>A completion Task<returns>
        public static Task DownloadReleasesAsync(this IUpdateManager This, IEnumerable<ReleaseEntry> releasesToDownload, Action<int> progress)
        {
            var subj = new Subject<int>();
            subj.Subscribe(progress, ex => { });

            return This.DownloadReleases(releasesToDownload, subj).ToTask();
        }

        /// <summary>
        /// Take an already downloaded set of releases and apply them, 
        /// copying in the new files from the NuGet package and rewriting 
        /// the application shortcuts.
        /// </summary>
        /// <param name="updateInfo">The UpdateInfo instance acquired from 
        /// CheckForUpdate</param>
        /// <param name="progress">An Action which can be used to report Progress - 
        /// will return values from 0-100</param>
        /// <returns>A list of EXEs that should be started if this is a new 
        /// installation.</returns>
        public static Task<List<string>> ApplyReleasesAsync(this IUpdateManager This, UpdateInfo updateInfo, Action<int> progress)
        {
            var subj = new Subject<int>();
            subj.Subscribe(progress, ex => { });
            return This.ApplyReleases(updateInfo, subj).ToTask();
        }
    }
}
