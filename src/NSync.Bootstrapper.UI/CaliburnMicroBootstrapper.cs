using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autofac;
using Caliburn.Micro;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;

namespace SampleApp.BA
{
    public class CaliburnMicroBootstrapper : Bootstrapper<ShellViewModel>
    {
        private readonly BootstrapperApplication ba;
        private IContainer container;

        public CaliburnMicroBootstrapper(BootstrapperApplication ba)
        {
            LogManager.GetLog = t => new WixLog(ba.Engine, t);

            this.ba = ba;

            SetupContainer();
        }

        private void SetupContainer()
        {
            ContainerBuilder builder = new ContainerBuilder();

            RegisterCaliburnMicro(builder);

            builder.RegisterInstance<BootstrapperApplication>(ba).SingleInstance();

            container = builder.Build();
        }

        private void RegisterCaliburnMicro(ContainerBuilder builder)
        {
            //  register view models
            builder.RegisterAssemblyTypes(AssemblySource.Instance.ToArray())
                //  must be a type with a name that ends with ViewModel
              .Where(type => type.Name.EndsWith("ViewModel"))
                //  registered as self
              .AsSelf()
                //  always create a new one
              .InstancePerDependency();

            //  register views
            builder.RegisterAssemblyTypes(AssemblySource.Instance.ToArray())
                //  must be a type with a name that ends with View
              .Where(type => type.Name.EndsWith("View"))
                //  registered as self
              .AsSelf()
                //  always create a new one
              .InstancePerDependency();

            //  register the single window manager for this container
            builder.Register<IWindowManager>(c => new WindowManager()).InstancePerLifetimeScope();
            //  register the single event aggregator for this container
            builder.Register<IEventAggregator>(c => new EventAggregator()).InstancePerLifetimeScope();
        }

        protected override object GetInstance(Type service, string key)
        {
            object instance;
            if (string.IsNullOrWhiteSpace(key))
            {
                if (container.TryResolve(service, out instance))
                    return instance;
            }
            else
            {
                if (container.TryResolveNamed(key, service, out instance))
                    return instance;
            }
            throw new Exception(string.Format("Could not locate any instances of contract {0}.", key ?? service.Name));
        }

        protected override IEnumerable<object> GetAllInstances(Type service)
        {
            return container.Resolve(typeof(IEnumerable<>).MakeGenericType(service)) as IEnumerable<object>;
        }

        protected override void BuildUp(object instance)
        {
            container.InjectProperties(instance);
        }

        protected override IEnumerable<System.Reflection.Assembly> SelectAssemblies()
        {
            return new[] { Assembly.GetExecutingAssembly() };
        }
    }
}
