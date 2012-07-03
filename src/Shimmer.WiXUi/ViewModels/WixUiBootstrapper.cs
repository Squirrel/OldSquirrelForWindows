using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ReactiveUI;
using ReactiveUI.Routing;
using Shimmer.Client;
using Shimmer.Client.WiXUi;
using TinyIoC;

namespace Shimmer.WiXUi.ViewModels
{
    public class WixUiBootstrapper : ReactiveObject, IWixUiBootstrapper
    {
        public IRoutingState Router { get; protected set; }
        public IWiXEvents WiXEvents { get; protected set; }
        public static TinyIoCContainer Kernel { get; protected set; }

        public WixUiBootstrapper(IWiXEvents wixEvents, TinyIoCContainer testKernel = null, IRoutingState router = null)
        {
            Kernel = testKernel ?? createDefaultKernel();
            Kernel.Register<IWixUiBootstrapper>(this).AsSingleton();
            Kernel.Register<IScreen>(this);
            Kernel.Register(wixEvents);

            Router = router ?? new RoutingState();
            WiXEvents = wixEvents;

            registerExtensionDlls(Kernel);

            RxApp.ConfigureServiceLocator(
                (type, contract) => Kernel.Resolve(type, contract),
                (type, contract) => Kernel.ResolveAll(type));
       }

        void registerExtensionDlls(TinyIoCContainer kernel)
        {
            var di = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            var extensions = di.GetFiles("*.dll")
                .Where(x => x.FullName != Assembly.GetExecutingAssembly().Location)
                .SelectMany(x => {
                    try {
                        return new[] {Assembly.LoadFile(x.FullName)};
                    } catch (Exception ex) {
                        this.Log().WarnException("Couldn't load " + x.Name, ex);
                        return Enumerable.Empty<Assembly>();
                    }
                })
                .SelectMany(x => x.GetModules()).SelectMany(x => x.GetTypes())
                .Where(x => typeof(IWiXCustomUi).IsAssignableFrom(x) && !x.IsAbstract)
                .SelectMany(x => {
                    try {
                        return new[] {(IWiXCustomUi) Activator.CreateInstance(x)};
                    } catch (Exception ex) {
                        this.Log().WarnException("Couldn't create instance: " + x.FullName, ex);
                        return Enumerable.Empty<IWiXCustomUi>();
                    }
                });

            foreach (var extension in extensions) {
                extension.RegisterTypes(kernel);
            }
        }

        TinyIoCContainer createDefaultKernel()
        {
            var ret = new TinyIoCContainer();
            return ret;
        }
    }
}