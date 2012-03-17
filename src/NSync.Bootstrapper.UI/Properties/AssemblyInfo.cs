using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;
using SampleApp.BA;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("SampleApp.BA")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Example")]
[assembly: AssemblyProduct("SampleApp.BA")]
[assembly: AssemblyCopyright("Copyright © Example 2012")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("637713d1-28d3-4135-9473-0c946cc8963c")]

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
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

// Identifies the class that derives from UserExperience and is the UX class that gets
// instantiated by the interop layer
[assembly: BootstrapperApplication(typeof(NSyncBootstrapper))]
