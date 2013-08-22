Set-PSDebug -Strict
$ErrorActionPreference = "Stop"

$scriptPath = split-path -parent $MyInvocation.MyCommand.Definition
$rootFolder = (Get-Item $scriptPath).Parent.FullName
$srcFolder = "$rootFolder\src"

Write-Host "Root folder is $rootFolder"

$nuget = "$srcFolder\.nuget\nuget.exe"
$items = Get-ChildItem -Path "$srcFolder" -Filter "packages.config" -Recurse

foreach ($item in $items)
{
   . $nuget install $item.FullName -OutputDirectory "$srcFolder\packages"
}