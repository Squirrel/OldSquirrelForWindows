using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;
using Shimmer.WiXUi;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Shimmer.WiXUi")]
[assembly: AssemblyDescription("Dont Use This Package")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("GitHub")]
[assembly: AssemblyProduct("Shimmer.WiXUi")]
[assembly: AssemblyCopyright("Copyright ©  2012")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("e5953795-e3f1-47a2-a966-1ddf0076ca0c")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("0.6.16")]
[assembly: AssemblyFileVersion("0.6.16")]
[assembly: AssemblyInformationalVersion("0.6.16")]

[assembly:BootstrapperApplication(typeof(App))]
