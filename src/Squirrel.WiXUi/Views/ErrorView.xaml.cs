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

            this.WhenAny(x => x.ViewModel.Error, x => x.Value)
                .Where(x => x != null)
                .Select(x => String.Format("{0}\n{1}", x.ErrorMessage, x.ErrorCauseOrResolution))
                .BindTo(this, x => x.ErrorMessage.Text);

            this.BindCommand(ViewModel, x => x.Shutdown, x => x.Shutdown);
            this.BindCommand(ViewModel, x => x.OpenLogsFolder, x => x.LogsCommand);
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
