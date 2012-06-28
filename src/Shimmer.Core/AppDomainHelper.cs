using System;
using System.Security;
using System.Security.Permissions;

namespace Shimmer.Core
{
    public static class AppDomainHelper
    {
        public static TOut ExecuteInNewAppDomain<TIn, TOut>(TIn input, Func<TIn, TOut> method)
        {
            return runInNewAppDomain(input, (runner, arg) => runner.Execute(arg, method));
        }

        public static void ExecuteActionInNewAppDomain<TIn>(TIn input, Action<TIn> method)
        {
            runInNewAppDomain<TIn, object>(input, (runner, arg) =>
            {
                runner.Execute(arg, method);
                return null;
            });
        }

        static TOut runInNewAppDomain<TIn, TOut>(TIn input, Func<MethodRunner, TIn, TOut> method)
        {
            var appDomainSetup = new AppDomainSetup
                                 {
                                     ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase,
                                     ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile,
                                     ApplicationName = AppDomain.CurrentDomain.SetupInformation.ApplicationName,
                                     LoaderOptimization = LoaderOptimization.MultiDomainHost
                                 };

            var permissionSet = new PermissionSet(PermissionState.Unrestricted);
            permissionSet.AddPermission(new SecurityPermission(SecurityPermissionFlag.Execution));

            AppDomain appDomain = null;
            try {
                appDomain = AppDomain.CreateDomain(
                    "AppDomainHelper.ExecuteInNewAppDomain", // FriendlyName
                    null,
                    appDomainSetup,
                    permissionSet,
                    null);

                return method(createMethodRunner(appDomain), input);
            }
            finally {
                if (appDomain != null) {
                    AppDomain.Unload(appDomain);
                }
            }
        }

        static MethodRunner createMethodRunner(AppDomain appDomain)
        {
            var type = typeof (MethodRunner);
            string assemblyName = type.Assembly.FullName;
            string typeName = type.FullName ?? "MethodRunner";
            return (MethodRunner)appDomain.CreateInstanceAndUnwrap(assemblyName, typeName);
        }
    }
}