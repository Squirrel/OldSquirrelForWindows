using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Authentication;
using Shimmer.Client;

namespace ShimmerIAppUpdateTestTarget
{
    public class TestAppSetup : IAppSetup
    {
        public IEnumerable<ShortcutCreationRequest> GetAppShortcutList()
        {
            if (shouldThrow()) {
                throw new IOException();
            }

            return Enumerable.Empty<ShortcutCreationRequest>();
        }

        public void OnAppInstall()
        {
            if (shouldThrow()) {
                throw new FileNotFoundException();
            }

            setEnvVar("AppInstall_Called", 1);
        }

        public void OnAppUninstall()
        {
            if (shouldThrow()) {
                throw new AuthenticationException();
            }

            setEnvVar("AppUninstall_Called", 1);
        }

        public void OnVersionInstalled(Version versionBeingInstalled)
        {
            if (shouldThrow()) {
                throw new UnauthorizedAccessException();
            }

            setEnvVar("VersionInstalled_Called", versionBeingInstalled);
        }

        public void OnVersionUninstalling(Version versionBeingUninstalled)
        {
            if (shouldThrow()) {
                Console.WriteLine(((String) null).Length);
            }

            setEnvVar("VersionInstalling_Called", versionBeingUninstalled);
        }

        bool shouldThrow()
        {
            return getEnvVar("ShouldThrow") != null;
        }

        static string getEnvVar(string name)
        {
            return Environment.GetEnvironmentVariable(String.Format("__IAPPSETUP_TEST_{0}", name.ToUpperInvariant()));
        }

        static void setEnvVar(string name, object val)
        {
            Environment.SetEnvironmentVariable(String.Format("__IAPPSETUP_TEST_{0}", name.ToUpperInvariant()), val.ToString(), EnvironmentVariableTarget.Process);
        }
    }
}