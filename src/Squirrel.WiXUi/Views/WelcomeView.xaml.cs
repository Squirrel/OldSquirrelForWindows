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

            this.WhenAnyVM(x => x.Title, x => x.Value).Subscribe(x => Title.Text = x);
            this.WhenAnyVM(x => x.Description, x => x.Value).Subscribe(x => Description.Text = x);
            this.WhenAnyVM(x => x.ShouldProceed, x => x.Value).Subscribe(x => ShouldProceed.Command = x);
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
