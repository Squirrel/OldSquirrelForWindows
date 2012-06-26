using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Shimmer.Core;

namespace Shimmer.Client
{
    public enum ShortcutCreationLocation {
        StartMenu,
        Desktop,
        Custom,
    }

    public sealed class ShortcutCreationRequest : IEquatable<ShortcutCreationRequest>
    {
        // Shortcut Details
        public string Title { get; set; }
        public string Description { get; set; }
        public string TargetPath { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public string IconLibrary { get; set; }
        public int IconIndex { get; set; }

        // Where to put the shortcut
        public ShortcutCreationLocation CreationLocation { get; set; }
        public string CustomLocation { get; set; }

        public string GetLinkTarget(string applicationName, bool createDirectoryIfNecessary = false)
        {
            var dir = default(string);

            switch(CreationLocation) {
            case ShortcutCreationLocation.Desktop:
                dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                break;
            case ShortcutCreationLocation.StartMenu:
                dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), applicationName);
                break;
            case ShortcutCreationLocation.Custom:
                dir = (new FileInfo(CustomLocation)).DirectoryName;
                break;
            }

            if (createDirectoryIfNecessary && Directory.Exists(dir)) {
                (new DirectoryInfo(dir)).CreateRecursive();
            }

            return Path.Combine(dir, Title + ".lnk");
        }

        #region Boring Equality Stuff
        public bool Equals(ShortcutCreationRequest other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Title, other.Title) && Equals(CreationLocation, other.CreationLocation) && string.Equals(CustomLocation, other.CustomLocation);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (ShortcutCreationRequest)) return false;
            return Equals((ShortcutCreationRequest) obj);
        }

        public override int GetHashCode()
        {
            unchecked {
                int hashCode = (Title != null ? Title.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ CreationLocation.GetHashCode();
                hashCode = (hashCode*397) ^ (CustomLocation != null ? CustomLocation.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(ShortcutCreationRequest left, ShortcutCreationRequest right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ShortcutCreationRequest left, ShortcutCreationRequest right)
        {
            return !Equals(left, right);
        }
        #endregion
    }

    /// <summary>
    /// This interface is implemented by every EXE in your application in order
    /// to perform setup routines, as well as to get the list of shortcuts that
    /// an EXE should have.
    ///
    /// The easiest way to implement this interface is by using the abstract 
    /// base class AppSetup.
    /// </summary>
    public interface IAppSetup
    {
        /// <summary>
        /// Get the list of shortcuts the EXE should install. Usually you would
        /// return a shortcut at least for the currently executing assembly (i.e.
        /// your own EXE).
        /// </summary>
        /// <returns>A list of shortcuts to install, or Enumerable.Empty if no
        /// shortcuts should be installed. Do *not* return 'null'</returns>
        IEnumerable<ShortcutCreationRequest> GetAppShortcutList();

        /// <summary>
        /// Called by setup when the application is initially installed.
        /// </summary>
        void OnAppInstall();

        /// <summary>
        /// Called by setup when the application is about to be completely 
        /// uninstalled.
        /// </summary>
        void OnAppUninstall();

        /// <summary>
        /// Called by UpdateManager when a new version of the app is about to
        /// be installed. Note that this will still be called even on initial
        /// install.
        /// </summary>
        /// <param name="versionBeingInstalled">The version being installed.</param>
        void OnVersionInstalled(Version versionBeingInstalled);

        /// <summary>
        /// Called by UpdateManager when a new version of the app is about to
        /// be uninstalled. Note that this will still be called even on app
        /// uninstall.
        /// </summary>
        /// <param name="versionBeingInstalled">The version being installed.</param>
        void OnVersionUninstalling(Version versionBeingUninstalled);
    }

    public abstract class AppSetup : IAppSetup
    {
        /// <summary>
        /// The name of the shortcut that will show up on the Desktop. Typically
        /// this is the application name.
        /// </summary>
        public abstract string ShortcutName { get; }

        /// <summary>
        /// The Description field that can be seen in the Properties page of a
        /// shortcut.
        /// </summary>
        public virtual string AppDescription {
            get {
                return "";
            }
        }

        public virtual IEnumerable<ShortcutCreationRequest> GetAppShortcutList()
        {
            // XXX: Make sure this trick actually works; we want the derived 
            // type's assembly
            var target = this.GetType().Assembly.Location;

            return new[] {
                new ShortcutCreationRequest() {
                    CreationLocation = ShortcutCreationLocation.Desktop,
                    Title = ShortcutName,
                    Description =  AppDescription,
                    IconLibrary = target,
                    IconIndex = 0,
                },
                new ShortcutCreationRequest() {
                    CreationLocation = ShortcutCreationLocation.StartMenu,
                    Title = ShortcutName,
                    Description =  AppDescription,
                    IconLibrary = target,
                    IconIndex = 0,
                }
            };
        }

        public virtual void OnAppInstall()
        {
        }

        public virtual void OnAppUninstall()
        {
        }

        public virtual void OnVersionInstalled(Version versionBeingInstalled)
        {
        }

        public virtual void OnVersionUninstalling(Version versionBeingUninstalled)
        {
        }
    }
}
