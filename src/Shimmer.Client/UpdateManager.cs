using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive.Threading.Tasks;
using NuGet;
using ReactiveUIMicro;
using Shimmer.Core;

// NB: These are whitelisted types from System.IO, so that we always end up 
// using fileSystem instead.
using FileAccess = System.IO.FileAccess;
using FileMode = System.IO.FileMode;
using MemoryStream = System.IO.MemoryStream;
using Path = System.IO.Path;
using StreamReader = System.IO.StreamReader;

namespace Shimmer.Client
{
    public sealed class UpdateManager : IUpdateManager, IEnableLogger
    {
        readonly IRxUIFullLogger log;
        readonly IFileSystemFactory fileSystem;
        readonly string rootAppDirectory;
        readonly string applicationName;
        readonly IUrlDownloader urlDownloader;
        readonly string updateUrlOrPath;
        readonly FrameworkVersion appFrameworkVersion;

        IDisposable updateLock;
        string id;

        public UpdateManager(string urlOrPath, 
            string applicationName,
            FrameworkVersion appFrameworkVersion,
            string rootDirectory = null,
            IFileSystemFactory fileSystem = null,
            IUrlDownloader urlDownloader = null)
        {
            Contract.Requires(!String.IsNullOrEmpty(urlOrPath));
            Contract.Requires(!String.IsNullOrEmpty(applicationName));

            log = LogManager.GetLogger<UpdateManager>();
            id = Guid.NewGuid().ToString();
            log.Info("Creating object with id {0}", id);

            updateUrlOrPath = urlOrPath;
            this.applicationName = applicationName;
            this.appFrameworkVersion = appFrameworkVersion;

            this.rootAppDirectory = Path.Combine(rootDirectory ?? getLocalAppDataDirectory(), applicationName);
            this.fileSystem = fileSystem ?? AnonFileSystem.Default;

            this.urlDownloader = urlDownloader ?? new DirectUrlDownloader(fileSystem);
        }

        public string PackageDirectory {
            get { return Path.Combine(rootAppDirectory, "packages"); }
        }

        public string LocalReleaseFile {
            get { return Path.Combine(PackageDirectory, "RELEASES"); }
        }

        public IObservable<UpdateInfo> CheckForUpdate(bool ignoreDeltaUpdates = false, IObserver<int> progress = null)
        {
            return acquireUpdateLock().SelectMany(_ => checkForUpdate(ignoreDeltaUpdates, progress));
        }

        IObservable<UpdateInfo> checkForUpdate(bool ignoreDeltaUpdates = false, IObserver<int> progress = null)
        {
            IEnumerable<ReleaseEntry> localReleases = Enumerable.Empty<ReleaseEntry>();
            progress = progress ?? new Subject<int>();

            try {
                var file = fileSystem.GetFileInfo(LocalReleaseFile).OpenRead();

                // NB: sr disposes file
                using (var sr = new StreamReader(file, Encoding.UTF8)) {
                    localReleases = ReleaseEntry.ParseReleaseFile(sr.ReadToEnd());
                }
            } catch (Exception ex) {
                // Something has gone wrong, we'll start from scratch.
                log.WarnException("Failed to load local release list", ex);
                initializeClientAppDirectory();
            }

            IObservable<string> releaseFile;

            // Fetch the remote RELEASES file, whether it's a local dir or an 
            // HTTP URL
            try {
                if (isHttpUrl(updateUrlOrPath)) {
                    this.Log().Info("Downloading RELEASES file from {0}", updateUrlOrPath);
                    releaseFile = urlDownloader.DownloadUrl(String.Format("{0}/{1}", updateUrlOrPath, "RELEASES"), progress);
                } else {
                    this.Log().Info("Reading RELEASES file from {0}", updateUrlOrPath);
                    var fi = fileSystem.GetFileInfo(Path.Combine(updateUrlOrPath, "RELEASES"));

                    using (var sr = new StreamReader(fi.OpenRead(), Encoding.UTF8)) {
                        var text = sr.ReadToEnd();
                        releaseFile = Observable.Return(text);
                    }

                    progress.OnNext(100);
                    progress.OnCompleted();
                }               
            } catch (Exception ex) {
                progress.OnCompleted();
                return Observable.Throw<UpdateInfo>(ex);
            }

            var ret = releaseFile
                .Select(ReleaseEntry.ParseReleaseFile)
                .SelectMany(releases => determineUpdateInfo(localReleases, releases, ignoreDeltaUpdates))
                .PublishLast();

            ret.Connect();
            return ret;
        }

