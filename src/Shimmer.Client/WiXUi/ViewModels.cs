using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGet;
using ReactiveUI.Routing;
using ReactiveUI.Xaml;
using Shimmer.Core;

namespace Shimmer.Client.WiXUi
{
    public interface IWelcomeViewModel : IRoutableViewModel
    {
        IPackage PackageMetadata { get; set; }
        ReactiveCommand ShouldProceed { get; }
    }

    public interface IInstallingViewModel : IRoutableViewModel
    {
        IPackage PackageMetadata { get; set; }
        IObserver<int> ProgressValue { get; }
    }

    public interface IUninstallingViewModel : IRoutableViewModel
    {
        IPackage PackageMetadata { get; set; }
        IObserver<int> ProgressValue { get; }
    }

    public interface IErrorViewModel : IRoutableViewModel
    {
        IPackage PackageMetadata { get; set; }
        UserError Error { get; set; }
        ReactiveCommand Shutdown { get; }
    }
}