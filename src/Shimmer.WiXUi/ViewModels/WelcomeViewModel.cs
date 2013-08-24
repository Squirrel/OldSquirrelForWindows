using System;
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

#pragma warning disable 649
        IPackage _PackageMetadata;
#pragma warning restore 649
        public IPackage PackageMetadata
        {
            get { return _PackageMetadata; }
            set
            {
                this.RaiseAndSetIfChanged(x => x.PackageMetadata, value);
                this.RaisePropertyChanged(x => x.Title);
                this.RaisePropertyChanged(x => x.Summary);
            }
        }

        public string Title
        {
            get
            {
                if (_PackageMetadata == null) return String.Empty;

                return string.IsNullOrWhiteSpace(_PackageMetadata.Title)
                         ? _PackageMetadata.Id
                         : _PackageMetadata.Title;
            }
        }

        public string Summary
        {
            get
            {
                if (_PackageMetadata == null) return String.Empty;

                return _PackageMetadata.Summary;
            }
        }

        public ReactiveCommand ShouldProceed { get; private set; }

        public WelcomeViewModel(IScreen hostScreen)
        {
            HostScreen = hostScreen;
            ShouldProceed = new ReactiveCommand();
        }
    }
}