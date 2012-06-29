using System;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;
using ReactiveUI;
using ReactiveUI.Routing;
using TinyIoC;

namespace Shimmer.ViewModels.WiXUi
{
    public interface IAppBootstrapper : IScreen { }

    public class AppBootstrapper : ReactiveObject, IAppBootstrapper
    {
        public IRoutingState Router { get; protected set; }
        public static TinyIoCContainer Kernel { get; protected set; }

        public AppBootstrapper(TinyIoCContainer testKernel = null, IRoutingState router = null)
        {
            Kernel = testKernel ?? createDefaultKernel();
            Kernel.Register<IAppBootstrapper>(this).AsSingleton();
            Kernel.Register<IScreen>(this);

            Router = router ?? new RoutingState();

            RxApp.ConfigureServiceLocator(
                (type, contract) => Kernel.Resolve(type, contract),
                (type, contract) => Kernel.ResolveAll(type));
       }

        TinyIoCContainer createDefaultKernel()
        {
            var ret = new TinyIoCContainer();

#if 0
            ret.Bind<IWelcomeViewModel>().To<WelcomeViewModel>();
            ret.Bind<IPlayViewModel>().To<PlayViewModel>();
            ret.Bind<IBackgroundTaskHostViewModel>().To<PlayViewModel>();
            ret.Bind<ISearchViewModel>().To<SearchViewModel>();
            ret.Bind<IViewForViewModel<WelcomeViewModel>>().To<WelcomeView>();
            ret.Bind<IViewForViewModel<PlayViewModel>>().To<PlayView>();
            ret.Bind<IViewForViewModel<SearchViewModel>>().To<SearchView>();
            ret.Bind<IViewForViewModel<SongTileViewModel>>().To<SongTileView>().InTransientScope();

#if DEBUG
            var testBlobCache = new TestBlobCache();
            ret.Bind<IBlobCache>().ToConstant(testBlobCache).Named("LocalMachine");
            ret.Bind<IBlobCache>().ToConstant(testBlobCache).Named("UserAccount");
            ret.Bind<ISecureBlobCache>().ToConstant(testBlobCache);
#else
            ret.Bind<ISecureBlobCache>().ToConstant(BlobCache.Secure);
            ret.Bind<IBlobCache>().ToConstant(BlobCache.LocalMachine).Named("LocalMachine");
            ret.Bind<IBlobCache>().ToConstant(BlobCache.UserAccount).Named("UserAccount");
#endif
#endif

            return ret;
        }
    }
}