using System;
using System.Reactive.Linq;
using NuGet;
using ReactiveUI;
using ReactiveUI.Routing;
using ReactiveUI.Xaml;
using Shimmer.Client.WiXUi;
using Shimmer.Core;

namespace Shimmer.WiXUi.ViewModels
{
    public class WelcomeViewModel : ReactiveObject, IWelcomeViewModel
    {
        public string UrlPathSegment { get { return "welcome"; } }
        public IScreen HostScreen { get; protected set; }

        IPackage _PackageMetadata;
        public IPackage PackageMetadata
        {
            get { return _PackageMetadata; }
            set { this.RaiseAndSetIfChanged(x => x.PackageMetadata, value); }
        }

        ObservableAsPropertyHelper<string> _Title;
        public string Title {
            get { return _Title.Value; }
        }

        ObservableAsPropertyHelper<string> _Description;
        public string Description {
            get { return _Description.Value; }
        }

        public ReactiveCommand ShouldProceed { get; private set; }

        public WelcomeViewModel(IScreen hostScreen)
        {
            HostScreen = hostScreen;
            ShouldProceed = new ReactiveCommand();

            this.WhenAny(x => x.PackageMetadata, x => x.Value.ExtractTitle())
                .ToProperty(this, x => x.Title);

            this.WhenAny(x => x.PackageMetadata, v => v.Value)
                .Select(x => x != null ? x.Description : String.Empty)
                .ToProperty(this, x => x.Description);
        }
    }
}