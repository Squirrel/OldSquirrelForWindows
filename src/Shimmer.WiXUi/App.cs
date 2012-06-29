using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;
using ReactiveUI.Routing;
using Shimmer.ViewModels.WiXUi;
using Shimmer.WiXUi.Views;

namespace Shimmer.WiXUi
{
    public class App : BootstrapperApplication
    {
        protected override void Run()
        {
            var app = new Application();
            var bootstrapper = new AppBootstrapper(this);

            app.MainWindow = new RootWindow {
                // XXX: Fix this casting shit in ReactiveUI.Routing
                viewHost = {Router = (RoutingState) bootstrapper.Router}
            };
        }
    }
}
