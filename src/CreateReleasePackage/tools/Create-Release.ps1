[CmdletBinding()]
param (
    [Parameter(Position=0, ValueFromPipeLine=$true, Mandatory=$true)]
    [string] $Param_ProjectNameToBuild = ''
)

Set-PSDebug -Strict
#$ErrorActionPreference = "Stop"


$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Path
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

<# DEBUG:
function Get-ProjectBuildOutputDir {
    param(
        [parameter(Mandatory = $true)]
        [string]$ProjectName
    )

    "$toolsDir\..\..\SampleUpdatingApp\bin\Debug"
}
#>

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
		[string]$name
	)

	$buildDir = Get-ProjectBuildOutputDir $name

	echo "Creating Release for $name"

	$nugetPackages = ls "$buildDir\*.nupkg" | ?{ $_.Name.EndsWith(".symbols.nupkg") -eq $false }
	foreach($pkg in $nugetPackages) {
		$packageDir = Join-Path $solutionDir "packages"
		$fullRelease = & $createReleasePackageExe -o $releaseDir -p $packageDir $pkg.FullName 

        ## NB: For absolutely zero reason whatsoever, $fullRelease ends up being the full path Three times
        $fullRelease = $fullRelease.Split(" ")[0]

        $candleTemplate = Generate-TemplateFromPackage $pkg.FullName "$toolsDir\template.wxs"
        $wixTemplate = Join-Path $buildDir "template.wxs"
        rm $wixTemplate
        mv $candleTemplate $wixTemplate

		$pkgFullName = $pkg.FullName
		$defines = " -d`"ToolsDir=$toolsDir`"" + " -d`"NuGetFullPackage=$fullRelease`"" 

		$candleExe = Join-Path $wixDir "candle.exe"
		$lightExe = Join-Path $wixDir "light.exe"
		
		rm "$buildDir\template.wixobj"
        & $candleExe "-d`"ToolsDir=$toolsDir`"" "-d`"ReleasesFile=$releaseDir\RELEASES`"" "-d`"NuGetFullPackage=$fullRelease`"" -out "$buildDir\template.wixobj" -arch x86 -ext "$wixDir\WixBalExtension.dll" -ext "$wixDir\WixUtilExtension.dll" $wixTemplate		
		& $lightExe -out "$releaseDir\Setup.exe" -ext "$wixDir\WixBalExtension.dll" -ext "$wixDir\WixUtilExtension.dll" "$buildDir\template.wixobj"
	}
}

$solutionDir = Get-SolutionDir
##$solutionDir = "$toolsDir\..\.."

### DEBUG:
$createReleasePackageExe = [IO.Path]::Combine($solutionDir, 'CreateReleasePackage', 'bin', 'Debug', 'CreateReleasePackage.exe')
$wixDir = [IO.Path]::Combine($solutionDir, '..', 'ext', 'wix')
### End DEBUG

$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$releaseDir = Join-Path $solutionDir "Releases"
if ({ Test-Path $releaseDir } -eq $false) { mkdir -p $releaseDir }
	
Create-ReleaseForProject $Param_ProjectName

### DEBUG:
#Create-ReleaseForProject SampleUpdatingApp