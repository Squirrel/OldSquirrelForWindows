$here = Split-Path -Parent $MyInvocation.MyCommand.Path

function Get-TemporaryFolder {
    $id = [System.Guid]::NewGuid().ToString()
    # TestDrive:\ is a Pester folder which gets cleaned up 
    # after the completion of the tests *yay*
    $folder = Join-Path "TestDrive:\" $id
    New-Item -ItemType Directory $folder | Out-Null
    Convert-Path -LiteralPath $folder
}

function New-DirectorySafe {
    param(
        [parameter(Mandatory=$true)]
        $Path
    )

    if (!(Test-Path $Path)) {
        New-Item $Path -ItemType Directory | Out-Null
    }
}

function Copy-SquirrelTools {
    param(
        [parameter(Mandatory=$true)]
        $ToolsDir
    )
    copy $here\..\src\CreateReleasePackage\tools\* $ToolsDir

    $binaries = @("$here\..\bin\CreateReleasePackage.exe", `
                    "$here\..\bin\Squirrel.WiXUI.config" , `
                    "$here\..\bin\NuGet.Core.dll", `
                    "$here\..\bin\Ionic.Zip.dll", `
                    "$here\..\bin\MarkdownSharp.dll", `
                    "$here\..\bin\ReactiveUI.Blend.dll", `
                    "$here\..\bin\ReactiveUI.dll", `
                    "$here\..\bin\ReactiveUI.Routing.dll", `
                    "$here\..\bin\ReactiveUI.Xaml.dll", `
                    "$here\..\bin\SharpBITS.Base.dll", `
                    "$here\..\bin\Squirrel.Client.dll", `
                    "$here\..\bin\Squirrel.Core.dll", `
                    "$here\..\bin\Squirrel.WiXUi.dll", `
                    "$here\..\bin\Squirrel.WiXUiClient.dll", `
                    "$here\..\bin\System.IO.Abstractions.dll", `
                    "$here\..\bin\System.Reactive.Core.dll", `
                    "$here\..\bin\System.Reactive.Interfaces.dll", `
                    "$here\..\bin\System.Reactive.Linq.dll", `
                    "$here\..\bin\System.Reactive.PlatformServices.dll", `
                    "$here\..\bin\System.Reactive.Windows.Threading.dll", `
                    "$here\..\bin\System.Windows.Interactivity.dll")

    copy -Path $binaries -Destination $ToolsDir

    copy -Path "$here\..\ext\wix\" `
         -Recurse `
         -Exclude "doc" `
         -Destination "$toolsDir\wix"
}

Describe "Create-Release" {
  Context "When checking an existing directory for packages" {

    $tempPath = Get-TemporaryFolder

    $toolsDir = Join-Path $tempPath tools
    $solutionDir = Join-Path $tempPath code
    $nugetExe = Join-Path $here "..\src\.nuget\nuget.exe"
    $packagesDir = Join-Path $solutionDir packages
    $buildOutputDir = Join-Path $solutionDir binaries

    New-DirectorySafe $toolsDir
    New-DirectorySafe $solutionDir
    New-DirectorySafe $packagesDir
    New-DirectorySafe $buildOutputDir

    copy $here\packages\TestApp.packages.config $packagesDir\packages.config

    . $nugetExe install $packagesDir\packages.config `
                -OutputDirectory $packagesDir

    copy $here\packages\TestApp.1.0.0-beta.nupkg $buildOutputDir

    Copy-SquirrelTools -ToolsDir $toolsDir

    . "$toolsDir\Create-Release.ps1" -SolutionDir $solutionDir `
                                     -BuildDir $buildOutputDir

    It "completes without error" {
       $LASTEXITCODE | Should Be 0
    }

    It "creates the nupkg" {
        "$solutionDir\Releases\TestApp-1.0.0-beta-full.nupkg" | Should Exist
    }
  }

  Context "When CreateReleasePackage.exe fails" {

    $tempPath = Get-TemporaryFolder

    $toolsDir = Join-Path $tempPath tools
    $solutionDir = Join-Path $tempPath code
    $nugetExe = Join-Path $here "..\src\.nuget\nuget.exe"
    $packagesDir = Join-Path $solutionDir packages
    $buildOutputDir = Join-Path $solutionDir binaries

    New-DirectorySafe $toolsDir
    New-DirectorySafe $solutionDir
    New-DirectorySafe $packagesDir
    New-DirectorySafe $buildOutputDir

    copy $here\packages\TestApp.MissingOne.packages.config $packagesDir\packages.config

    . $nugetExe install $packagesDir\packages.config `
                -OutputDirectory $packagesDir

    copy $here\packages\TestApp.1.0.0-beta.nupkg $buildOutputDir

    Copy-SquirrelTools -ToolsDir $toolsDir

    . "$toolsDir\Create-Release.ps1" -SolutionDir $solutionDir `
                                     -BuildDir $buildOutputDir

    It "returns an error code" {
       $LASTEXITCODE | Should Not Be 0
    }

    It "does not create the nupkg" {
        "$solutionDir\Releases\TestApp-1.0.0-beta-full.nupkg" | Should Not Exist
    }
  }

}
