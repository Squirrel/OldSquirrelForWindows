using System;
using System.Linq.Expressions;
using System.Reactive.Subjects;
using System.Threading;
using Moq;
using ReactiveUIMicro;

namespace Squirrel.Tests.TestHelpers
{
    public static class MoqExtensions
    {
        public static Mock<T> SetupReactiveObjectProperties<T>(this Mock<T> mock) where T : class, IReactiveNotifyPropertyChanged
        {
            mock.Setup(x => x.Changed).Returns(new Subject<IObservedChange<object, object>>());
            mock.Setup(x => x.Changing).Returns(new Subject<IObservedChange<object, object>>());
            return mock;
        }

        public static void WaitUntil<T>(this Mock<T> mock, Expression<Action<T>> action) where T : class
        {
            var autoResetEvent = new AutoResetEvent(false);

            mock.Setup(action)
                .Callback(() => autoResetEvent.Set());

            autoResetEvent.WaitOne(TimeSpan.FromSeconds(10));
        }
    }
}
