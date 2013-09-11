[CmdletBinding()]
param (
	[Parameter(Mandatory=$true)]
	[string] $SolutionDir,
	[Parameter(Mandatory=$true)]
	[string] $BuildDirectory
	[Parameter(Mandatory = $false)]
	[string]$ReleaseDirectory = Join-Path $SolutionDir "Releases"
)

Set-PSDebug -Strict

$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Import-Module (Join-Path $toolsDir "utilities.psm1")
Import-Module (Join-Path $toolsDir "commands.psm1")

Create-ReleaseForProject -SolutionDir $SolutionDir `
                         -BuildDirectory $BuildDirectory `
                         -ReleaseDirectory $ReleaseDirectory