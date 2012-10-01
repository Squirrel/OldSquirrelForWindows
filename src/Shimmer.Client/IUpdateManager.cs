using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Shimmer.Core;

namespace Shimmer.Client
{
    [ContractClass(typeof(UpdateManagerContracts))]
    public interface IUpdateManager
    {
        /// <summary>
        /// Acquire the global lock to start updating. Call this method before
        /// calling any of the other methods in this interface. Note that this
        /// lock should be *global* (i.e. it should be a named Mutex that spans
        /// processes), so that multiple instances don't try to operate on the
        /// packages directory concurrently.
        /// </summary>
        /// <returns>A Disposable that will release the lock, or the method throws
        /// if the lock cannot be acquired.</returns>
        IDisposable AcquireUpdateLock();

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
        IObservable<UpdateInfo> CheckForUpdate(bool ignoreDeltaUpdates = false, IObserver<int> progress = null);

        /// <summary>
        /// Download a list of releases into the local package directory.
        /// </summary>
        /// <param name="releasesToDownload">The list of releases to download, 
        /// almost always from UpdateInfo.ReleasesToApply.</param>
        /// <param name="progress">A Observer which can be used to report Progress - 
        /// will return values from 0-100 and Complete, or Throw</param>
        /// <returns>A completion Observable - either returns a single 
        /// Unit.Default then Complete, or Throw</returns>
        IObservable<Unit> DownloadReleases(IEnumerable<ReleaseEntry> releasesToDownload, IObserver<int> progress = null);

        /// <summary>
        /// Take an already downloaded set of releases and apply them, 
        /// copying in the new files from the NuGet package and rewriting 
        /// the application shortcuts.
        /// </summary>
        /// <param name="updateInfo">The UpdateInfo instance acquired from 
        /// CheckForUpdate</param>
        /// <param name="progress">A Observer which can be used to report Progress - 
        /// will return values from 0-100 and Complete, or Throw</param>
        /// <returns>A progress Observable - will return values from 0-100 and
        /// Complete, or Throw</returns>
        IObservable<Unit> ApplyReleases(UpdateInfo updateInfo, IObserver<int> progress = null);
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

        public IObservable<Unit> ApplyReleases(UpdateInfo updateInfo, IObserver<int> progress = null)
        {
            Contract.Requires(updateInfo != null);
            return default(IObservable<Unit>);
        }
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
                .SelectMany(x => This.DownloadReleases(x.ReleasesToApply).TakeLast(1).Select(_ => x))
                .SelectMany(x => This.ApplyReleases(x).TakeLast(1).Select(_ => x.ReleasesToApply.MaxBy(y => y.Version).LastOrDefault()))
                .Finally(() => theLock.Dispose())
                .Multicast(new AsyncSubject<ReleaseEntry>());

            ret.Connect();
            return ret;
        }
    }
}