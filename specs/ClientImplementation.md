# Client-side Library

To be able to meet the specifications of the "updates" section of the README
(especially the bits about 'No Reboots', 'Updates should be applied while the
app is running'), we have to be a bit more clever than "Stuff everything in a
folder, hit go".

### How can you replace DLLs while they're loaded? Impossible!

You can't. So, how can you do it? The basic trick that ClickOnce uses is, you
have a folder of EXEs and DLLs, and an Application Shortcut. When ClickOnce
goes to update its stuff, it builds a completely *new* folder of binaries,
then the last thing it does is rewrite the app shortcut to point to the new
folder.

So, to that end, the installation root really only needs to consist of two
folders:

```
  \packages
    MyCoolApp-1.0.nupkg
    MyCoolApp-1.1.nupkg.delta
    MyCoolApp-1.1.nupkg   ## Generated from 1.0+1.1-delta
  \app-[version]
```

Packages is effectively immutable, it simply consists of the packages we've
downloaded. This means however, that we need write-access to our own install
directory - this is fine for per-user installs, but if the user has installed
to Program Files, we'll need to come up with another solution. And that
solution is, "Only support per-user installs".

## The Update process, from start to finish

### Syncing the packages directory

The first thing that the NSync client will do to start the updates process, is
download the remote version of "Releases". Comparing this file to the Releases
file on disk will tell us whether an update is available.

Determining whether to use the delta packages or not will depend on the
download size - the updater will take the smaller of "latest full package" vs.
"Sum of all delta packages between current and latest". The updater makes a
choice, then fetches down all the files and checks them against the SHA1s in
the Releases file.

If the installer decided to do a Delta update, it will then use the Delta
updates against the existing Full package to build a new Full package.

### Installing a full update

Since we've done the prep work to create a new NuGet package from the deltas,
the actual update process only has to deal with full NuGet packages. This is
as simple as:

1. Extract the NuGet package to a temp dir
1. Move lib\net40 to \app-[newversion]
1. Rewrite the shortcut to point to \app-[newversion]

On next startup, we blow away \app-[version] since it's now the previous
version of the code.
