param(
    [ValidateSet('Rebuild','Build', 'Clean')]
    [string]
    $build = "Rebuild"
    ,
    [ValidateSet('Debug', 'Release')]
    [string]
    $config = "Debug"
    ,
    [string]
    $MSBuildVerbosity = "normal"
)


$rootFolder = split-path -parent $MyInvocation.MyCommand.Definition
$srcFolder = "$rootFolder\src"

$configFiles = Get-ChildItem -Path $srcFolder -Include "packages.config" -Recurse

foreach ($configFile in $configFiles)
{
   . "$srcFolder\.nuget\nuget.exe" install $configFile.FullName -OutputDirectory "$srcFolder\packages"
}


$msbuild = "$env:SystemRoot\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"

. $msbuild "$srcFolder\Shimmer.sln" /t:$build /p:Configuration=$config /verbosity:$MSBuildVerbosity