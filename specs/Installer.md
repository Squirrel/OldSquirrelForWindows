# Installer

Installer just installs `bootstrap.exe` whose job is to:

1. Run the bootstrapper to unpack the latest full NuGet package and finish
   initial install.
1. Execute the uninstaller code when WiX goes to remove us, and remove the App
   directory.

### So, on install:

1. WiX unpacks `bootstrap.exe` and runs it, and puts an entry in *Programs and
   Features*.
1. `bootstrap.exe` executes initial install using `Shimmer.Core` for the full
   NuGet package, doing the update in-place so the installer never needs to be
   rebuilt.  

### On Uninstall:

1. WiX gets notified about the uninstall, calls `bootstrap.exe` to do app
   uninstall via `Shimmer.Core`
1. WiX then blows away `bootstrap.exe`, the "real" installed app.

## Bootstrap UI

`bootstrap.exe` has an extremely simple UI when it does its work, it just pops
up, shows a progress bar, a-la Chrome Installer:

![](http://t0.gstatic.com/images?q=tbn:ANd9GcS_DuuEyOX1lfeo_jDetHLiE17pp_4M-Xerj2ieGEkvQQ4h83w57IL5KD6Kzw)

On Uninstall, there is no UI, it's solely in the background.


## Generating Bootstrap.exe and the WiX installer

The WiX install script is generated via a Mustache template, whose contents
are primarily populated via the generated NuGet release package. WiX will end
up installing `bootstrap.exe`, the latest NuGet package file, and a one-line
RELEASES file (meaning that what WiX installs is technically a valid Shimmer
remote update directory).
