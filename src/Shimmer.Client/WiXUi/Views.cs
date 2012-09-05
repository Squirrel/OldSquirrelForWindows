using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ReactiveUI.Routing;

namespace Shimmer.Client.WiXUi
{
    public interface IWelcomeView : IViewForViewModel<IWelcomeViewModel>
    {
    }

    public interface IInstallingView : IViewForViewModel<IInstallingViewModel>
    {
    }

    public interface IUninstallingView : IViewForViewModel<IUninstallingViewModel>
    {
    }

    public interface IErrorView : IViewForViewModel<IErrorViewModel>
    {
    }
}