        public IObservable<Unit> DownloadReleases(IEnumerable<ReleaseEntry> releasesToDownload, IObserver<int> progress = null)
        {
            return acquireUpdateLock().SelectMany(_ => downloadReleases(releasesToDownload, progress));
        }

        IObservable<Unit> downloadReleases(IEnumerable<ReleaseEntry> releasesToDownload, IObserver<int> progress = null)
        {
            progress = progress ?? new Subject<int>();
            IObservable<Unit> downloadResult = null;

            if (isHttpUrl(updateUrlOrPath)) {
                var urls = releasesToDownload.Select(x => String.Format("{0}/{1}", updateUrlOrPath, x.Filename));
                var paths = releasesToDownload.Select(x => Path.Combine(rootAppDirectory, "packages", x.Filename));

                downloadResult = urlDownloader.QueueBackgroundDownloads(urls, paths, progress);
            } else {
                var toIncrement = 100.0 / releasesToDownload.Count();

                // Do a parallel copy from the remote directory to the local
                var downloads = releasesToDownload.ToObservable()
                    .Select(x => fileSystem.CopyAsync(
                        Path.Combine(updateUrlOrPath, x.Filename),
                        Path.Combine(rootAppDirectory, "packages", x.Filename)))
                    .Merge(4)
                    .Publish();

                downloads
                    .Scan(0.0, (acc, _) => acc + toIncrement)
                    .Select(x => (int) x)
                    .Subscribe(progress);

                downloadResult = downloads.TakeLast(1);
                downloads.Connect();
            }

            return downloadResult.SelectMany(_ => checksumAllPackages(releasesToDownload));
        }

        public IObservable<List<string>> ApplyReleases(UpdateInfo updateInfo, IObserver<int> progress = null)
        {
            return acquireUpdateLock().SelectMany(_ => applyReleases(updateInfo, progress).ToObservable());
        }

        async Task<List<string>> applyReleases(UpdateInfo updateInfo, IObserver<int> progress = null)
        {
            progress = progress ?? new Subject<int>();

            // NB: It's important that we update the local releases file *only* 
            // once the entire operation has completed, even though we technically
            // could do it after DownloadUpdates finishes. We do this so that if
            // we get interrupted / killed during this operation, we'll start over

            var ret = default(List<string>);
            try {
                await cleanDeadVersions(updateInfo.CurrentlyInstalledVersion != null ? updateInfo.CurrentlyInstalledVersion.Version : null);
                progress.OnNext(10);

                var release = await createFullPackagesFromDeltas(updateInfo.ReleasesToApply, updateInfo.CurrentlyInstalledVersion);
                progress.OnNext(50);

                ret = await Observable.Start(() => installPackageToAppDir(updateInfo, release), RxApp.TaskpoolScheduler);
                progress.OnNext(95);

                await UpdateLocalReleasesFile();
                progress.OnNext(100);
            } finally {
                progress.OnCompleted();
            }
            
            return ret;
        }

        public IObservable<Unit> UpdateLocalReleasesFile()
        {
            return acquireUpdateLock().SelectMany(_ => Observable.Start(() => 
                ReleaseEntry.BuildReleasesFile(PackageDirectory, fileSystem), RxApp.TaskpoolScheduler));
        }

        public IObservable<Unit> FullUninstall()
        {
            return acquireUpdateLock().SelectMany(_ => fullUninstall());
        }

