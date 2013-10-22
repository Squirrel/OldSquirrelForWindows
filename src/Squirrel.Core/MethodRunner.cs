using System;

namespace Shimmer.Core
{
    /// <summary>
    /// Container object used to execute code in another app domain
    /// </summary>
    internal class MethodRunner : MarshalByRefObject
    {
        public TOut Execute<TIn, TOut>(TIn input, Func<TIn, TOut> method)
        {
            return method(input);
        }

        public void Execute<TIn>(TIn input, Action<TIn> method)
        {
            method(input);
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }
    }
}