using System;
using Squirrel.Core;
using System.ComponentModel;
using ReactiveUIMicro.Routing;

namespace Squirrel.Client.WiXUi
{
    public interface IWixUiBootstrapper : IScreen
    {
        IWiXEvents WiXEvents { get; }
    }
}
