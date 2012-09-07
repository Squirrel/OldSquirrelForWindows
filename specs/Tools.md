## Scenarios

At the end of the day, here's how a developer will use Shimmer:

1. Add the "Shimmer" package to your application
1. The post-install for Shimmer enables NuGet Package Build (i.e. flips the
   bits in NuGet.targets and enables NuGet Package Restore)
1. From the NuGet package console, run `Create-Release` - this builds the
   world, and you end up with `$SolutionDir/Releases` folder that has both a
   Shimmer release folder as well as a `Setup.exe`

## How does this work:

1. Call `$DTE` to build the current project, including the NuGet packages
1. Look at all of the projects which have references to `Shimmer.Client`
1. Look up the build output directory for those projects, run
   `CreateReleasePackage.exe` on all of the .nupkg files
1. Using the generated NuGet package, fill in the `Template.wxs` file
1. Create a temporary directory for the contents of the Setup.exe, copy in the
   `Shimmer.WiXUi.dll` as well as any DLL Project that references
   `Shimmer.Client.dll`
1. Run `Candle` and `Light` to generate a `Setup.exe`, which contains
   Shimmer.WiXUi.dll and friends, any custom UI DLLs, and the latest full
   `nupkg` file.
