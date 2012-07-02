using System;
using System.Configuration;
using System.Data;
using System.Windows;

namespace ShimmerIAppUpdateTestTarget
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            MessageBox.Show("This is an app used by the unit test runner. Ignore me!");
            Environment.Exit(-1);
        }
    }
}