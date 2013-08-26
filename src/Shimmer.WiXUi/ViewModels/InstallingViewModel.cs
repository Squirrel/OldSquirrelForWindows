using System;
using System.Reactive.Linq;
using NuGet;
using ReactiveUI;
using ReactiveUI.Routing;
using Shimmer.Client.WiXUi;

namespace Shimmer.WiXUi.ViewModels
{
    public class InstallingViewModel : ReactiveObject, IInstallingViewModel
    {
        public string UrlPathSegment { get { return "installing";  } }
        public IScreen HostScreen { get; private set; }

        IPackage _PackageMetadata;
        public IPackage PackageMetadata
        {
            get { return _PackageMetadata; }
            set { this.RaiseAndSetIfChanged(x => x.PackageMetadata, value); }
        }

        ObservableAsPropertyHelper<string> _Title;
        public string Title { get { return _Title.Value; } }

        ObservableAsPropertyHelper<string> _Summary;
        public string Summary { get { return _Summary.Value; } }

        public IObserver<int> ProgressValue { get; private set; }

        ObservableAsPropertyHelper<int> _LatestProgress;
        public int LatestProgress {
            get { return _LatestProgress.Value; }
        }
        public InstallingViewModel(IScreen hostScreen)
        {
            HostScreen = hostScreen;

            var progress = new ScheduledSubject<int>(RxApp.DeferredScheduler);
            ProgressValue = progress;

            progress.ToProperty(this, x => x.LatestProgress, 0);

            this.WhenAny(x => x.PackageMetadata, x => x.Value)
                .SelectMany(metadata => metadata != null
                               ? Observable.Return(new Tuple<string, string>(metadata.Title, metadata.Id))
                               : Observable.Return(new Tuple<string, string>("", "")))
                .Select(tuple => !String.IsNullOrWhiteSpace(tuple.Item1)
                                        ? tuple.Item1
                                        : tuple.Item2)
                .ToProperty(this, x => x.Title);

            this.WhenAny(x => x.PackageMetadata, v => v.Value)
                .SelectMany(x => x != null ? Observable.Return(x.Summary) : Observable.Return(""))
                .ToProperty(this, x => x.Summary);
        }
    }
}