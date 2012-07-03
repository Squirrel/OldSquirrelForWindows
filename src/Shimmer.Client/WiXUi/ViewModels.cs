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
        IPackage PackageMetadata { get; }
        bool AlreadyInstalled { get; }
        ReactiveCommand ShouldProceed { get; }
    }

    public interface IInstallingViewModel : IRoutableViewModel
    {
        IPackage PackageMetadata { get; }
        IObserver<int> ProgressValue { get; set; }
    }

    public interface IUninstallingViewModel : IRoutableViewModel
    {
        IPackage PackageMetadata { get; }
        IObserver<int> ProgressValue { get; set; }
    }

    public interface IErrorViewModel : IRoutableViewModel
    {
        IPackage PackageMetadata { get; }
        UserError Error { get; set; }
    }
}