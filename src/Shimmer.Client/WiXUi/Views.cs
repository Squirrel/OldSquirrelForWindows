using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ReactiveUI;
using ReactiveUI.Routing;

namespace Shimmer.Client.WiXUi
{
    public interface IWelcomeView : IViewFor<IWelcomeViewModel>
    {
    }

    public interface IInstallingView : IViewFor<IInstallingViewModel>
    {
    }

    public interface IUninstallingView : IViewFor<IUninstallingViewModel>
    {
    }

    public interface IErrorView : IViewFor<IErrorViewModel>
    {
    }
}
