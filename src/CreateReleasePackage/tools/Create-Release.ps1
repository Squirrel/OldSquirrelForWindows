param (
	[Parameter(Position=0, ValueFromPipeLine=$true)]
	[string] $Param_ProjectNameToBuild = '',
)

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

function Create-ReleaseForProject {
	param(
		[Parameter(Mandatory = $true)]
		[string]$name
	)

	$buildDir = Get-ProjectBuildOutputDir $name

	echo "Creating Release for $name"

	$nugetPackages = ls "$buildDir\*.nupkg"
	foreach($pkg in $nugetPackages) {
		$fullRelease = & $createReleasePackageExe -o $releaseDir $pkg.FullName 
		$vars = & $createReleasePackageExe --package-info $pkg.FullName

		## Eval in some constants, here's what gets defined:
		<#
			$NuGetPackage_Authors = 'Paul'
			$NuGetPackage_Description = 'Description'
			$NuGetPackage_IconUrl = ''
			$NuGetPackage_LicenseUrl = ''
			$NuGetPackage_ProjectUrl = ''
			$NuGetPackage_Summary = ''
			$NuGetPackage_Title = 'Shimmer.WiXUi'
			$NuGetPackage_Version = '1.0.0.0'
		#>

		$varNames = $vars.Split("`n'") | % { $_.Substring(0, $_.IndexOf(' ')) }
		foreach($expr in $vars.Split("`n")) {
			if ($expr.Length -gt 1) { invoke-expression $expr }
		}

		$pkgFullName = $pkg.FullName
		$defineList = $varNames | % { $e = invoke-expression $_;  [String]::Format("-d`"{0}={1}`"", $_.Substring(1), $e) }
		$defines = [String]::Join(" ", $defineList) + " -d`"ToolsDir=$toolsDir`"" + " -d`"NuGetFullPackage=$fullRelease`"" 

		$candleExe = Join-Path $wixDir "candle.exe"
		$lightExe = Join-Path $wixDir "light.exe"
		$wixTemplate = Join-Path $toolsDir "template.wxs"
		$extensions = "-ext `"$wixDir\WixBalExtension.dll`" -ext `"$wixDir\WixUtilExtension.exe`""

		& $candleExe "$defines -out $buildDir -arch x86 $extensions"
		& $lightExe "-out $releaseDir\Setup.exe $extensions $buildDir\template.wixobj"
	}
}

$solutionDir = Get-SolutionDir

### DEBUG:
$createReleasePackageExe = [IO.Path]::Combine($solutionDir, 'CreateReleasePackage', 'bin', 'Debug', 'CreateReleasePackage.exe')
$wixDir = [IO.Path]::Combine($solutionDir, '..', 'ext', 'wix')
### End DEBUG

$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$releaseDir = Join-Path $solutionDir "Releases"
mkdir -p $releaseDir

if ($Param_ProjectName.Length -gt 0) {
	Create-ReleaseForProject $Param_ProjectName
} else {
	foreach ($item in $dte.Solution.Projects | ?{$_.Object.References | ?{$_.Name -eq "Shimmer.Client"}}) {
		$name = $item.Name
		Create-ReleaseForProject $Param_ProjectName
	}
}