using System;
using System.IO;
using System.Reflection;
using Shimmer.Client;

namespace SampleUpdatingApp
{
    public class WriteAFileDuringSetup : AppSetup
    {
        public override string ShortcutName
        {
            get { return "SampleUpdatingApp"; }
        }

        public override void OnAppInstall()
        {
            base.OnAppInstall();

            var file = Path.Combine(GetCurrentDirectory(), "install");

            File.WriteAllText(file, Guid.NewGuid().ToString());
        }

        public override void OnVersionInstalled(Version versionBeingInstalled)
        {
            base.OnVersionInstalled(versionBeingInstalled);

            if (versionBeingInstalled <= new Version(1, 2)) {
                var file = Path.Combine(GetCurrentDirectory(), "install-" + versionBeingInstalled);
                File.WriteAllText(file, Guid.NewGuid().ToString());
            }
        }

        public override void OnVersionUninstalling(Version versionBeingUninstalled)
        {
            base.OnVersionUninstalling(versionBeingUninstalled);
            var currentDirectory = GetCurrentDirectory();

            var directoryInfo = new DirectoryInfo(currentDirectory);

            var appRoot = directoryInfo.Parent.Parent;

            var file = Path.Combine(appRoot.FullName, "uninstall-" + versionBeingUninstalled);

            File.WriteAllText(file, Guid.NewGuid().ToString());
        }

        public override void OnAppUninstall()
        {
            var currentDirectory = GetCurrentDirectory();

            var directoryInfo = new DirectoryInfo(currentDirectory);

            var appRoot = directoryInfo.Parent.Parent;

            var file = Path.Combine(appRoot.FullName, "uninstall");

            File.WriteAllText(file, Guid.NewGuid().ToString());

            base.OnAppUninstall();
        }

        static string GetCurrentDirectory()
        {
            var codeBase = Assembly.GetExecutingAssembly().CodeBase;
            var uri = new UriBuilder(codeBase);
            var path = Uri.UnescapeDataString(uri.Path);
            return Path.GetDirectoryName(path);
        }
    }
}
