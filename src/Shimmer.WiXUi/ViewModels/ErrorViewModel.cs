using System;
using System.Reactive.Linq;
using NuGet;
using ReactiveUI;
using ReactiveUI.Routing;
using ReactiveUI.Xaml;
using Shimmer.Client.WiXUi;

namespace Shimmer.WiXUi.ViewModels
{
    public class ErrorViewModel : ReactiveObject, IErrorViewModel
    {
        public string UrlPathSegment { get { return "error"; } }

        public IScreen HostScreen { get; private set; }

#pragma warning disable 649
        IPackage _PackageMetadata;
#pragma warning restore 649
        public IPackage PackageMetadata {
            get { return _PackageMetadata; }
            set { this.RaiseAndSetIfChanged(x => x.PackageMetadata, value); }
        }

#pragma warning disable 649
        ObservableAsPropertyHelper<string> _Title; 
#pragma warning restore 649
        public string Title {
            get { return _Title.Value; }
        }
            
#pragma warning disable 649
        UserError _Error;
#pragma warning restore 649
        public UserError Error {
            get { return _Error; }
            set { this.RaiseAndSetIfChanged(x => x.Error, value); }
        }

        public ReactiveCommand Shutdown { get; protected set; }

        public ErrorViewModel(IScreen hostScreen)
        {
            HostScreen = hostScreen;

            Shutdown = new ReactiveCommand();
            this.WhenAny(x => x.PackageMetadata, x => x.Value)
                .SelectMany(x => x != null ? Observable.Return(x.Title) : Observable.Empty<string>())
                .ToProperty(this, x => x.Title);
        }
    }
}
