$scriptPath = Split-Path $MyInvocation.MyCommand.Path
$rootDirectory = join-path $scriptPath ..
$binFolder = join-path $scriptPath "..\bin\"
$nugetExe = join-path $scriptPath "..\src\.nuget\NuGet.exe"

$packages = Get-ChildItem $binFolder\*.nupkg -Exclude *.symbols.nupkg `
             | Sort { $_.Length }
$packages | foreach { 
 $file = $_.FullName
 Write-Host "Publishing file to NuGet:" $file
 . $nugetExe push $file -Timeout 600 -NonInteractive
}