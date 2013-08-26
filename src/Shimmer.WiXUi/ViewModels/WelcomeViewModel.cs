using System;
using System.Reactive.Linq;
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

            this.WhenAny(x => x.PackageMetadata, x => x.Value)
                .SelectMany(metadata => metadata != null
                               ? Observable.Return(new Tuple<string, string>(metadata.Title, metadata.Id))
                               : Observable.Return(new Tuple<string, string>("","")))
                .Select(tuple => !String.IsNullOrWhiteSpace(tuple.Item1)
                                        ? tuple.Item1
                                        : tuple.Item2)
                .ToProperty(this, x => x.Title);

            this.WhenAny(x => x.PackageMetadata, v => v.Value)
                .SelectMany(x => x != null ? Observable.Return(x.Description) : Observable.Return(""))
                .ToProperty(this, x => x.Description);
        }
    }
}