        IObservable<Unit> fullUninstall()
        {
            return Observable.Start(() => {
                cleanUpOldVersions(new Version(255, 255, 255, 255));

                try {
                    Utility.DeleteDirectory(rootAppDirectory);
                    return;
                } catch (Exception ex) {
                    log.WarnException("Full Uninstall tried to delete root dir but failed, punting until next reboot", ex);
                }
                
                Utility.DeleteDirectoryAtNextReboot(rootAppDirectory);
            }, RxApp.TaskpoolScheduler);
        }

        public void Dispose()
        {
            log.Info("Disposing object with id {0}", id);
            var disp = Interlocked.Exchange(ref updateLock, null);
            if (disp != null) {
                disp.Dispose();
            }
        }

        ~UpdateManager()
        {
            if (updateLock != null) {
                log.Info("Destructor hit with id {0} not disposed", id);
                throw new Exception("You must dispose UpdateManager!");
            }
        }

        IObservable<IDisposable> acquireUpdateLock()
        {
            if (updateLock != null) return Observable.Return(updateLock);

            return Observable.Start(() => {
                var key = Utility.CalculateStreamSHA1(new MemoryStream(Encoding.UTF8.GetBytes(rootAppDirectory)));

                IDisposable theLock;
                try {
                    theLock = RxApp.InUnitTestRunner() ?
                        Disposable.Empty : new SingleGlobalInstance(key, 2000);
                } catch (TimeoutException) {
                    throw new TimeoutException("Couldn't acquire update lock, another instance may be running updates");
                }

                var ret = Disposable.Create(() => {
                    theLock.Dispose();
                    updateLock = null;
                });

                updateLock = ret;
                return ret;
            });
        }

        static string getLocalAppDataDirectory()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        DirectoryInfoBase getDirectoryForRelease(Version releaseVersion)
        {
            return fileSystem.GetDirectoryInfo(Path.Combine(rootAppDirectory, "app-" + releaseVersion));
        }

        //
        // CheckForUpdate methods
        //

        void initializeClientAppDirectory()
        {
            // On bootstrap, we won't have any of our directories, create them
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
                log.Warn("Release information couldn't be determined due to remote corrupt RELEASES file");
                return Observable.Throw<UpdateInfo>(new Exception("Corrupt remote RELEASES file"));
            }

            if (localReleases.Count() == remoteReleases.Count()) {
                log.Info("No updates, remote and local are the same");
                return Observable.Return<UpdateInfo>(null);
            }

            if (ignoreDeltaUpdates) {
                remoteReleases = remoteReleases.Where(x => !x.IsDelta);
            }

            if (!localReleases.Any()) {
                log.Warn("First run or local directory is corrupt, starting from scratch");

                var latestFullRelease = findCurrentVersion(remoteReleases);
                return Observable.Return(UpdateInfo.Create(findCurrentVersion(localReleases), new[] {latestFullRelease}, PackageDirectory, appFrameworkVersion));
            }

            if (localReleases.Max(x => x.Version) >= remoteReleases.Max(x => x.Version)) {
                log.Warn("hwhat, local version is greater than remote version");

                var latestFullRelease = findCurrentVersion(remoteReleases);
                return Observable.Return(UpdateInfo.Create(findCurrentVersion(localReleases), new[] {latestFullRelease}, PackageDirectory, appFrameworkVersion));
            }

            return Observable.Return(UpdateInfo.Create(findCurrentVersion(localReleases), remoteReleases, PackageDirectory, appFrameworkVersion));
        }

        ReleaseEntry findCurrentVersion(IEnumerable<ReleaseEntry> localReleases)
        {
            if (!localReleases.Any()) {
                return null;
            }

            return localReleases.MaxBy(x => x.Version).SingleOrDefault(x => !x.IsDelta);
        }

