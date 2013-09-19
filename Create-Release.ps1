param(
    [ValidateSet('Rebuild','Build', 'Clean')]
    [string]$build = "Rebuild",
    
    [ValidateSet('Debug', 'Release')]
    [string]$config = "Release",

    [bool] $BumpVersion = $true
)

function Write-Diagnostic {
    param([string]$message)

    Write-Host
    Write-Host $message -ForegroundColor Green
    Write-Host
}

$rootFolder = split-path -parent $MyInvocation.MyCommand.Definition

$scriptsFolder = Join-Path $rootFolder "script"

$binaries = "$rootFolder\bin\"

if (Test-Path $binaries) { Remove-Item $binaries -Recurse -Force }

Write-Diagnostic  "Bootstrapping environment"

. $scriptsFolder\bootstrap.ps1
if ($BumpVersion) {
    Write-Diagnostic  "Increment version of libraries"
    . $scriptsFolder\Bump-Version.ps1 -Increment Patch
}

Write-Diagnostic "Building solution"

. $scriptsFolder\Build-Solution.ps1 -Project "$rootFolder\src\Shimmer.sln" `
                                    -Build $build -Config $config

Write-Diagnostic "Recreating the CreateReleasePackage package"

Write-Host "Because of a limitation in the auto-generated NuGet package"
Write-Host "I need to re-compile the CreateReleasePackage tool now"
Write-Host "passing the magic parameter -Tool so that the project reference"
Write-Host "goes away"

. $rootFolder\src\.nuget\NuGet.exe pack $rootFolder\src\CreateReleasePackage\CreateReleasePackage.csproj -Tool -OutputDirectory $rootFolder\bin\ -Verbosity quiet -NoPackageAnalysis

if (Test-Path $binaries) {
    Remove-Item "$rootFolder\bin\Shimmer.Tests.*.nupkg"
    Remove-Item "$rootFolder\bin\Shimmer.WiXUi.*.nupkg"
}

Write-Diagnostic "Running all the tests"

. $scriptsFolder\Run-UnitTests.ps1
. $scriptsFolder\Run-PowershellTests.ps1

rm ${env:LOCALAPPDATA}\Shimmer\ProjectWithContent*
rm ${env:LOCALAPPDATA}\Shimmer\SampleUpdatingApp*
rm ${env:LOCALAPPDATA}\Shimmer\theApp*