using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO.Abstractions;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NSync.Core;
using NuGet;
using ReactiveUI;

using IEnableLogger = NSync.Core.IEnableLogger;

// NB: These are whitelisted types from System.IO, so that we always end up 
// using fileSystem instead.
using Path = System.IO.Path;
using StreamReader = System.IO.StreamReader;
using MemoryStream = System.IO.MemoryStream;
using FileMode = System.IO.FileMode;
using FileAccess = System.IO.FileAccess;

namespace NSync.Client
{
    public interface IUpdateManager
    {
        IDisposable AcquireUpdateLock();
        IObservable<UpdateInfo> CheckForUpdate(bool ignoreDeltaUpdates = false);
        IObservable<Unit> DownloadReleases(IEnumerable<ReleaseEntry> releasesToDownload);
        IObservable<Unit> ApplyReleases(UpdateInfo updateInfo);
    }

    public class UpdateManager : IEnableLogger, IUpdateManager
    {
        readonly IFileSystemFactory fileSystem;
        readonly string rootAppDirectory;
        readonly string applicationName;
        readonly IUrlDownloader urlDownloader;
        readonly string updateUrlOrPath;

        bool hasUpdateLock;

        public UpdateManager(string urlOrPath, 
            string applicationName,
            string rootDirectory = null,
            IFileSystemFactory fileSystem = null,
            IUrlDownloader urlDownloader = null)
        {
            Contract.Requires(!String.IsNullOrEmpty(urlOrPath));
            Contract.Requires(!String.IsNullOrEmpty(applicationName));

            updateUrlOrPath = urlOrPath;
            this.applicationName = applicationName;

            rootAppDirectory = Path.Combine(rootDirectory ?? getLocalAppDataDirectory(), applicationName);
            this.fileSystem = fileSystem ?? new AnonFileSystem(
                s => new DirectoryInfoWrapper(new System.IO.DirectoryInfo(s)),
                s => new FileInfoWrapper(new System.IO.FileInfo(s)),
                s => new FileWrapper(),
                s => new DirectoryInfoWrapper(new System.IO.DirectoryInfo(s).CreateRecursive()),
                s => new System.IO.DirectoryInfo(s).Delete(true),
                Utility.CopyToAsync,
                Utility.CreateTempFile);

            this.urlDownloader = urlDownloader;
        }

