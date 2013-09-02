param(
    [ValidateSet('Rebuild','Build', 'Clean')]
    [string]$build = "Rebuild",
    
    [ValidateSet('Debug', 'Release')]
    [string]$config = "Release",

    [bool] $BumpVersion = $true
)

$rootFolder = split-path -parent $MyInvocation.MyCommand.Definition

$binaries = "$rootFolder\bin\"

if (Test-Path $binaries) { Remove-Item $binaries -Recurse -Force }

. $rootFolder\script\bootstrap.ps1
if ($BumpVersion) {
    . $rootFolder\script\Bump-Version.ps1 -Increment Patch
}
. $rootFolder\Build-Solution.ps1 -Build $build -Config $config

Write-Host ""
Write-Host "Because of a limitation in the auto-generated NuGet package"
Write-Host "I need to re-compile the CreateReleasePackage tool now"
Write-Host "passing the magic parameter -Tool so that the project reference"
Write-Host "goes away"
WRite-Host ""

. $rootFolder\src\.nuget\NuGet.exe pack $rootFolder\src\CreateReleasePackage\CreateReleasePackage.csproj -Tool -OutputDirectory $rootFolder\bin\

if (Test-Path $binaries) {
    Remove-Item "$rootFolder\bin\Shimmer.Tests.*.nupkg"
    Remove-Item "$rootFolder\bin\Shimmer.WiXUi.*.nupkg"
}