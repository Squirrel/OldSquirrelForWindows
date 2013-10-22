using System;
using System.Reactive.Linq;
using NuGet;
using ReactiveUI;
using ReactiveUI.Routing;
using Squirrel.Client.WiXUi;
using Squirrel.Core.Extensions;

namespace Squirrel.WiXUi.ViewModels
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

        ObservableAsPropertyHelper<string> _Title;
        public string Title {
            get { return _Title.Value; }
        }

        ObservableAsPropertyHelper<string> _Description;
        public string Description {
            get { return _Description.Value; }
        }

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

            this.WhenAny(x => x.PackageMetadata, x => x.Value.ExtractTitle())
                .ToProperty(this, x => x.Title);

            this.WhenAny(x => x.PackageMetadata, v => v.Value)
                .Select(x => x != null ? x.Description : String.Empty)
                .ToProperty(this, x => x.Description);
        }
    }
}