        public IObservable<UpdateInfo> CheckForUpdate(bool ignoreDeltaUpdates = false)
        {
            IEnumerable<ReleaseEntry> localReleases = Enumerable.Empty<ReleaseEntry>();

            if (!hasUpdateLock) {
                return Observable.Throw<UpdateInfo>(new Exception("Call AcquireUpdateLock before using this method"));
            }

            try {
                var fi = fileSystem.GetFileInfo(Path.Combine(rootAppDirectory, "packages", "RELEASES"));
                var file = fi.OpenRead();

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
            if (isHttpUrl(updateUrlOrPath)) {
                releaseFile = urlDownloader.DownloadUrl(String.Format("{0}/{1}", updateUrlOrPath, "RELEASES"));
            } else {
                var fi = fileSystem.GetFileInfo(Path.Combine(updateUrlOrPath, "RELEASES"));
                using (var sr = new StreamReader(fi.OpenRead(), Encoding.UTF8)) {
                    var text = sr.ReadToEnd();
                    releaseFile = Observable.Return(text);
                }
            }

            var ret =  releaseFile
                .Select(ReleaseEntry.ParseReleaseFile)
                .SelectMany(releases => determineUpdateInfo(localReleases, releases, ignoreDeltaUpdates))
                .Multicast(new AsyncSubject<UpdateInfo>());

            ret.Connect();
            return ret;
        }

        public IObservable<Unit> DownloadReleases(IEnumerable<ReleaseEntry> releasesToDownload)
        {
            Contract.Requires(releasesToDownload != null);

            if (!hasUpdateLock) {
                return Observable.Throw<Unit>(new Exception("Call AcquireUpdateLock before using this method"));
            }

            IObservable<Unit> downloadResult;

            if (isHttpUrl(updateUrlOrPath)) {
                var urls = releasesToDownload.Select(x => String.Format("{0}/{1}", updateUrlOrPath, x.Filename));
                var paths = releasesToDownload.Select(x => Path.Combine(rootAppDirectory, "packages", x.Filename));

                downloadResult = urlDownloader.QueueBackgroundDownloads(urls, paths);
            } else {
                downloadResult = releasesToDownload.ToObservable()
                    .Select(x => Observable.Defer(() =>
                        fileSystem.CopyAsync(
                            Path.Combine(updateUrlOrPath, x.Filename),
                            Path.Combine(rootAppDirectory, "packages", x.Filename))))
                    .Merge(2);
            }

            return downloadResult.SelectMany(_ => checksumAllPackages(releasesToDownload));
        }

        public IObservable<Unit> ApplyReleases(UpdateInfo updateInfo)
        {
            Contract.Requires(updateInfo != null);

            if (!hasUpdateLock) {
                return Observable.Throw<Unit>(new Exception("Call AcquireUpdateLock before using this method"));
            }

            var fullPackageToApply = createFullPackagesFromDeltas(updateInfo.ReleasesToApply, updateInfo.CurrentlyInstalledVersion);

            return fullPackageToApply.SelectMany(release => Observable.Start(() => {
                var pkg = new ZipPackage(Path.Combine(rootAppDirectory, "packages", release.Filename));
                var target = fileSystem.GetDirectoryInfo(Path.Combine(rootAppDirectory, "app-" + release.Version));
                target.Create();

                // NB: We sort this list in order to guarantee that if a Net20
                // and a Net40 version of a DLL get shipped, we always end up
                // with the 4.0 version.
                pkg.GetFiles()
                    .Where(x => x.Path.StartsWith("lib", StringComparison.InvariantCultureIgnoreCase))
                    .OrderBy(x => x.Path)
                    .ForEach(x => {
                        var m = Regex.Match(x.Path, @".*\\([^\\]+)$");
                        var targetPath = Path.Combine(target.FullName, m.Groups[1].Value);

                        var fi = fileSystem.GetFileInfo(targetPath);
                        if (fi.Exists) {
                            fi.Delete();
                        }

                        using (var inf = x.GetStream())
                        using (var of = fi.Open(FileMode.CreateNew, FileAccess.Write)) {
                            this.Log().Info("Writing {0} to app directory", targetPath);
                            inf.CopyTo(of);
                        }
                    });

                var newCurrentVersion = updateInfo.ReleasesToApply.MaxBy(x => x.Version).First().Version;

                var shortcutsToIgnore = cleanUpOldVersions(newCurrentVersion);
                runPostInstallOnDirectory(target.FullName, updateInfo.CurrentlyInstalledVersion == null, newCurrentVersion, shortcutsToIgnore);
            })).SelectMany(_ => UpdateLocalReleasesFile());
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
            return Observable.Start(() => {
                var packagesDir = fileSystem.GetDirectoryInfo(Path.Combine(rootAppDirectory, "packages"));

                var entries = packagesDir.GetFiles("*.nupkg")
                    .Select(x => {
                        using (var file = x.OpenRead()) {
                            return ReleaseEntry.GenerateFromFile(file, x.Name);
                        }
                    }).ToArray();

                var tempFile = fileSystem.CreateTempFile();
                try {
                    if (entries.Length > 0) {
                        ReleaseEntry.WriteReleaseFile(entries, tempFile.Item2);
                    }
                } finally {
                    tempFile.Item2.Dispose();
                }

                fileSystem.GetFileInfo(tempFile.Item1).MoveTo(Path.Combine(packagesDir.FullName, "RELEASES"));
            }, RxApp.TaskpoolScheduler);
        }

        void initializeClientAppDirectory()
        {
            var pkgDir = Path.Combine(rootAppDirectory, "packages");
            if (fileSystem.GetDirectoryInfo(pkgDir).Exists) {
                fileSystem.DeleteDirectoryRecursive(pkgDir);
            }
            fileSystem.CreateDirectoryRecursive(pkgDir);
        }

        IObservable<UpdateInfo> determineUpdateInfo(IEnumerable<ReleaseEntry> localReleases, IEnumerable<ReleaseEntry> remoteReleases, bool ignoreDeltaUpdates)
        {
            var pkgDir = Path.Combine(rootAppDirectory, "packages");
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
                return Observable.Return(UpdateInfo.Create(findCurrentVersion(localReleases), new[] {latestFullRelease}, pkgDir));
            }

            if (localReleases.Max(x => x.Version) >= remoteReleases.Max(x => x.Version)) {
                this.Log().Warn("hwhat, local version is greater than remote version");

                var latestFullRelease = findCurrentVersion(remoteReleases);
                return Observable.Return(UpdateInfo.Create(findCurrentVersion(localReleases), new[] {latestFullRelease}, pkgDir));
            }

            return Observable.Return(UpdateInfo.Create(findCurrentVersion(localReleases), remoteReleases, pkgDir));
        }

        ReleaseEntry findCurrentVersion(IEnumerable<ReleaseEntry> localReleases)
        {
            if (!localReleases.Any()) {
                return null;
            }

            return localReleases.MaxBy(x => x.Version).SingleOrDefault(x => !x.IsDelta);
        }

        static string getLocalAppDataDirectory()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
        
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
            return releasesDownloaded.ToObservable()
                .Select(x => Observable.Defer(() => checksumPackage(x)))
                .Merge(3)
                .Aggregate(Unit.Default, (acc, _) => acc);
        }

