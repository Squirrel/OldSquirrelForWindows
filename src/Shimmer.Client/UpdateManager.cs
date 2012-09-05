using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NuGet;
using ReactiveUI;
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
    [Serializable]
    public class UpdateManager : IEnableLogger, IUpdateManager
    {
        readonly IFileSystemFactory fileSystem;
        readonly string rootAppDirectory;
        readonly string applicationName;
        readonly IUrlDownloader urlDownloader;
        readonly string updateUrlOrPath;
        readonly FrameworkVersion appFrameworkVersion;

        bool hasUpdateLock;

        public UpdateManager(string urlOrPath, 
            string applicationName,
            FrameworkVersion appFrameworkVersion,
            string rootDirectory = null,
            IFileSystemFactory fileSystem = null,
            IUrlDownloader urlDownloader = null)
        {
            Contract.Requires(!String.IsNullOrEmpty(urlOrPath));
            Contract.Requires(!String.IsNullOrEmpty(applicationName));

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

        public IObservable<UpdateInfo> CheckForUpdate(bool ignoreDeltaUpdates = false)
        {
            IEnumerable<ReleaseEntry> localReleases = Enumerable.Empty<ReleaseEntry>();

            var noLock = checkForLock<UpdateInfo>();
            if (noLock != null) return noLock;

            try {
                var file = fileSystem.GetFileInfo(LocalReleaseFile).OpenRead();

                // NB: sr disposes file
                using (var sr = new StreamReader(file, Encoding.UTF8)) {
                    localReleases = ReleaseEntry.ParseReleaseFile(sr.ReadToEnd());
                }
            } catch (Exception ex) {
                // Something has gone wrong, we'll start from scratch.
                this.Log().WarnException("Failed to load local release list", ex);
                initializeClientAppDirectory();
            }

            IObservable<string> releaseFile;

            // Fetch the remote RELEASES file, whether it's a local dir or an 
            // HTTP URL
            if (isHttpUrl(updateUrlOrPath)) {
                releaseFile = urlDownloader.DownloadUrl(String.Format("{0}/{1}", updateUrlOrPath, "RELEASES"));
            } else {
                var fi = fileSystem.GetFileInfo(Path.Combine(updateUrlOrPath, "RELEASES"));

                using (var sr = new StreamReader(fi.OpenRead(), Encoding.UTF8)) {
                    var text = sr.ReadToEnd();
                    releaseFile = Observable.Return(text);
                }
            }

            var ret = releaseFile
                .Select(ReleaseEntry.ParseReleaseFile)
                .SelectMany(releases => determineUpdateInfo(localReleases, releases, ignoreDeltaUpdates))
                .Multicast(new AsyncSubject<UpdateInfo>());

            ret.Connect();
            return ret;
        }

        public IObservable<int> DownloadReleases(IEnumerable<ReleaseEntry> releasesToDownload)
        {
            var noLock = checkForLock<int>();
            if (noLock != null) return noLock;

            IObservable<int> downloadResult;

            if (isHttpUrl(updateUrlOrPath)) {
                var urls = releasesToDownload.Select(x => String.Format("{0}/{1}", updateUrlOrPath, x.Filename));
                var paths = releasesToDownload.Select(x => Path.Combine(rootAppDirectory, "packages", x.Filename));

                downloadResult = urlDownloader.QueueBackgroundDownloads(urls, paths);
            } else {
                var toIncrement = 100.0 / releasesToDownload.Count();

                // Do a parallel copy from the remote directory to the local
                downloadResult = releasesToDownload.ToObservable()
                    .Select(x => fileSystem.CopyAsync(
                        Path.Combine(updateUrlOrPath, x.Filename),
                        Path.Combine(rootAppDirectory, "packages", x.Filename)))
                    .Merge(4)
                    .Scan(0.0, (acc, _) => acc + toIncrement)
                    .Select(x => (int)x);
            }

            return downloadResult
                .Select(x => (int)(x * 0.95))
                .Concat(checksumAllPackages(releasesToDownload).Select(_ => 100));
        }

        public IObservable<int> ApplyReleases(UpdateInfo updateInfo)
        {
            var noLock = checkForLock<int>();
            if (noLock != null) return noLock;

            // NB: This is so gross, but reporting accurate progress here is 
            // just too damn hard.
            var ret = new ReplaySubject<int>();

            // NB: It's important that we update the local releases file *only* 
            // once the entire operation has completed, even though we technically
            // could do it after DownloadUpdates finishes. We do this so that if
            // we get interrupted / killed during this operation, we'll start over
            cleanDeadVersions(updateInfo.CurrentlyInstalledVersion != null ? updateInfo.CurrentlyInstalledVersion.Version : null)
                .Do(_ => ret.OnNext(10), ret.OnError)
                .SelectMany(_ => createFullPackagesFromDeltas(updateInfo.ReleasesToApply, updateInfo.CurrentlyInstalledVersion))
                .Do(_ => ret.OnNext(50), ret.OnError)
                .SelectMany(release =>
                    Observable.Start(() => installPackageToAppDir(updateInfo, release), RxApp.TaskpoolScheduler))
                .Do(_ => ret.OnNext(95), ret.OnError)
                .SelectMany(_ => UpdateLocalReleasesFile())
                .Subscribe(_ => ret.OnNext(100), ret.OnError, ret.OnCompleted);

            return ret;
        }

        public IDisposable AcquireUpdateLock()
        {
            var key = Utility.CalculateStreamSHA1(new MemoryStream(Encoding.UTF8.GetBytes(rootAppDirectory)));
            var ret = new SingleGlobalInstance(key, 500);

            hasUpdateLock = true;
            return Disposable.Create(() => {
                ret.Dispose();
                hasUpdateLock = false;
            });
        }

        public IObservable<Unit> UpdateLocalReleasesFile()
        {
            var noLock = checkForLock<Unit>();
            if (noLock != null) return noLock;

            return Observable.Start(() => 
                ReleaseEntry.BuildReleasesFile(PackageDirectory, fileSystem), RxApp.TaskpoolScheduler);
        }

        public IObservable<Unit> FullUninstall()
        {
            var noLock = checkForLock<Unit>();
            if (noLock != null) return noLock;

            return Observable.Start(() => {
                cleanUpOldVersions(new Version(255, 255, 255, 255));
                Utility.DeleteDirectoryAtNextReboot(rootAppDirectory);
            }, RxApp.TaskpoolScheduler);
        }

        IObservable<TRet> checkForLock<TRet>()
        {
            if (!hasUpdateLock) {
                return Observable.Throw<TRet>(new Exception("Call AcquireUpdateLock before using this method"));
            }

            return null;
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
                this.Log().Warn("Release information couldn't be determined due to remote corrupt RELEASES file");
                return Observable.Throw<UpdateInfo>(new Exception("Corrupt remote RELEASES file"));
            }

            if (localReleases.Count() == remoteReleases.Count()) {
                this.Log().Info("No updates, remote and local are the same");
                return Observable.Return<UpdateInfo>(null);
            }

            if (ignoreDeltaUpdates) {
                remoteReleases = remoteReleases.Where(x => !x.IsDelta);
            }

            if (localReleases.IsEmpty()) {
                this.Log().Warn("First run or local directory is corrupt, starting from scratch");

                var latestFullRelease = findCurrentVersion(remoteReleases);
                return Observable.Return(UpdateInfo.Create(findCurrentVersion(localReleases), new[] {latestFullRelease}, PackageDirectory, appFrameworkVersion));
            }

            if (localReleases.Max(x => x.Version) >= remoteReleases.Max(x => x.Version)) {
                this.Log().Warn("hwhat, local version is greater than remote version");

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
                this.Log().Error("File should exist but doesn't", targetPackage.FullName);
                throw new Exception("Checksummed file doesn't exist: " + targetPackage.FullName);
            }

            if (targetPackage.Length != downloadedRelease.Filesize) {
                this.Log().Error("File Length should be {0}, is {1}", downloadedRelease.Filesize, targetPackage.Length);
                targetPackage.Delete();
                throw new Exception("Checksummed file size doesn't match: " + targetPackage.FullName);
            } 

            using (var file = targetPackage.OpenRead()) {
                var hash = Utility.CalculateStreamSHA1(file);
                if (hash != downloadedRelease.SHA1) {
                    this.Log().Error("File SHA1 should be {0}, is {1}", downloadedRelease.SHA1, hash);
                    targetPackage.Delete();
                    throw new Exception("Checksum doesn't match: " + targetPackage.FullName);
                }
            }
        }


        //
        // ApplyReleases methods
        //

        void installPackageToAppDir(UpdateInfo updateInfo, ReleaseEntry release)
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
                        this.Log().Info("Writing {0} to app directory", targetPath);
                        inf.CopyTo(of);
                    }
                });

            var newCurrentVersion = updateInfo.FutureReleaseEntry.Version;

            // Perform post-install; clean up the previous version by asking it
            // which shortcuts to install, and nuking them. Then, run the app's
            // post install and set up shortcuts.
            runPostInstallAndCleanup(newCurrentVersion, updateInfo.IsBootstrapping);
        }

        void runPostInstallAndCleanup(Version newCurrentVersion, bool isBootstrapping)
        {
            this.Log().Debug(CultureInfo.InvariantCulture, "AppDomain ID: {0}", AppDomain.CurrentDomain.Id);

            fixPinnedExecutables(newCurrentVersion);

            var shortcutsToIgnore = cleanUpOldVersions(newCurrentVersion);
            var targetPath = getDirectoryForRelease(newCurrentVersion);

            runPostInstallOnDirectory(targetPath.FullName, isBootstrapping, newCurrentVersion, shortcutsToIgnore);
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

                return basePkg.ApplyDeltaPackage(deltaPkg,
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
            return fileSystem.GetDirectoryInfo(rootAppDirectory).GetDirectories()
                .Where(x => x.Name.StartsWith("app-", StringComparison.InvariantCultureIgnoreCase))
                .Where(x => x.Name != "app-" + newCurrentVersion)
                .OrderBy(x => x.Name)
                .SelectMany(oldAppRoot => {
                    var path = oldAppRoot.FullName;
                    var ret = AppDomainHelper.ExecuteInNewAppDomain(path, runAppSetupCleanups);

                    try {
                        Utility.DeleteDirectoryAtNextReboot(oldAppRoot.FullName);
                    } catch (Exception ex) {
                        this.Log().WarnException("Couldn't delete old app directory on next reboot", ex);
                    }
                    return ret;
                });
        }

        void fixPinnedExecutables(Version newCurrentVersion) {
            if (Environment.OSVersion.Version < new Version(6, 1)) {
                return;
            }

            var oldAppDirectories = fileSystem
                .GetDirectoryInfo(rootAppDirectory)
                .GetDirectories()
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
                    if (shortcut.Target.StartsWith(oldAppDirectory, StringComparison.OrdinalIgnoreCase)) {

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
        }

        IEnumerable<ShortcutCreationRequest> runAppSetupCleanups(string fullDirectoryPath)
        {
            var dirName = Path.GetFileName(fullDirectoryPath);
            var apps = findAppSetupsToRun(fullDirectoryPath);
            var ver = new Version(dirName.Replace("app-", ""));

            var ret = apps.SelectMany(app => uninstallAppVersion(app, ver)).ToArray();

            return ret;
        }

        IEnumerable<ShortcutCreationRequest> uninstallAppVersion(IAppSetup app, Version ver)
        {
            try {
                app.OnVersionUninstalling(ver);
            } catch (Exception ex) {
                this.Log().ErrorException("App threw exception on uninstall:  " + app.GetType().FullName, ex);
            }

            var shortcuts = Enumerable.Empty<ShortcutCreationRequest>();
            try {
                shortcuts = app.GetAppShortcutList();
            } catch (Exception ex) {
                this.Log().ErrorException("App threw exception on shortcut uninstall:  " + app.GetType().FullName, ex);
            }

            // Get the list of shortcuts that *should've* been there, but aren't;
            // this means that the user deleted them by hand and that they should 
            // stay dead
            return shortcuts.Aggregate(new List<ShortcutCreationRequest>(), (acc, x) => {
                var path = x.GetLinkTarget(applicationName);

                var fi = fileSystem.GetFileInfo(path);
                if (fi.Exists) {
                    fi.Delete();
                } else {
                    acc.Add(x);
                }

                return acc;
            });
        }

        void runPostInstallOnDirectory(string newAppDirectoryRoot, bool isFirstInstall, Version newCurrentVersion, IEnumerable<ShortcutCreationRequest> shortcutRequestsToIgnore)
        {
            var postInstallInfo = new PostInstallInfo {
                NewAppDirectoryRoot = newAppDirectoryRoot,
                IsFirstInstall = isFirstInstall,
                NewCurrentVersion = newCurrentVersion,
                ShortcutRequestsToIgnore = shortcutRequestsToIgnore.ToArray()
            };

            AppDomainHelper.ExecuteActionInNewAppDomain(postInstallInfo, info => {
                findAppSetupsToRun(info.NewAppDirectoryRoot).ForEach(app =>
                    installAppVersion(app, info.NewCurrentVersion, info.ShortcutRequestsToIgnore, info.IsFirstInstall));
            });
        }

        void installAppVersion(IAppSetup app, Version newCurrentVersion, IEnumerable<ShortcutCreationRequest> shortcutRequestsToIgnore, bool isFirstInstall)
        {
            try {
                if (isFirstInstall) app.OnAppInstall();
                app.OnVersionInstalled(newCurrentVersion);
            } catch (Exception ex) {
                this.Log().ErrorException("App threw exception on install:  " + app.GetType().FullName, ex);
                throw;
            }

            var shortcutList = Enumerable.Empty<ShortcutCreationRequest>();
            try {
                shortcutList = app.GetAppShortcutList();
            } catch (Exception ex) {
                this.Log().ErrorException("App threw exception on shortcut uninstall:  " + app.GetType().FullName, ex);
                throw;
            }

            shortcutList
                .Where(x => !shortcutRequestsToIgnore.Contains(x))
                .ForEach(x => {
                    var shortcut = x.GetLinkTarget(applicationName, true);

                    var fi = fileSystem.GetFileInfo(shortcut);
                    if (fi.Exists) fi.Delete();

                    fileSystem.CreateDirectoryRecursive(fi.Directory.FullName);

                    var sl = new ShellLink() {
                        Target = x.TargetPath,
                        IconPath = x.IconLibrary,
                        IconIndex = x.IconIndex,
                        Arguments = x.Arguments,
                        WorkingDirectory = x.WorkingDirectory,
                        Description = x.Description
                    };

                    sl.Save(shortcut);
                });
        }

        IEnumerable<IAppSetup> findAppSetupsToRun(string appDirectory)
        {
            try {
                return fileSystem.GetDirectoryInfo(appDirectory).GetFiles("*.exe")
                    .Select(x => {
                        try {
                            var ret = Assembly.LoadFile(x.FullName);
                            return ret;
                        } catch (Exception ex) {
                            this.Log().WarnException("Post-install: load failed for " + x.FullName, ex);
                            return null;
                        }
                    })
                    .Where(x => x != null)
                    .SelectMany(x => x.GetModules()).SelectMany(x => x.GetTypes().Where(y => typeof(IAppSetup).IsAssignableFrom(y)))
                    .Select(x => {
                        try {
                            return (IAppSetup)Activator.CreateInstance(x);
                        } catch (Exception ex) {
                            this.Log().WarnException("Post-install: Failed to create type " + x.FullName, ex);
                            return null;
                        }
                    })
                    .Where(x => x != null)
                    .ToArray();
            } catch (UnauthorizedAccessException ex) {
                // NB: This can happen if we run into a MoveFileEx'd directory,
                // where we can't even get the list of files in it.
                this.Log().WarnException("Couldn't search directory for IAppSetups: " + appDirectory, ex);
                return null;
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
}