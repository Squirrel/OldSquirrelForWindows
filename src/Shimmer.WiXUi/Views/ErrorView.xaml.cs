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
    /// Interaction logic for ErrorView.xaml
    /// </summary>
    public partial class ErrorView : UserControl, IViewFor<IErrorViewModel>
    {
        public ErrorView()
        {
            InitializeComponent();

            this.WhenAny(x => x.ViewModel, x => x.Value)
                .Subscribe(x => ErrorMessage.Text = String.Format("{0}\n{1}", x.Error.ErrorMessage, x.Error.ErrorCauseOrResolution));
        }

        public IErrorViewModel ViewModel {
            get { return (IErrorViewModel)GetValue(ViewModelProperty); }
            set { SetValue(ViewModelProperty, value); }
        }
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(IErrorViewModel), typeof(ErrorView), new PropertyMetadata(null));

        object IViewFor.ViewModel {
            get { return ViewModel; }
            set { ViewModel = (IErrorViewModel) value; }
        }
    }
}
