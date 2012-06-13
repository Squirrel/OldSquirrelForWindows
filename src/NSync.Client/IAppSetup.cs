using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NSync.Client
{
    [Flags]
    public enum ShortcutCreationLocation {
        StartMenu = 1 << 0,
        Desktop = 1 << 1,
        Custom = 1 << 2,
    }

    public sealed class ShortcutCreationRequest
    {
        // Shortcut Details
        public string Title { get; set; }
        public string Path { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public string IconLibrary { get; set; }
        public int IconIndex { get; set; }

        // Where to put the shortcut
        public ShortcutCreationLocation CreationLocation { get; set; }
        public string CustomLocation { get; set; }
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
