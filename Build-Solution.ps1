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
    $MSBuildVerbosity = "quiet"
)


$rootFolder = split-path -parent $MyInvocation.MyCommand.Definition
$srcFolder = "$rootFolder\src"

$msbuild = "$env:SystemRoot\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"

. $msbuild "$srcFolder\Shimmer.sln" /t:$build /p:Configuration=$config /verbosity:$MSBuildVerbosity