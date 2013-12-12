using ReactiveUIMicro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace Squirrel.Tests.Core.RxUIMicro
{
    public class TestViewModel : ReactiveObject
    {
        string someProperty;
        public string SomeProperty {
            get { return someProperty; }
            set { this.RaiseAndSetIfChanged(x => x.SomeProperty, ref someProperty, value); }
        }
    }

    public class TestView : UserControl, IViewFor<TestViewModel>
    {
        public TestViewModel ViewModel {
            get { return (TestViewModel)GetValue(ViewModelProperty); }
            set { SetValue(ViewModelProperty, value); }
        }
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(TestViewModel), typeof(TestView), new PropertyMetadata(null));

        object IViewFor.ViewModel {
            get { return ViewModel; }
            set { ViewModel = (TestViewModel) value; }
        }
    }

    public class WhenAnyTests
    {
        [Fact]
        public void WhenAnyForViewModelsShouldReportChanges()
        {
            var fixture = new TestViewModel();

            var output = new List<string>();
            fixture.WhenAny(x => x.SomeProperty, x => x.Value)
                .Subscribe(output.Add);

            Assert.Equal(1, output.Count);

            fixture.SomeProperty = "Bar";
            Assert.Equal(2, output.Count);
        }

        [Fact]
        public void WhenAnyForViewsShouldReportChanges()
        {
            var fixture = new TestView();

            var output = new List<TestViewModel>();
            fixture.WhenAnyDP(x => x.ViewModel, x => x.Value)
                .Subscribe(output.Add);

            Assert.Equal(1, output.Count);

            fixture.ViewModel = new TestViewModel();
            Assert.Equal(2, output.Count);
        }

        [Fact]
        public void WhenAnyChainingShouldReportChanges()
        {
            var fixture = new TestView();

            var output = new List<string>();
            fixture.WhenAnyDP(x => x.ViewModel, x => x.Value)
                .Where(x => x != null)
                .Select(x => x.WhenAny(y => y.SomeProperty, y => y.Value)).Switch()
                .Subscribe(output.Add);

            Assert.Equal(0, output.Count);

            fixture.ViewModel = new TestViewModel() { SomeProperty = "Foo", };
            Assert.Equal(1, output.Count);

            fixture.ViewModel.SomeProperty = "Bar";
            Assert.Equal(2, output.Count);
        }
    }
}
