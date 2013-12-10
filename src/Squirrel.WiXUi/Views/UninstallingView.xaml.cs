using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
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
using ReactiveUIMicro;
using Squirrel.Client.WiXUi;
using Squirrel.WiXUi.ViewModels;

namespace Squirrel.WiXUi.Views
{
    /// <summary>
    /// Interaction logic for UninstallingView.xaml
    /// </summary>
    public partial class UninstallingView : UserControl, IViewFor<UninstallingViewModel>
    {
        public UninstallingView()
        {
            InitializeComponent();

            this.WhenAnyDP(x => x.ViewModel, x => x.Value)
                .Select(x => x.WhenAny(y => x.LatestProgress, y => (double) y.Value)).Switch()
                .ObserveOn(RxApp.DeferredScheduler) // XXX: WHYYYYY
                .Subscribe(x => ProgressValue.Value = x);
        }

        public UninstallingViewModel ViewModel {
            get { return (UninstallingViewModel)GetValue(ViewModelProperty); }
            set { SetValue(ViewModelProperty, value); }
        }
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(UninstallingViewModel), typeof(UninstallingView), new PropertyMetadata(null));

        object IViewFor.ViewModel {
            get { return ViewModel; }
            set { ViewModel = (UninstallingViewModel) value; }
        }
    }
}
