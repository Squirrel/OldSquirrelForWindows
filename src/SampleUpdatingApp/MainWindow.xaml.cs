using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ReactiveUI;
using ReactiveUI.Xaml;
using Shimmer.Client;

namespace SampleUpdatingApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            DataContext = new MainWindowViewModel();

            InitializeComponent();
        }
    }

    public class MainWindowViewModel : ReactiveObject
    {
        string _UpdatePath;
        public string UpdatePath {
            get { return _UpdatePath; }
            set { this.RaiseAndSetIfChanged(x => x.UpdatePath, value); }
        }

        UpdateInfo _UpdateInfo;
        public UpdateInfo UpdateInfo {
            get { return _UpdateInfo; }
            set { this.RaiseAndSetIfChanged(x => x.UpdateInfo, value); }
        }

        UpdateInfo _DownloadedUpdateInfo;
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
                .Subscribe(x => updateManager = new UpdateManager(UpdatePath, "SampleUpdatingApp"));

            CheckForUpdate = new ReactiveAsyncCommand(noneInFlight);
            CheckForUpdate.RegisterAsyncFunction(_ => {
                using (updateManager.AcquireUpdateLock()) {
                    return updateManager.CheckForUpdate().First();
                }
            }).Subscribe(x => { UpdateInfo = x; DownloadedUpdateInfo = null; });

            DownloadReleases = new ReactiveAsyncCommand(noneInFlight.Where(_ => UpdateInfo != null));
            DownloadReleases.RegisterAsyncFunction(_ => {
                using (updateManager.AcquireUpdateLock()) {
                    return updateManager.DownloadReleases(UpdateInfo.ReleasesToApply).First();
                }
            }).Subscribe(_ => DownloadedUpdateInfo = UpdateInfo);

            ApplyReleases = new ReactiveAsyncCommand(noneInFlight.Where(_ => DownloadedUpdateInfo != null));
            ApplyReleases.RegisterAsyncFunction(_ => {
                using (updateManager.AcquireUpdateLock()) {
                    return updateManager.ApplyReleases(DownloadedUpdateInfo).First();
                }
            });

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
