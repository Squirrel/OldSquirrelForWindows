using ReactiveUI.Routing;
using Shimmer.Core;

namespace Shimmer.Client
{
    public interface IWixUiBootstrapper : IScreen
    {
        IWiXEvents WiXEvents { get; }
    }
}