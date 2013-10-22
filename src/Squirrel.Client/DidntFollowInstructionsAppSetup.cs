using System.Diagnostics;

namespace Shimmer.Client
{
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