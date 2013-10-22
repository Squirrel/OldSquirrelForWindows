Set-PSDebug -Strict
$ErrorActionPreference = "Stop"

$scriptPath = split-path -parent $MyInvocation.MyCommand.Definition
$rootFolder = (Get-Item $scriptPath).Parent.FullName
$srcFolder = "$rootFolder\src"

git submodule init

$nuget = "$srcFolder\.nuget\nuget.exe"

. $nuget config -Set Verbosity=quiet
. $nuget restore "$srcFolder\Squirrel.sln" -OutputDirectory "$srcFolder\packages"
