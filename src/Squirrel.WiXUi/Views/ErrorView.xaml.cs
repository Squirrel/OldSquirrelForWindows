using ReactiveUIMicro;
using ReactiveUIMicro.Xaml;
using Squirrel.WiXUi.ViewModels;
using System;
using System.Reactive.Linq;
using System.Windows;

namespace Squirrel.WiXUi.Views
{
    public partial class ErrorView : IViewFor<ErrorViewModel>
    {
        public ErrorView()
        {
            InitializeComponent();

            // TODO: when uninstall error occurs, set Title accordingly

            this.WhenAnyDP(x => x.ViewModel, x => x.Value)
                .Select(x => x.WhenAny(y => y.Error, y => y.Value)).Switch()
                .Where(x => x != null)
                .Select(x => String.Format("{0}\n{1}", x.ErrorMessage, x.ErrorCauseOrResolution))
                .Subscribe(x => ErrorMessage.Text = x);

            this.WhenAnyVM(x => x.Shutdown, x => x.Value).Subscribe(x => Shutdown.Command = x);
            this.WhenAnyVM(x => x.OpenLogsFolder, x => x.Value).Subscribe(x => LogsCommand.Command = x);
        }

        public ErrorViewModel ViewModel {
            get { return (ErrorViewModel)GetValue(ViewModelProperty); }
            set { SetValue(ViewModelProperty, value); }
        }
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(ErrorViewModel), typeof(ErrorView), new PropertyMetadata(null));

        object IViewFor.ViewModel {
            get { return ViewModel; }
            set { ViewModel = (ErrorViewModel) value; }
        }
    }
}