        IObservable<Unit> checksumPackage(ReleaseEntry downloadedRelease)
        {
            return Observable.Start(() => {
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
            }, RxApp.TaskpoolScheduler);
        }

        IObservable<ReleaseEntry> createFullPackagesFromDeltas(IEnumerable<ReleaseEntry> releasesToApply, ReleaseEntry currentVersion)
        {
            if (!releasesToApply.Any() || releasesToApply.All(x => !x.IsDelta)) {
                return Observable.Return(releasesToApply.MaxBy(x => x.Version).First());
            }

            if (!releasesToApply.Any(x => x.IsDelta)) {
                return Observable.Throw<ReleaseEntry>(new Exception("Cannot apply combinations of delta and full packages"));
            }

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

                return createFullPackagesFromDeltas(releasesToApply.Skip(1), entry);
            });
        }

        IEnumerable<ShortcutCreationRequest> cleanUpOldVersions(Version newCurrentVersion)
        {
            // XXX: Make sure we don't blow up if their IAppSetup sucks
            return fileSystem.GetDirectoryInfo(rootAppDirectory).GetDirectories()
                .Where(x => x.Name.StartsWith("app-", StringComparison.InvariantCultureIgnoreCase))
                .Where(x => x.Name != "app-" + newCurrentVersion)
                .SelectMany(dir => {
                    var apps = findAppSetupsToRun(dir.FullName);
                    var ver = new Version(dir.Name.Replace("app-", ""));

                    var ret = apps.SelectMany(app => {
                        app.OnVersionUninstalling(ver);

                        var shortcuts = app.GetAppShortcutList();
                        return shortcuts.Aggregate(new List<ShortcutCreationRequest>(), (acc, x) => {
                            var path = x.GetLinkTarget(applicationName, false);

                            var fi = fileSystem.GetFileInfo(path);
                            if (fi.Exists) {
                                fi.Delete();
                            } else {
                                acc.Add(x);
                            }
                            return acc;
                        });
                    
                    });
                        
                    Utility.DeleteDirectory(dir.FullName);
                    return ret;
                });
        }

        void runPostInstallOnDirectory(string newAppDirectoryRoot, bool isFirstInstall, Version newCurrentVersion, IEnumerable<ShortcutCreationRequest> shortcutRequestsToIgnore)
        {
            // XXX: Make sure we don't blow up if their IAppSetup sucks
            findAppSetupsToRun(newAppDirectoryRoot)
                .ForEach(app => {
                    if (isFirstInstall)  app.OnAppInstall();
                    app.OnVersionInstalled(newCurrentVersion);

                    app.GetAppShortcutList()
                        .Where(x => !shortcutRequestsToIgnore.Contains(x))
                        .ForEach(x => {
                            var shortcut = x.GetLinkTarget(applicationName, true);

                            var fi = fileSystem.GetFileInfo(shortcut);
                            if (fi.Exists) {
                                fi.Delete();
                            }

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
                });
        }

        IEnumerable<IAppSetup> findAppSetupsToRun(string appDirectory)
        {
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
                .SelectMany(x => x.GetModules())
                .SelectMany(x => x.GetTypes().Where(y => typeof(IAppSetup).IsAssignableFrom(y)))
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
        }
    }

    public static class UpdateManagerMixins
    {
        public static IObservable<Unit> UpdateApp(this IUpdateManager This)
        {
            IDisposable theLock;

            try {
                theLock = This.AcquireUpdateLock();
            } catch (TimeoutException _) {
                return Observable.Return(Unit.Default);
            } catch (Exception ex) {
                return Observable.Throw<Unit>(ex);
            }

            var ret = This.CheckForUpdate()
                .SelectMany(x => This.DownloadReleases(x.ReleasesToApply).Select(_ => x))
                .SelectMany(x => This.ApplyReleases(x))
                .Finally(() => theLock.Dispose())
                .Multicast(new AsyncSubject<Unit>());

            ret.Connect();
            return ret;
        }
    }

    public class UpdateInfo
    {
        public Version Version { get; protected set; }
        public ReleaseEntry CurrentlyInstalledVersion { get; protected set; }
        public IEnumerable<ReleaseEntry> ReleasesToApply { get; protected set; }
        string packageDirectory;

        protected UpdateInfo(ReleaseEntry currentlyInstalledVersion, IEnumerable<ReleaseEntry> releasesToApply, string packageDirectory)
        {
            // NB: When bootstrapping, CurrentlyInstalledVersion is null!
            CurrentlyInstalledVersion = currentlyInstalledVersion;
            Version = currentlyInstalledVersion != null ? currentlyInstalledVersion.Version : null;
            ReleasesToApply = releasesToApply ?? Enumerable.Empty<ReleaseEntry>();

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