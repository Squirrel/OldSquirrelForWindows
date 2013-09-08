param(
    [ValidateSet('FullBuild', 'RunUnitTests', 'RunIntegrationTests', 'Build', 'Clean')]
    [string]
    $build = "Build"
    ,
    [ValidateSet('Debug', 'Release')]
    [string]
    $config = "Release"
    ,
    [string]
    $MSBuildVerbosity = "quiet"
)

Set-StrictMode -Version Latest

$scriptPath = Split-Path $MyInvocation.MyCommand.Path
$rootDirectory = join-path $scriptPath ..
$srcFolder = join-path $scriptPath "..\src"

$projFile = join-path $srcFolder Shimmer.sln
$nugetExe = join-path $srcFolder .nuget\NuGet.exe

. $nugetExe config -Set Verbosity=quiet

. $nugetExe restore $projFile

& "$(get-content env:windir)\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" $projFile /t:$build /p:Configuration=$config /verbosity:$MSBuildVerbosity

& $scriptPath\Run-UnitTests.ps1