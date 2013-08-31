using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using NuGet;
using ReactiveUIMicro;
using Shimmer.Core;

namespace Shimmer.Client
{
    public interface IInstallManager
    {
        IObservable<List<string>> ExecuteInstall(string currentAssemblyDir, IPackage bundledPackageMetadata, IObserver<int> progress = null);
        IObservable<Unit> ExecuteUninstall();
    }

    public class InstallManager : IInstallManager
    {
        public ReleaseEntry BundledRelease { get; protected set; }
        public string TargetRootDirectory { get; protected set; }
        readonly IRxUIFullLogger log;

        public InstallManager(ReleaseEntry bundledRelease, string targetRootDirectory = null)
        {
            BundledRelease = bundledRelease;
            TargetRootDirectory = targetRootDirectory;
            log = LogManager.GetLogger<InstallManager>();
        }

        public IObservable<List<string>> ExecuteInstall(string currentAssemblyDir, IPackage bundledPackageMetadata, IObserver<int> progress = null)
        {
            progress = progress ?? new Subject<int>();

            // NB: This bit of code is a bit clever. The binaries that WiX 
            // has installed *itself* meets the qualifications for being a
            // Shimmer update directory (a RELEASES file and the corresponding 
            // NuGet packages). 
            //
            // So, in order to reuse some code and not write the same things 
            // twice we're going to "Eigenupdate" from our own directory; 
            // UpdateManager will operate in bootstrap mode and create a 
            // local directory for us. 
            //
            // Then, we create a *new* UpdateManager whose target is the normal 
            // update URL - we can then apply delta updates against the bundled 
            // NuGet package to get up to vCurrent. The reason we go through
            // this rigamarole is so that developers don't have to rebuild the 
            // installer as often (never, technically).

            return executeInstall(currentAssemblyDir, bundledPackageMetadata, progress)
                .ToObservable()
                .ObserveOn(RxApp.DeferredScheduler);
        }

        async Task<List<string>> executeInstall(string currentAssemblyDir, IPackage bundledPackageMetadata, IObserver<int> progress = null)
        {
            var fxVersion = determineFxVersionFromPackage(bundledPackageMetadata);
            var eigenUpdater = new UpdateManager(currentAssemblyDir, bundledPackageMetadata.Id, fxVersion, TargetRootDirectory);

            var eigenCheckProgress = new Subject<int>();
            var eigenCopyFileProgress = new Subject<int>();
            var eigenApplyProgress = new Subject<int>();

            var realCheckProgress = new Subject<int>();
            var realCopyFileProgress = new Subject<int>();
            var realApplyProgress = new Subject<int>();

            // The real update takes longer than the eigenupdate because we're
            // downloading from the Internet instead of doing everything 
            // locally, so give it more weight
            Observable.Concat(
                    Observable.Concat(eigenCheckProgress, eigenCopyFileProgress, eigenCopyFileProgress).Select(x => (x / 3.0) * 0.33),
                    Observable.Concat(realCheckProgress, realCopyFileProgress, realApplyProgress).Select(x => (x / 3.0) * 0.67))
                .Select(x => (int)x)
                .Subscribe(progress);

            List<string> ret = null;
            var updateInfo = await eigenUpdater.CheckForUpdate(progress: eigenCheckProgress);

            foreach (var u in updateInfo.ReleasesToApply) {
                log.Info("HEY! We should be applying update {0}", u.Filename);
            }

            await eigenUpdater.DownloadReleases(updateInfo.ReleasesToApply, eigenCopyFileProgress);
            ret = await eigenUpdater.ApplyReleases(updateInfo, eigenApplyProgress);

            eigenUpdater.Dispose();

            var updateUrl = bundledPackageMetadata.ProjectUrl != null ? bundledPackageMetadata.ProjectUrl.ToString() : null;
            updateUrl = null; //XXX REMOVE ME
            if (updateUrl == null) {
                realCheckProgress.OnNext(100); realCheckProgress.OnCompleted();
                realCopyFileProgress.OnNext(100); realCopyFileProgress.OnCompleted();
                realApplyProgress.OnNext(100); realApplyProgress.OnCompleted();

                return ret;
            }

            var realUpdater = new UpdateManager(updateUrl, bundledPackageMetadata.Id, fxVersion, TargetRootDirectory);

            try {
                updateInfo = await realUpdater.CheckForUpdate(progress: realCheckProgress);
                await realUpdater.DownloadReleases(updateInfo.ReleasesToApply, realCopyFileProgress);
                await realUpdater.ApplyReleases(updateInfo, realApplyProgress);
            } catch (Exception ex) {
                log.ErrorException("Failed to update to latest remote version", ex);
                return new List<string>();
            }

            realUpdater.Dispose();

            return ret;
        }

        public IObservable<Unit> ExecuteUninstall()
        {
            var updateManager = new UpdateManager("http://lol", BundledRelease.PackageName, FrameworkVersion.Net40, TargetRootDirectory);

            return updateManager.FullUninstall()
                .ObserveOn(RxApp.DeferredScheduler)
                .Log(this, "Full uninstall")
                .Finally(updateManager.Dispose);
        }

        static FrameworkVersion determineFxVersionFromPackage(IPackage package)
        {
            return package.GetFiles().Any(x => x.Path.Contains("lib") && x.Path.Contains("45"))
                ? FrameworkVersion.Net45
                : FrameworkVersion.Net40;
        }
    }
}
