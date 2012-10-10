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
using Shimmer.Client.WiXUi;
using Shimmer.WiXUi.ViewModels;

namespace Shimmer.WiXUi.Views
{
    /// <summary>
    /// Interaction logic for UninstallingView.xaml
    /// </summary>
    public partial class UninstallingView : UserControl, IViewFor<IUninstallingViewModel>
    {
        public UninstallingView()
        {
            InitializeComponent();

            this.OneWayBind(ViewModel, x => x.ProgressValue);
        }

        public IUninstallingViewModel ViewModel {
            get { return (IUninstallingViewModel)GetValue(ViewModelProperty); }
            set { SetValue(ViewModelProperty, value); }
        }
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(IUninstallingViewModel), typeof(UninstallingView), new PropertyMetadata(null));

        object IViewFor.ViewModel {
            get { return ViewModel; }
            set { ViewModel = (IUninstallingViewModel) value; }
        }
    }
}
