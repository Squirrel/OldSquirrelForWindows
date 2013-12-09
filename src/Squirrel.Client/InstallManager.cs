using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using NuGet;
using ReactiveUIMicro;
using Squirrel.Client.Extensions;
using Squirrel.Core;

namespace Squirrel.Client
{
    public interface IInstallManager : IEnableLogger
    {
        IObservable<List<string>> ExecuteInstall(string currentAssemblyDir, IPackage bundledPackageMetadata, IObserver<int> progress = null);
        IObservable<Unit> ExecuteUninstall(Version version);
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
            // Squirrel update directory (a RELEASES file and the corresponding 
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

            var updateUsingDeltas =
                executeInstall(currentAssemblyDir, bundledPackageMetadata, progress: progress)
                        .ToObservable()
                        .ObserveOn(RxApp.DeferredScheduler)
                        .Catch<List<string>, Exception>(ex => {
                    log.WarnException("Updating using deltas has failed", ex);
                    return executeInstall(currentAssemblyDir, bundledPackageMetadata, true, progress)
                                 .ToObservable();
            });

            return updateUsingDeltas;
        }

        Task<List<string>> executeInstall(
            string currentAssemblyDir,
            IPackage bundledPackageMetadata,
            bool ignoreDeltaUpdates = false,
            IObserver<int> progress = null)
        {
            var fxVersion = bundledPackageMetadata.DetectFrameworkVersion();

            var eigenCheckProgress = new Subject<int>();
            var eigenCopyFileProgress = new Subject<int>();
            var eigenApplyProgress = new Subject<int>();

            var realCheckProgress = new Subject<int>();
            var realCopyFileProgress = new Subject<int>();
            var realApplyProgress = new Subject<int>();

            var eigenUpdateObs = Observable.Using(() => new UpdateManager(currentAssemblyDir, bundledPackageMetadata.Id, fxVersion, TargetRootDirectory), eigenUpdater => {
                // The real update takes longer than the eigenupdate because we're
                // downloading from the Internet instead of doing everything 
                // locally, so give it more weight
                Observable.Concat(
                    Observable.Concat(eigenCheckProgress, eigenCopyFileProgress, eigenCopyFileProgress)
                        .Select(x => (x/3.0)*0.33),
                    Observable.Concat(realCheckProgress, realCopyFileProgress, realApplyProgress)
                        .Select(x => (x/3.0)*0.67))
                    .Select(x => (int) x)
                    .Subscribe(progress);

                var updateInfoObs = eigenUpdater.CheckForUpdate(ignoreDeltaUpdates, eigenCheckProgress);

                return updateInfoObs.SelectMany(updateInfo => {
                    log.Info("The checking of releases completed - and there was much rejoicing");

                    if (!updateInfo.ReleasesToApply.Any()) {

                        var rootDirectory = TargetRootDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                        var version = updateInfo.CurrentlyInstalledVersion;
                        var releaseFolder = String.Format("app-{0}", version.Version);
                        var absoluteFolder = Path.Combine(rootDirectory, version.PackageName, releaseFolder);

                        if (!Directory.Exists(absoluteFolder)) {
                            log.Warn("executeInstall: the directory {0} doesn't exist - cannot find the current app?!!?");
                        } else {
                            return Observable.Return(
                                Directory.GetFiles(absoluteFolder, "*.exe", SearchOption.TopDirectoryOnly).ToList());
                        }
                    }

                    foreach (var u in updateInfo.ReleasesToApply) {
                        log.Info("HEY! We should be applying update {0}", u.Filename);
                    }

                    return eigenUpdater.DownloadReleases(updateInfo.ReleasesToApply, eigenCopyFileProgress)
                        .Do(_ => log.Info("The downloading of releases completed - and there was much rejoicing"))
                        .SelectMany(_ => eigenUpdater.ApplyReleases(updateInfo, eigenApplyProgress))
                        .Do(_ => log.Info("The applying of releases completed - and there was much rejoicing"));
                });
            });

            return eigenUpdateObs.SelectMany(ret => {
                var updateUrl = bundledPackageMetadata.ProjectUrl != null ? bundledPackageMetadata.ProjectUrl.ToString() : null;
                updateUrl = null; //XXX REMOVE ME
                if (updateUrl == null) {
                    realCheckProgress.OnNext(100); realCheckProgress.OnCompleted();
                    realCopyFileProgress.OnNext(100); realCopyFileProgress.OnCompleted();
                    realApplyProgress.OnNext(100); realApplyProgress.OnCompleted();

                    return Observable.Return(ret);
                }

                return Observable.Using(() => new UpdateManager(updateUrl, bundledPackageMetadata.Id, fxVersion, TargetRootDirectory), realUpdater => {
                    return realUpdater.CheckForUpdate(progress: realCheckProgress)
                        .SelectMany(x => realUpdater.DownloadReleases(x.ReleasesToApply, realCopyFileProgress).Select(_ => x))
                        .SelectMany(x => realUpdater.ApplyReleases(x, realApplyProgress))
                        .Select(_ => ret)
                        .LoggedCatch(this, Observable.Return(new List<string>()), "Failed to update to latest remote version");
                });
            }).ToTask();
        }

        public IObservable<Unit> ExecuteUninstall(Version version = null)
        {
            var updateManager = new UpdateManager("http://lol", BundledRelease.PackageName, FrameworkVersion.Net40, TargetRootDirectory);

            return updateManager.FullUninstall(version)
                .ObserveOn(RxApp.DeferredScheduler)
                .Log(this, "Full uninstall")
                .Finally(updateManager.Dispose);
        }
    }
}
