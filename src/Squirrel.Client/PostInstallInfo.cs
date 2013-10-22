using System;

namespace Shimmer.Client
{
    [Serializable]
    public class PostInstallInfo
    {
        public bool IsFirstInstall { get; set; }
        public string NewAppDirectoryRoot { get; set; }
        public Version NewCurrentVersion { get; set; }
        public ShortcutCreationRequest[] ShortcutRequestsToIgnore { get; set; }
    }
}