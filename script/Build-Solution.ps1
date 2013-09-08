param(
    [string]
    $Project
    ,

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

& "$(get-content env:windir)\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" $Project /t:$build /p:Configuration=$config /verbosity:$MSBuildVerbosity
