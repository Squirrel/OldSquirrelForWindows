using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGet;
using ReactiveUI;
using ReactiveUI.Routing;
using ReactiveUI.Xaml;
using Shimmer.Client.WiXUi;

namespace Shimmer.WiXUi.ViewModels
{
    public class WelcomeViewModel : ReactiveObject, IWelcomeViewModel
    {
        public string UrlPathSegment { get { return "welcome"; } }
        public IScreen HostScreen { get; protected set; }

        IPackage _PackageMetadata;
        public IPackage PackageMetadata {
            get { return _PackageMetadata; }
            set { this.RaiseAndSetIfChanged(x => x.PackageMetadata, value); }
        }

        public ReactiveCommand ShouldProceed { get; private set; }

        public WelcomeViewModel(IScreen hostScreen)
        {
            HostScreen = hostScreen;
            ShouldProceed = new ReactiveCommand();
        }
    }
}