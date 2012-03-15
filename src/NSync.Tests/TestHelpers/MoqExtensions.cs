using System.Reactive.Subjects;
using Moq;
using ReactiveUI;

namespace NSync.Tests.TestHelpers
{
    public static class MoqExtensions
    {
        public static Mock<T> SetupReactiveObjectProperties<T>(this Mock<T> mock) where T : class, IReactiveNotifyPropertyChanged
        {
            mock.Setup(x => x.Changed).Returns(new Subject<IObservedChange<object, object>>());
            mock.Setup(x => x.Changing).Returns(new Subject<IObservedChange<object, object>>());
            return mock;
        }
    }
}
