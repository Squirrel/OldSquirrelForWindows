using System;
using System.Windows;

namespace ShimmerIAppUpdateTestTarget
{
    public partial class App
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            MessageBox.Show("This is an app used by the unit test runner. Ignore me!");
            Environment.Exit(-1);
        }
    }
}