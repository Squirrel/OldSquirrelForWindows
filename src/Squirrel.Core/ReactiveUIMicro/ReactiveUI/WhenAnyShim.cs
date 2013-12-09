using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Linq;
using System.Text;
using System.Linq.Expressions;

namespace ReactiveUIMicro
{
    public static class WhenAnyShim
    {
        public static IObservable<TVal> WhenAny<TSender, TTarget, TVal>(this TSender This, Expression<Func<TSender, TTarget>> property, Func<IObservedChange<TSender, TTarget>, TVal> selector)
            where TSender : IReactiveNotifyPropertyChanged
        {
            var propName = Reflection.SimpleExpressionToPropertyName(property);
            var getter = Reflection.GetValueFetcherForProperty<TSender>(propName);

            return This.Changed
                .Where(x => x.PropertyName == propName)
                .Select(_ => new ObservedChange<TSender, TTarget>() {
                    Sender = This,
                    PropertyName = propName,
                    Value = (TTarget)getter(This),
                })
                .StartWith(new ObservedChange<TSender, TTarget>() { Sender = This, PropertyName = propName, Value = (TTarget)getter(This) })
                .Select(selector);
        }
    }
}