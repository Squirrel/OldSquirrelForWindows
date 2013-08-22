[CmdletBinding()]
param (
    [Parameter(Position=0, ValueFromPipeLine=$true, Mandatory=$false)]
    [string] $ProjectNameToBuild = '',
	[Parameter(Mandatory=$false)]
	[string] $SolutionDir,
	[Parameter(Mandatory=$false)]
	[string] $BuildDirectory
)

Set-PSDebug -Strict

$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$createReleasePackageExe = Join-Path $toolsDir "CreateReleasePackage.exe"
$wixDir = Join-Path $toolsDir "wix"
$support = Join-Path $toolsDir "support.ps1"
. $support

function Get-ProjectBuildOutputDir {
    param(
        [parameter(Mandatory = $true)]
        [string]$ProjectName
    )    

	$projDir = Get-ProjectPropertyValue $ProjectName 'FullPath'
	$proj = Get-Project $ProjectName
	$buildSuffix = $proj.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value
	Join-Path $projDir $buildSuffix
}

function Generate-TemplateFromPackage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$packageFile,
        [Parameter(Mandatory = $true)]
        [string]$templateFile
    )

	$resultFile = & $createReleasePackageExe --preprocess-template $templateFile $pkg.FullName
    $resultFile
}

function Create-ReleaseForProject {
	param(
		[Parameter(Mandatory = $true)]
		[string]$solutionDir,
		[Parameter(Mandatory = $true)]
		[string]$buildDirectory
	)

	$releaseDir = Join-Path $solutionDir "Releases"
	if (!(Test-Path $releaseDir)) { mkdir -p $releaseDir }

	echo "Creating Release for $solutionDir => $releaseDir`n"

	$nugetPackages = ls "$buildDirectory\*.nupkg" | ?{ $_.Name.EndsWith(".symbols.nupkg") -eq $false }

	if ($nugetPackages.length -eq 0) {
		throw "Shimmer couldn't find any .nupkg files in the folder $buildDirectory"
	}

	foreach($pkg in $nugetPackages) {
        $pkgFullName = $pkg.FullName
        echo "Found package $pkgFullName"

		$packageDir = Join-Path $solutionDir "packages"
		$fullRelease = & $createReleasePackageExe -o $releaseDir -p $packageDir $pkgFullName

        ## NB: For absolutely zero reason whatsoever, $fullRelease ends up being the full path Three times
        $fullRelease = $fullRelease.Split(" ")[0]
		echo "Full release file at $fullRelease"

        $candleTemplate = Generate-TemplateFromPackage $pkg.FullName "$toolsDir\template.wxs"
        $wixTemplate = Join-Path $buildDirectory "template.wxs"
        if (Test-Path $wixTemplate) { rm $wixTemplate | Out-Null }
        mv $candleTemplate $wixTemplate | Out-Null

		$defines = " -d`"ToolsDir=$toolsDir`"" + " -d`"NuGetFullPackage=$fullRelease`"" 

		$candleExe = Join-Path $wixDir "candle.exe"
		$lightExe = Join-Path $wixDir "light.exe"
		
		if (Test-Path "$buildDirectory\template.wixobj") {  rm "$buildDirectory\template.wixobj" | Out-Null }
        echo "Running candle.exe"
        & $candleExe "-d`"ToolsDir=$toolsDir`"" "-d`"ReleasesFile=$releaseDir\RELEASES`"" "-d`"NuGetFullPackage=$fullRelease`"" -out "$buildDirectory\template.wixobj" -arch x86 -ext "$wixDir\WixBalExtension.dll" -ext "$wixDir\WixUtilExtension.dll" $wixTemplate		
        echo "Running light.exe"		
        & $lightExe -out "$releaseDir\Setup.exe" -ext "$wixDir\WixBalExtension.dll" -ext "$wixDir\WixUtilExtension.dll" "$buildDirectory\template.wixobj"
	}
}

if (-not $ProjectNameToBuild) {
	$ProjectNameToBuild = (Get-Project).Name
}

if (-not $SolutionDir) {
	if (Test-Path variable:Dte) {
		$SolutionDir = Get-SolutionDir
	} else {
		throw "Cannot determine the Solution directory - either run this script`n" +
			  "inside the NuGet Package Console, or specify a directory as a parameter"
	}
}

if (Test-Path variable:Configuration) {
	$BuildDirectory = "$SolutionDir\bin\$Configuration"
}

if (-not $BuildDirectory) {
	if (Test-Path variable:Dte) { 
		$BuildDirectory = Get-ProjectBuildOutputDir $ProjectNameToBuild
	} else {
		throw "Cannot determine the Build directory - either run this script`n" +
			  "inside the NuGet Package Console, or specify a directory as a parameter"
	}
}

### DEBUG:
#$createReleasePackageExe = [IO.Path]::Combine($toolsDir, '..', 'bin', 'Debug', 'CreateReleasePackage.exe')
#$wixDir = [IO.Path]::Combine($toolsDir, '..', '..', '..', 'ext', 'wix')
### End DEBUG

$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Create-ReleaseForProject $SolutionDir $BuildDirectory