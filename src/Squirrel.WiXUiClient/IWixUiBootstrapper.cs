using System;
using ReactiveUI.Routing;
using Shimmer.Core;

namespace Shimmer.Client.WiXUi
{
    public interface IWixUiBootstrapper : IScreen
    {
        IWiXEvents WiXEvents { get; }
    }
}