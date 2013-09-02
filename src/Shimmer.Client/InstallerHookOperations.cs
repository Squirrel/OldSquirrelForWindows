using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using ReactiveUIMicro;
using Shimmer.Core;

namespace Shimmer.Client
{
    [Serializable]
    class InstallerHookOperations
    {
        readonly IRxUIFullLogger log;
        readonly IFileSystemFactory fileSystem;
        readonly string applicationName;

        public InstallerHookOperations(IRxUIFullLogger log, IFileSystemFactory fileSystem, string applicationName)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.applicationName = applicationName;
        }

        public IEnumerable<string> RunAppSetupInstallers(PostInstallInfo info)
        {
            var appSetups = default(IEnumerable<IAppSetup>);

            try {
                appSetups = findAppSetupsToRun(info.NewAppDirectoryRoot);
            } catch (UnauthorizedAccessException ex) {
                log.ErrorException("Failed to load IAppSetups in post-install due to access denied", ex);
                return new string[0];
            }

            return appSetups
                .Select(app => installAppVersion(app, info.NewCurrentVersion, info.ShortcutRequestsToIgnore, info.IsFirstInstall))
                .Where(x => x != null)
                .ToArray();
        }

        public IEnumerable<ShortcutCreationRequest> RunAppSetupCleanups(string fullDirectoryPath)
        {
            var dirName = Path.GetFileName(fullDirectoryPath);
            var ver = new Version(dirName.Replace("app-", ""));

            var apps = default(IEnumerable<IAppSetup>);
            try {
                apps = findAppSetupsToRun(fullDirectoryPath);
            } catch (UnauthorizedAccessException ex) {
                log.ErrorException("Couldn't run cleanups", ex);
                return Enumerable.Empty<ShortcutCreationRequest>();
            }

            var ret = apps.SelectMany(app => uninstallAppVersion(app, ver)).ToArray();

            return ret;
        }

        IEnumerable<ShortcutCreationRequest> uninstallAppVersion(IAppSetup app, Version ver)
        {
            try {
                app.OnVersionUninstalling(ver);
            } catch (Exception ex) {
                log.ErrorException("App threw exception on uninstall:  " + app.GetType().FullName, ex);
            }

            var shortcuts = Enumerable.Empty<ShortcutCreationRequest>();
            try {
                shortcuts = app.GetAppShortcutList();
            } catch (Exception ex) {
                log.ErrorException("App threw exception on shortcut uninstall:  " + app.GetType().FullName, ex);
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

        string installAppVersion(IAppSetup app, Version newCurrentVersion, IEnumerable<ShortcutCreationRequest> shortcutRequestsToIgnore, bool isFirstInstall)
        {
            try {
                if (isFirstInstall) app.OnAppInstall();
                app.OnVersionInstalled(newCurrentVersion);
            } catch (Exception ex) {
                log.ErrorException("App threw exception on install:  " + app.GetType().FullName, ex);
                throw;
            }

            var shortcutList = Enumerable.Empty<ShortcutCreationRequest>();
            try {
                shortcutList = app.GetAppShortcutList();
            } catch (Exception ex) {
                log.ErrorException("App threw exception on shortcut uninstall:  " + app.GetType().FullName, ex);
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
                        Description = x.Description,
                    };

                    sl.Save(shortcut);
                });

            return app.LaunchOnSetup ? app.Target : null;
        }

        IEnumerable<IAppSetup> findAppSetupsToRun(string appDirectory)
        {
            var allExeFiles = default(FileInfoBase[]);

            try {
                allExeFiles = fileSystem.GetDirectoryInfo(appDirectory).GetFiles("*.exe");
            } catch (UnauthorizedAccessException ex) {
                // NB: This can happen if we run into a MoveFileEx'd directory,
                // where we can't even get the list of files in it.
                log.WarnException("Couldn't search directory for IAppSetups: " + appDirectory, ex);
                throw;
            }

            var locatedAppSetups = allExeFiles
                .Select(x => loadAssemblyOrWhine(x.FullName)).Where(x => x != null)
                .SelectMany(x => x.GetModules())
                .SelectMany(x => {
                     try {
                         return x.GetTypes().Where(y => typeof (IAppSetup).IsAssignableFrom(y));
                     } catch (ReflectionTypeLoadException ex) {
                         log.WarnException("Couldn't load types from module", ex);
                         ex.LoaderExceptions.ForEach(lex => log.WarnException("LoaderException: ", lex));
                         return Enumerable.Empty<Type>();
                     }
                })
                .Select(createInstanceOrWhine).Where(x => x != null)
                .ToArray();

            return locatedAppSetups.Length > 0
                ? locatedAppSetups
                : allExeFiles.Select(x => new DidntFollowInstructionsAppSetup(x.FullName)).ToArray();
        }


        IAppSetup createInstanceOrWhine(Type typeToCreate)
        {
            try {
                return (IAppSetup) Activator.CreateInstance(typeToCreate);
            }
            catch (Exception ex) {
                log.WarnException("Post-install: Failed to create type " + typeToCreate.FullName, ex);
                return null;
            }
        }

        Assembly loadAssemblyOrWhine(string fileToLoad)
        {
            try {
                var ret = Assembly.LoadFile(fileToLoad);
                return ret;
            }
            catch (Exception ex) {
                log.WarnException("Post-install: load failed for " + fileToLoad, ex);
                return null;
            }
        }
    }
}
