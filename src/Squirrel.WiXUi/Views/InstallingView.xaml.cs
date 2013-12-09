using System;
using System.Windows;
using ReactiveUIMicro;
using Squirrel.WiXUi.ViewModels;

namespace Squirrel.WiXUi.Views
{
    public partial class InstallingView : IViewFor<InstallingViewModel>
    {
        public InstallingView()
        {
            InitializeComponent();

            this.OneWayBind(ViewModel, x => x.LatestProgress, x => x.ProgressValue.Value);
            this.OneWayBind(ViewModel, x => x.Title, x => x.Title.Text);
            this.OneWayBind(ViewModel, x => x.Description, x => x.Description.Text);
        }

        public InstallingViewModel ViewModel {
            get { return (InstallingViewModel)GetValue(ViewModelProperty); }
            set { SetValue(ViewModelProperty, value); }
        }
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(InstallingViewModel), typeof(InstallingView), new PropertyMetadata(null));

        object IViewFor.ViewModel {
            get { return ViewModel; }
            set { ViewModel = (InstallingViewModel) value; }
        }
    }
}