        //
        // DownloadReleases methods
        //
        
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
            return releasesDownloaded
                .MapReduce(x => Observable.Start(() => checksumPackage(x)))
                .Select(_ => Unit.Default);
        }

        void checksumPackage(ReleaseEntry downloadedRelease)
        {
            var targetPackage = fileSystem.GetFileInfo(
                Path.Combine(rootAppDirectory, "packages", downloadedRelease.Filename));

            if (!targetPackage.Exists) {
                log.Error("File {0} should exist but doesn't", targetPackage.FullName);
                throw new Exception("Checksummed file doesn't exist: " + targetPackage.FullName);
            }

            if (targetPackage.Length != downloadedRelease.Filesize) {
                log.Error("File Length should be {0}, is {1}", downloadedRelease.Filesize, targetPackage.Length);
                targetPackage.Delete();
                throw new Exception("Checksummed file size doesn't match: " + targetPackage.FullName);
            }

            using (var file = targetPackage.OpenRead()) {
                var hash = Utility.CalculateStreamSHA1(file);
                if (!hash.Equals(downloadedRelease.SHA1,StringComparison.OrdinalIgnoreCase)) {
                    log.Error("File SHA1 should be {0}, is {1}", downloadedRelease.SHA1, hash);
                    targetPackage.Delete();
                    throw new Exception("Checksum doesn't match: " + targetPackage.FullName);
                }
            }
        }


        //
        // ApplyReleases methods
        //

        List<string> installPackageToAppDir(UpdateInfo updateInfo, ReleaseEntry release)
        {
            var pkg = new ZipPackage(Path.Combine(rootAppDirectory, "packages", release.Filename));
            var target = getDirectoryForRelease(release.Version);

            // NB: This might happen if we got killed partially through applying the release
            if (target.Exists) {
                Utility.DeleteDirectory(target.FullName);
            }
            target.Create();

            // Copy all of the files out of the lib/ dirs in the NuGet package
            // into our target App directory.
            //
            // NB: We sort this list in order to guarantee that if a Net20
            // and a Net40 version of a DLL get shipped, we always end up
            // with the 4.0 version.
            pkg.GetFiles().Where(x => pathIsInFrameworkProfile(x, appFrameworkVersion)).OrderBy(x => x.Path)
                .ForEach(x => {
                    var targetPath = Path.Combine(target.FullName, Path.GetFileName(x.Path));

                    var fi = fileSystem.GetFileInfo(targetPath);
                    if (fi.Exists) fi.Delete();

                    using (var inf = x.GetStream())
                    using (var of = fi.Open(FileMode.CreateNew, FileAccess.Write)) {
                        log.Info("Writing {0} to app directory", targetPath);
                        inf.CopyTo(of);
                    }
                });

            var newCurrentVersion = updateInfo.FutureReleaseEntry.Version;

            // Perform post-install; clean up the previous version by asking it
            // which shortcuts to install, and nuking them. Then, run the app's
            // post install and set up shortcuts.
            return runPostInstallAndCleanup(newCurrentVersion, updateInfo.IsBootstrapping);
        }

        List<string> runPostInstallAndCleanup(Version newCurrentVersion, bool isBootstrapping)
        {
            log.Debug(CultureInfo.InvariantCulture, "AppDomain ID: {0}", AppDomain.CurrentDomain.Id);

            fixPinnedExecutables(newCurrentVersion);

            var shortcutsToIgnore = cleanUpOldVersions(newCurrentVersion);
            var targetPath = getDirectoryForRelease(newCurrentVersion);

            return runPostInstallOnDirectory(targetPath.FullName, isBootstrapping, newCurrentVersion, shortcutsToIgnore);
        }

        List<string> runPostInstallOnDirectory(string newAppDirectoryRoot, bool isFirstInstall, Version newCurrentVersion, IEnumerable<ShortcutCreationRequest> shortcutRequestsToIgnore)
        {
            var postInstallInfo = new PostInstallInfo {
                NewAppDirectoryRoot = newAppDirectoryRoot,
                IsFirstInstall = isFirstInstall,
                NewCurrentVersion = newCurrentVersion,
                ShortcutRequestsToIgnore = shortcutRequestsToIgnore.ToArray()
            };

            var installerHooks = new InstallerHookOperations(log, fileSystem, applicationName);
            return AppDomainHelper.ExecuteInNewAppDomain(postInstallInfo, installerHooks.RunAppSetupInstallers).ToList();
        }

        static bool pathIsInFrameworkProfile(IPackageFile packageFile, FrameworkVersion appFrameworkVersion)
        {
            if (!packageFile.Path.StartsWith("lib", StringComparison.InvariantCultureIgnoreCase)) {
                return false;
            }

            if (appFrameworkVersion == FrameworkVersion.Net40 && packageFile.Path.StartsWith("lib\\net45", StringComparison.InvariantCultureIgnoreCase)) {
                return false;
            }

            if (packageFile.Path.StartsWith("lib\\winrt45", StringComparison.InvariantCultureIgnoreCase)) {
                return false;
            }

            return true;
        }

        IObservable<ReleaseEntry> createFullPackagesFromDeltas(IEnumerable<ReleaseEntry> releasesToApply, ReleaseEntry currentVersion)
        {
            Contract.Requires(releasesToApply != null);

            // If there are no deltas in our list, we're already done
            if (!releasesToApply.Any() || releasesToApply.All(x => !x.IsDelta)) {
                return Observable.Return(releasesToApply.MaxBy(x => x.Version).First());
            }

            if (!releasesToApply.All(x => x.IsDelta)) {
                return Observable.Throw<ReleaseEntry>(new Exception("Cannot apply combinations of delta and full packages"));
            }

            // Smash together our base full package and the nearest delta
            var ret = Observable.Start(() => {
                var basePkg = new ReleasePackage(Path.Combine(rootAppDirectory, "packages", currentVersion.Filename));
                var deltaPkg = new ReleasePackage(Path.Combine(rootAppDirectory, "packages", releasesToApply.First().Filename));

                var deltaBuilder = new DeltaPackageBuilder();

                return deltaBuilder.ApplyDeltaPackage(basePkg, deltaPkg,
                    Regex.Replace(deltaPkg.InputPackageFile, @"-delta.nupkg$", ".nupkg", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
            }, RxApp.TaskpoolScheduler);

            if (releasesToApply.Count() == 1) {
                return ret.Select(x => ReleaseEntry.GenerateFromFile(x.InputPackageFile));
            }

            return ret.SelectMany(x => {
                var fi = fileSystem.GetFileInfo(x.InputPackageFile);
                var entry = ReleaseEntry.GenerateFromFile(fi.OpenRead(), x.InputPackageFile);

                // Recursively combine the rest of them
                return createFullPackagesFromDeltas(releasesToApply.Skip(1), entry);
            });
        }

        IEnumerable<ShortcutCreationRequest> cleanUpOldVersions(Version newCurrentVersion)
        {
            var directory = fileSystem.GetDirectoryInfo(rootAppDirectory);
            if (!directory.Exists) {
                this.Log().Warn("The directory '{0}' does not exist", rootAppDirectory);
                return Enumerable.Empty<ShortcutCreationRequest>();
            }
            
            return directory.GetDirectories()
                .Where(x => x.Name.StartsWith("app-", StringComparison.InvariantCultureIgnoreCase))
                .Where(x => x.Name != "app-" + newCurrentVersion)
                .OrderBy(x => x.Name)
                .SelectMany(oldAppRoot => {
                    var path = oldAppRoot.FullName;
                    var installerHooks = new InstallerHookOperations(log, fileSystem, applicationName);

                    var ret = AppDomainHelper.ExecuteInNewAppDomain(path, installerHooks.RunAppSetupCleanups);

                    try {
                        Utility.DeleteDirectoryAtNextReboot(oldAppRoot.FullName);
                    } catch (Exception ex) {
                        log.WarnException("Couldn't delete old app directory on next reboot", ex);
                    }
                    return ret;
                });
        }

        void fixPinnedExecutables(Version newCurrentVersion) 
        {
            if (Environment.OSVersion.Version < new Version(6, 1)) {
                return;
            }

            var oldAppDirectories = fileSystem.GetDirectoryInfo(rootAppDirectory).GetDirectories()
                .Where(x => x.Name.StartsWith("app-", StringComparison.InvariantCultureIgnoreCase))
                .Where(x => x.Name != "app-" + newCurrentVersion)
                .Select(x => x.FullName)
                .ToArray();

            var newAppPath = Path.Combine(rootAppDirectory, "app-" + newCurrentVersion);

            var taskbarPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft\\Internet Explorer\\Quick Launch\\User Pinned\\TaskBar");

            foreach (var shortcut in fileSystem.GetDirectoryInfo(taskbarPath).GetFiles("*.lnk").Select(x => new ShellLink(x.FullName))) {
                foreach (var oldAppDirectory in oldAppDirectories) {
                    if (!shortcut.Target.StartsWith(oldAppDirectory, StringComparison.OrdinalIgnoreCase)) continue;

                    // replace old app path with new app path and check, if executable still exists
                    var newTarget = Path.Combine(newAppPath, shortcut.Target.Substring(oldAppDirectory.Length + 1));

                    if (fileSystem.GetFileInfo(newTarget).Exists) {
                        shortcut.Target = newTarget;

                        // replace working directory too if appropriate
                        if (shortcut.WorkingDirectory.StartsWith(oldAppDirectory, StringComparison.OrdinalIgnoreCase)) {
                            shortcut.WorkingDirectory = Path.Combine(newAppPath,
                                shortcut.WorkingDirectory.Substring(oldAppDirectory.Length + 1));
                        }

                        shortcut.Save();
                    } else {
                        TaskbarHelper.UnpinFromTaskbar(shortcut.Target);
                    }

                    break;
                }
            }
        }

        // NB: Once we uninstall the old version of the app, we try to schedule
        // it to be deleted at next reboot. Unfortunately, depending on whether
        // the user has admin permissions, this can fail. So as a failsafe,
        // before we try to apply any update, we assume previous versions in the
        // directory are "dead" (i.e. already uninstalled, but not deleted), and
        // we blow them away. This is to make sure that we don't attempt to run
        // an uninstaller on an already-uninstalled version.
        IObservable<Unit> cleanDeadVersions(Version currentVersion)
        {
            var di = fileSystem.GetDirectoryInfo(rootAppDirectory);

            // NB: If we try to access a directory that has already been 
            // scheduled for deletion by MoveFileEx it throws what seems like
            // NT's only error code, ERROR_ACCESS_DENIED. Squelch errors that
            // come from here.
            return di.GetDirectories().ToObservable()
                .Where(x => x.Name.ToLowerInvariant().Contains("app-"))
                .Where(x => currentVersion != null ? x.Name != getDirectoryForRelease(currentVersion).Name : true)
                .MapReduce(x => Observable.Start(() => Utility.DeleteDirectory(x.FullName), RxApp.TaskpoolScheduler)
                    .LoggedCatch<Unit, UpdateManager, UnauthorizedAccessException>(this, _ => Observable.Return(Unit.Default)))
                .Aggregate(Unit.Default, (acc, x) => acc);
        }
    }

    public class DidntFollowInstructionsAppSetup : AppSetup
    {
        readonly string shortCutName;
        public override string ShortcutName {
            get { return shortCutName; }
        }

        readonly string target;
        public override string Target { get { return target; } }

        public DidntFollowInstructionsAppSetup(string exeFile)
        {
            var fvi = FileVersionInfo.GetVersionInfo(exeFile);
            shortCutName = fvi.ProductName ?? fvi.FileDescription ?? fvi.FileName.Replace(".exe", "");
            target = exeFile;
        }
    }
}