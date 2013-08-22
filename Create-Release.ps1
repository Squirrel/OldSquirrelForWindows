param(
    [ValidateSet('Rebuild','Build', 'Clean')]
    [string]
    $build = "Rebuild"
    ,
    [ValidateSet('Debug', 'Release')]
    [string]
    $config = "Debug"
)

$rootFolder = split-path -parent $MyInvocation.MyCommand.Definition

$binaries = "$rootFolder\bin\"

if (Test-Path $binaries) { Remove-Item $binaries -Recurse -Force }

. $rootFolder\script\bootstrap.ps1
. $rootFolder\script\Bump-Version.ps1 -Increment Patch
. $rootFolder\Build-Solution.ps1 -Build $build -Config $config

if (Test-Path $binaries) {
    Remove-Item "$rootFolder\bin\Shimmer.Tests.*.nupkg"
    Remove-Item "$rootFolder\bin\Shimmer.WiXUi.*.nupkg"
}