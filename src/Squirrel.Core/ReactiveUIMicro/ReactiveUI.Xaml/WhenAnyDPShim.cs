using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Text;
using System.Linq.Expressions;

#if WINRT
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
#else
using System.Windows;
using System.Windows.Data;
#endif

namespace ReactiveUIMicro
{
    public static class WhenAnyDPShim
    {
        public static IObservable<TVal> WhenAnyDP<TSender, TTarget, TVal>(this TSender This, Expression<Func<TSender, TTarget>> property, Func<IObservedChange<TSender, TTarget>, TVal> selector)
            where TSender : DependencyObject
        {
            var propName = Reflection.SimpleExpressionToPropertyName(property);
            var getter = Reflection.GetValueFetcherForProperty<TSender>(propName);
            var obsForProp = new DependencyObjectObservableForProperty();

            return obsForProp.GetNotificationForProperty(This, propName)
                .Select(_ => new ObservedChange<TSender, TTarget>() {
                    Sender = This,
                    PropertyName = propName,
                    Value = (TTarget)getter(This),
                })
                .StartWith(new ObservedChange<TSender, TTarget>() { Sender = This, PropertyName = propName, Value = (TTarget)getter(This) })
                .Select(selector);
        }
    }

    class DependencyObjectObservableForProperty
    {
        public int GetAffinityForObject(Type type, bool beforeChanged = false)
        {
            return typeof(DependencyObject).IsAssignableFrom(type) ? 4 : 0;
        }

        static readonly Dictionary<Type, DependencyProperty> attachedProperties = new Dictionary<Type, DependencyProperty>();
        static readonly Dictionary<object, Tuple<Subject<object>, RefcountDisposeWrapper>> subjects = new Dictionary<object, Tuple<Subject<object>, RefcountDisposeWrapper>>();

        public IObservable<IObservedChange<object, object>> GetNotificationForProperty(object sender, string propertyName, bool beforeChanged = false)
        {
            Contract.Requires(sender != null && sender is DependencyObject);

            if (beforeChanged == true) return null;

            var dobj = sender as DependencyObject;
            var type = dobj.GetType();

            // Look for the DependencyProperty attached to this property name
#if WINRT
            var pi = type.GetProperty(propertyName + "Property", BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (pi != null) {
                goto itWorks;
            }
#endif

            var fi = type.GetField(propertyName + "Property", BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);

#if !WINRT && !SILVERLIGHT
            return Observable.Create<IObservedChange<object, object>>(subj =>
            {
                var dp = (DependencyProperty) fi.GetValue(null);
                var dpd = DependencyPropertyDescriptor.FromProperty(dp, type);
                var ev = new EventHandler((o, e) => subj.OnNext(new ObservedChange<object, object>() {Sender = sender, PropertyName = propertyName,}));
                dpd.AddValueChanged(sender, ev);

                return Disposable.Create(() => dpd.RemoveValueChanged(sender, ev));
            });
#else

        itWorks:
            return Observable.Create<IObservedChange<object, object>>(subj => {
                DependencyProperty attachedProp;

                if (!attachedProperties.ContainsKey(type)) {
                    // NB: There is no way to unregister an attached property, 
                    // we just have to leak it. Luckily it's per-type, so it's
                    // not *that* bad.
                    attachedProp = DependencyProperty.RegisterAttached(
                        "ListenAttached" + propertyName + this.GetHashCode().ToString("{0:x}"),
                        typeof(object), type,
                        new PropertyMetadata(null, (o,e) => subjects[o].Item1.OnNext(o)));
                    attachedProperties[type] = attachedProp;
                } else {
                    attachedProp = attachedProperties[type];
                }

                // Here's the idea for this cracked-out code:
                //
                // The reason we're doing all of this is that we can only 
                // create a single binding between a DependencyObject and its 
                // attached property, yet we could have multiple people 
                // interested in this property. We should only drop the actual
                // Binding once nobody is listening anymore.
                if (!subjects.ContainsKey(sender)) {
                    var disposer = new RefcountDisposeWrapper(
                        Disposable.Create(() => {
#if !SILVERLIGHT && !WINRT
                            // XXX: Apparently it's simply impossible to unset a binding in SL :-/
                            BindingOperations.ClearBinding(dobj, attachedProp);
#endif
                            subjects.Remove(dobj);
                        }));

                    subjects[sender] = Tuple.Create(new Subject<object>(), disposer);

                    var b = new Binding() { Source = dobj, Path = new PropertyPath(propertyName) };
                    BindingOperations.SetBinding(dobj, attachedProp, b);
                } else {
                    subjects[sender].Item2.AddRef();
                }

                var disp = subjects[sender].Item1
                    .Select(x => (IObservedChange<object, object>) new ObservedChange<object, object>() { Sender = x, PropertyName = propertyName })
                    .Subscribe(subj);

                return Disposable.Create(() => {
                    disp.Dispose();
                    subjects[sender].Item2.Release();
                });
            });
#endif
        }
    }
}