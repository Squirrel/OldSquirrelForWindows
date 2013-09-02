$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$createReleasePackageExe = Join-Path $toolsDir "CreateReleasePackage.exe"

$wixDir = Join-Path $toolsDir "wix"
$support = Join-Path $toolsDir "support.ps1"
$echoArgs = Join-Path $toolsDir "EchoArgs.exe"

$candleExe = Join-Path $wixDir "candle.exe"
$lightExe = Join-Path $wixDir "light.exe"

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
    if (!(Test-Path $releaseDir)) { `
        New-Item -ItemType Directory -Path $releaseDir | Out-Null
    }

    Write-Message "Checking $buildDirectory for packages`n"

    $nugetPackages = ls "$buildDirectory\*.nupkg" `
        | ?{ $_.Name.EndsWith(".symbols.nupkg") -eq $false } `
        | sort @{expression={$_.LastWriteTime};Descending=$false}

    if ($nugetPackages.length -eq 0) {
        Write-Error "No .nupkg files were found in the build directory"
        Write-Error "Have you built the solution lately?"

        return
    } else {
        foreach($pkg in $nugetPackages) {
            $pkgFullName = $pkg.FullName
            Write-Message "Found package $pkgFullName"
        }
    }

    Write-Host ""
    Write-Message "Publishing artifacts to $releaseDir"

    $releasePackages = @()

    foreach($pkg in $nugetPackages) {
        $pkgFullName = $pkg.FullName
       
        $packageDir = Join-Path $solutionDir "packages"
        $releaseOutput = & $createReleasePackageExe -o $releaseDir -p $packageDir $pkgFullName

        $packages = $releaseOutput.Split(";")
        $fullRelease = $packages[0]

        Write-Host ""
        Write-Message "Full release: $fullRelease"

        if ($packages.Length -gt 1) {
            $deltaRelease = $packages[-1]
            Write-Message "Delta release: $deltaRelease"
        }

        $releasePackages += @{
            "Package" = @{
                PackageSource = $pkgFullName
                FullRelease = $fullRelease
                DeltaRelease = $deltaRelease
            }
        }
    }

    # use the last package and create an installer
    $latest =  $releasePackages[-1]

    $latestPackageSource = $latest.Values.Item("PackageSource")
    $latestFullRelease = $latest.Values.Item("FullRelease")

    Write-Host ""
    Write-Message "Creating installer for $latestFullRelease"

    $candleTemplate = Generate-TemplateFromPackage $latestPackageSource "$toolsDir\template.wxs"
    $wixTemplate = Join-Path $buildDirectory "template.wxs"

    Remove-ItemSafe $wixTemplate
    mv $candleTemplate $wixTemplate | Out-Null

    Remove-ItemSafe "$buildDirectory\template.wixobj"

    Write-Message "Running candle.exe"
    & $candleExe -d"ToolsDir=$toolsDir" -d"ReleasesFile=$releaseDir\RELEASES" -d"NuGetFullPackage=$latestFullRelease" -out "$buildDirectory\template.wixobj" -arch x86 -ext "$wixDir\WixBalExtension.dll" -ext "$wixDir\WixUtilExtension.dll" "$wixTemplate"

    Write-Message "Running light.exe"
    & $lightExe -out "$releaseDir\Setup.exe" -ext "$wixDir\WixBalExtension.dll" -ext "$wixDir\WixUtilExtension.dll" "$buildDirectory\template.wixobj"
}

function New-Release {
    [CmdletBinding()]
    param (
        [Parameter(Position=0, ValueFromPipeLine=$true)]
        [string] $ProjectName
    )

    . $support

    if (-not $ProjectName) {
        $ProjectName = (Get-Project).Name
    }

    Write-Message "Publishing release for project $ProjectName"

    $solutionDir = (gci $dte.Solution.FullName).Directory

    $project = Get-Project $ProjectName

    $projectDir = (gci $project.FullName).Directory
    $outputDir =  $project.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value
    
    Create-ReleaseForProject -SolutionDir $solutionDir `
                             -BuildDirectory (Join-Path $projectDir $outputDir)
}

Register-TabExpansion 'New-Release' @{
        ProjectName = { Get-Project -All | Select -ExpandProperty Name }
}

Export-ModuleMember New-Release