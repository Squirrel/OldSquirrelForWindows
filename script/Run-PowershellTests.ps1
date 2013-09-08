Set-StrictMode -Version Latest

$scriptPath = Split-Path $MyInvocation.MyCommand.Path
$rootDirectory = join-path $scriptPath ..

$currentDir = Get-Location

Set-Location "$rootDirectory\tests"

. $rootDirectory\ext\pester\bin\pester.bat

Set-Location $currentDir