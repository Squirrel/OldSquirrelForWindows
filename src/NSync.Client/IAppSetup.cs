using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NSync.Core;

namespace NSync.Client
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

    public interface IAppSetup
    {
        IEnumerable<ShortcutCreationRequest> GetAppShortcutList();

        void OnAppInstall();
        void OnAppUninstall();
        void OnVersionInstalled(Version versionBeingInstalled);
        void OnVersionUninstalling(Version versionBeingUninstalled);
    }
}
