using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
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
        public IPackage PackageMetadata {
            get { return _PackageMetadata; }
            set { this.RaiseAndSetIfChanged(x => x.PackageMetadata, value); }
        }

        public IObserver<int> ProgressValue { get; private set; }

        ObservableAsPropertyHelper<int> _LatestProgress;
        public int LatestProgress {
            get { return _LatestProgress.Value; }
        }

        ObservableAsPropertyHelper<string> _Title;
        public string Title {
            get { return _Title.Value; }
        }

        ObservableAsPropertyHelper<string> _Description;
        public string Description {
            get { return _Description.Value; }
        }

        public InstallingViewModel(IScreen hostScreen)
        {
            HostScreen = hostScreen;

            var progress = new Subject<int>();
            ProgressValue = progress;

            progress.ToProperty(this, x => x.LatestProgress, 0);

            this.WhenAny(x => x.PackageMetadata, x => x.Value)
                .SelectMany(x => x != null ? Observable.Return(x.Title) : Observable.Empty<string>())
                .ToProperty(this, x => x.Title);

            this.WhenAny(x => x.PackageMetadata, x => x.Value)
                .SelectMany(x => x != null ? Observable.Return(x.Description) : Observable.Empty<string>())
                .ToProperty(this, x => x.Description);
        }
    }
}