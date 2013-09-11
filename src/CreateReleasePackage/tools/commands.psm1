$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Import-Module (Join-Path $toolsDir "utilities.psm1")

$createReleasePackageExe = Join-Path $toolsDir "CreateReleasePackage.exe"

$wixDir = Join-Path $toolsDir "wix"
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
        [string]$buildDirectory,
        [Parameter(Mandatory = $false)]
        [string]$releasesDirectory = Join-Path $solutionDir "Releases"
    )

    if (!(Test-Path $releasesDirectory)) { `
        New-Item -ItemType Directory -Path $releasesDirectory | Out-Null
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

    $packageDir = Get-NuGetPackagesPath($solutionDir)
    if(-not $packageDir) {
        $packageDir = Join-Path $solutionDir "packages"
    }

    Write-Host ""
    Write-Message "Using packages directory $packageDir"

    foreach($pkg in $nugetPackages) {
        $pkgFullName = $pkg.FullName
        $releaseOutput = & $createReleasePackageExe -o $releaseDir -p $packageDir $pkgFullName

        $packages = $releaseOutput.Split(";")
        $fullRelease = $packages[0].Trim()

        Write-Host ""
        Write-Message "Full release: $fullRelease"

        if ($packages.Length -gt 1) {
            $deltaRelease = $packages[-1].Trim()
            if ([string]::IsNullOrWhitespace($deltaRelease) -eq $false) {
                Write-Message "Delta release: $deltaRelease"
            }
        }

        $newItem = New-Object PSObject -Property @{
                PackageSource = $pkgFullName
                FullRelease = $fullRelease
                DeltaRelease = $deltaRelease
        }

        $releasePackages += $newItem
    }

    # use the last package and create an installer
    $latest =  $releasePackages[-1]

    $latestPackageSource = $latest.PackageSource
    $latestFullRelease = $latest.FullRelease

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