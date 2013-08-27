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
    $MSBuildVerbosity = "normal"
)

Set-StrictMode -Version Latest

$scriptPath = Split-Path $MyInvocation.MyCommand.Path
$srcFolder = join-path $scriptPath "..\src"

$projFile = join-path $srcFolder Shimmer.sln
$nugetExe = join-path $srcFolder .nuget\NuGet.exe

$items = Get-ChildItem -Path "$srcFolder" -Filter "packages.config" -Recurse

foreach ($item in $items)
{
   . $nugetExe install $item.FullName -OutputDirectory "$srcFolder\packages"
}

& "$(get-content env:windir)\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" $projFile /t:$build /p:Configuration=$config /verbosity:$MSBuildVerbosity
