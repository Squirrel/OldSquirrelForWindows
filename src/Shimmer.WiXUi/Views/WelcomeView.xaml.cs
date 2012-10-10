using System;
using System.Collections.Generic;
using System.Linq;
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
using Shimmer.Client.WiXUi;
using Shimmer.WiXUi.ViewModels;

namespace Shimmer.WiXUi.Views
{
    /// <summary>
    /// Interaction logic for WelcomeView.xaml
    /// </summary>
    public partial class WelcomeView : UserControl, IWelcomeView
    {
        public WelcomeView()
        {
            InitializeComponent();

            this.OneWayBind(ViewModel, x => x.PackageMetadata.Title);
            this.OneWayBind(ViewModel, x => x.PackageMetadata.Title);
            this.BindCommand(ViewModel, x => x.ShouldProceed);
        }

        public IWelcomeViewModel ViewModel {
            get { return (IWelcomeViewModel)GetValue(ViewModelProperty); }
            set { SetValue(ViewModelProperty, value); }
        }
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(IWelcomeViewModel), typeof(WelcomeView), new PropertyMetadata(null));

        object IViewFor.ViewModel {
            get { return ViewModel; }
            set { ViewModel = (IWelcomeViewModel) value; }
        }

    }
}
