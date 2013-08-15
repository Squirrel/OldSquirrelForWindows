using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Windows.Input;
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

        IPackage _PackageMetadata;
        public IPackage PackageMetadata {
            get { return _PackageMetadata; }
            set { this.RaiseAndSetIfChanged(x => x.PackageMetadata, value); }
        }

        ObservableAsPropertyHelper<string> _Title; 
        public string Title {
            get { return _Title.Value; }
        }
            
        UserError _Error;
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
