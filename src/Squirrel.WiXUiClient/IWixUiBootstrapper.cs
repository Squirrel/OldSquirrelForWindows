using System;
using Squirrel.Core;
using System.ComponentModel;

namespace Squirrel.Client.WiXUi
{
    public interface IWixUiBootstrapper : IScreen
    {
        IWiXEvents WiXEvents { get; }
    }
}
