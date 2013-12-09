using System;
using System.Windows;
using ReactiveUIMicro;
using ReactiveUIMicro.Xaml;
using Squirrel.WiXUi.ViewModels;

namespace Squirrel.WiXUi.Views
{
    public partial class WelcomeView : IViewFor<WelcomeViewModel>
    {
        public WelcomeView()
        {
            InitializeComponent();

            this.OneWayBind(ViewModel, x => x.Title, x => x.Title.Text);
            this.OneWayBind(ViewModel, x => x.Description, x => x.Description.Text);
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
