using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Authentication;
using Shimmer.Client;

namespace ShimmerIAppUpdateTestTarget
{
    // NB: CAUTION!! If you change this code, you *must* Rebuild All in order 
    // to see the changes you've made - since VS doesn't know that this is
    // a dependency (and we have to keep it that way or else we can't trust that
    // we're not just finding this assembly via the reference and not the actual
    // discovery / loading of the EXE), you'll get old versions of this file 
    // running in your tests then pull your hair out wondering why stuff doesn't
    // work.
    public class TestAppSetup : IAppSetup
    {
        public string Target {
            get { return Assembly.GetExecutingAssembly().Location; }
        }

        public bool LaunchOnSetup {
            get { return true; }
        }

        public IEnumerable<ShortcutCreationRequest> GetAppShortcutList()
        {
            if (shouldThrow()) {
                throw new IOException();
            }

            var shortCutDir = getEnvVar("ShortcutDir");
            if (String.IsNullOrWhiteSpace(shortCutDir)) {
                return Enumerable.Empty<ShortcutCreationRequest>();
            }

            var ret = new ShortcutCreationRequest {
                Arguments = "--foo",
                Description = "A test app",
                CreationLocation = ShortcutCreationLocation.Custom,
                CustomLocation = shortCutDir,
                IconLibrary = Assembly.GetExecutingAssembly().Location,
                IconIndex = 0,
                TargetPath = Assembly.GetExecutingAssembly().Location,
                Title = "Foo",
            };

            return new[] { ret };
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