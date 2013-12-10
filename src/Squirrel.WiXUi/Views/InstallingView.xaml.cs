using System;
using System.Windows;
using ReactiveUIMicro;
using ReactiveUIMicro.Xaml;
using Squirrel.WiXUi.ViewModels;

namespace Squirrel.WiXUi.Views
{
    public partial class InstallingView : IViewFor<InstallingViewModel>
    {
        public InstallingView()
        {
            InitializeComponent();

            this.WhenAnyVM(x => x.LatestProgress, x => x.Value).Subscribe(x => ProgressValue.Value = x);
            this.WhenAnyVM(x => x.Title, x => x.Value).Subscribe(x => Title.Text = x);
            this.WhenAnyVM(x => x.Description, x => x.Value).Subscribe(x => Description.Text = x);
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
