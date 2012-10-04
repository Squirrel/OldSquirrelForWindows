using System;
using System.Diagnostics;

namespace Shimmer.Client
{
    public class DidntFollowInstructionsAppSetup : AppSetup
    {
        readonly string shortCutName;
        public override string ShortcutName {
            get { return shortCutName; }
        }

        public DidntFollowInstructionsAppSetup(string exeFile)
        {
            var fvi = FileVersionInfo.GetVersionInfo(exeFile);
            shortCutName = fvi.ProductName ?? fvi.FileDescription ?? fvi.FileName.Replace(".exe", "");
            Target = exeFile;
        }
    }
}