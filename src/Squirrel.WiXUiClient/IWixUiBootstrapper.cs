using System;
using ReactiveUI.Routing;
using Squirrel.Core;

namespace Squirrel.Client.WiXUi
{
    public interface IWixUiBootstrapper : IScreen
    {
        IWiXEvents WiXEvents { get; }
    }
}
