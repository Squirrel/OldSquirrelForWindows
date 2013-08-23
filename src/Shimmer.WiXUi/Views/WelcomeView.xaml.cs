using System;
using System.Windows;
using ReactiveUI;
using ReactiveUI.Xaml;
using Shimmer.WiXUi.ViewModels;

namespace Shimmer.WiXUi.Views
{
    public partial class WelcomeView : IViewFor<WelcomeViewModel>
    {
        public WelcomeView()
        {
            InitializeComponent();

            this.OneWayBind(ViewModel, x => x.PackageMetadata.Title, x => x.Title.Text);
            this.BindCommand(ViewModel, x => x.ShouldProceed, x => x.ShouldProceed);
        }

        public WelcomeViewModel ViewModel {
            get { return (WelcomeViewModel)GetValue(ViewModelProperty); }
            set { SetValue(ViewModelProperty, value); }
        }

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(WelcomeViewModel), typeof(WelcomeView), new PropertyMetadata(null));

        object IViewFor.ViewModel {
            get { return ViewModel; }
            set { ViewModel = (WelcomeViewModel) value; }
        }
    }
}
