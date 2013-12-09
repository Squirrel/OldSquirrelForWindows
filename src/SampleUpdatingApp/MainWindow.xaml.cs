using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUIMicro;
using ReactiveUIMicro.Xaml;
using Squirrel.Client;

namespace SampleUpdatingApp
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            DataContext = new MainWindowViewModel();

            InitializeComponent();
        }
    }

    public class MainWindowViewModel : ReactiveObject
    {
#pragma warning disable 649
        string _UpdatePath;
#pragma warning restore 649
        public string UpdatePath {
            get { return _UpdatePath; }
            set { this.RaiseAndSetIfChanged(x => x.UpdatePath, value); }
        }

#pragma warning disable 649
        UpdateInfo _UpdateInfo;
#pragma warning restore 649
        public UpdateInfo UpdateInfo {
            get { return _UpdateInfo; }
            set { this.RaiseAndSetIfChanged(x => x.UpdateInfo, value); }
        }

#pragma warning disable 649
        UpdateInfo _DownloadedUpdateInfo;
#pragma warning restore 649
        public UpdateInfo DownloadedUpdateInfo {
            get { return _DownloadedUpdateInfo; }
            set { this.RaiseAndSetIfChanged(x => x.DownloadedUpdateInfo, value); }
        }

        public ReactiveAsyncCommand CheckForUpdate { get; protected set; }
        public ReactiveAsyncCommand DownloadReleases { get; protected set; }
        public ReactiveAsyncCommand ApplyReleases { get; protected set; }

        public MainWindowViewModel()
        {
            var noneInFlight = new BehaviorSubject<bool>(false);
            var updateManager = default(UpdateManager);

            this.WhenAny(x => x.UpdatePath, x => x.Value)
                .Where(x => !String.IsNullOrWhiteSpace(x))
                .Throttle(TimeSpan.FromMilliseconds(700), RxApp.DeferredScheduler)
                .Subscribe(x => {
                    if (updateManager != null)  updateManager.Dispose();
                    updateManager = new UpdateManager(UpdatePath, "SampleUpdatingApp", FrameworkVersion.Net40);
                });

            CheckForUpdate = new ReactiveAsyncCommand(noneInFlight);
            CheckForUpdate.RegisterAsyncObservable(_ => updateManager.CheckForUpdate())
                .Subscribe(x => { UpdateInfo = x; DownloadedUpdateInfo = null; });

            DownloadReleases = new ReactiveAsyncCommand(noneInFlight.Where(_ => UpdateInfo != null));
            DownloadReleases.RegisterAsyncObservable(_ => updateManager.DownloadReleases(UpdateInfo.ReleasesToApply))
                .Subscribe(_ => DownloadedUpdateInfo = UpdateInfo);

            ApplyReleases = new ReactiveAsyncCommand(noneInFlight.Where(_ => DownloadedUpdateInfo != null));
            ApplyReleases.RegisterAsyncObservable(_ => updateManager.ApplyReleases(DownloadedUpdateInfo));

            Observable.CombineLatest(
                CheckForUpdate.ItemsInflight.StartWith(0),
                DownloadReleases.ItemsInflight.StartWith(0),
                ApplyReleases.ItemsInflight.StartWith(0),
                this.WhenAny(x => x.UpdatePath, _ => 0),
                (a, b, c, _) => a + b + c
            ).Select(x => x == 0 && UpdatePath != null).Multicast(noneInFlight).Connect();
        }
    }
}
