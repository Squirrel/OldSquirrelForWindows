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

            var file = GetFileToTouch();

            File.WriteAllText(file, Guid.NewGuid().ToString());
        }

        public override void OnAppUninstall()
        {
            var file = GetFileToTouch();

            if (File.Exists(file))
                File.Delete(file);

            base.OnAppUninstall();
        }

        static string GetFileToTouch()
        {
            var codeBase = Assembly.GetExecutingAssembly().CodeBase;
            var uri = new UriBuilder(codeBase);
            var path = Uri.UnescapeDataString(uri.Path);
            var folder = Path.GetDirectoryName(path);

            return Path.Combine(folder, "testfile");
        }
    }
}
