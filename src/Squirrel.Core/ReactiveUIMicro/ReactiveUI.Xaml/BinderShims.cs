using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Windows;

namespace ReactiveUIMicro.Xaml
{
    public static class BinderShims
    {
        public static IObservable<TRet> WhenAnyVM<TViewModel, TTarget, TRet>(this IViewFor<TViewModel> This, Expression<Func<TViewModel, TTarget>> propName, Func<IObservedChange<TViewModel, TTarget>, TRet> selector)
            where TViewModel : class, IReactiveNotifyPropertyChanged
        {
            var depObj = This as DependencyObject;
            return depObj.WhenAnyDP<DependencyObject, TViewModel, TViewModel>("ViewModel", x => x.Value)
                .Select(x => x.WhenAny(propName, selector)).Switch();
        }
    }
}