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

$projFile = join-path $srcFolder Squirrel.sln
$nugetExe = join-path $srcFolder .nuget\NuGet.exe

. $nugetExe config -Set Verbosity=quiet

. $nugetExe restore $projFile

& $scriptPath\Build-Solution.ps1 -Project $projFile -Build $build -config $config -MSBuildVerbosity $MSBuildVerbosity

& $scriptPath\Run-UnitTests.ps1
