## http://stackoverflow.com/questions/5935162/nuget-error-external-packages-cannot-depend-on-packages-that-target-projects-w
param($installPath, $toolsPath, $package, $project)
$project.Object.Project.ProjectItems.Item("keep.me").Delete()

Import-Module (Join-Path $toolsPath utilities.psm1)

function Initialize-Shimmer {
    [CmdletBinding()]
    param (
        [Parameter(Position=0, ValueFromPipeLine=$true)]
        [string] $ProjectName = ''
    )

    if (-not $ProjectName) {
        $ProjectName = (Get-Project).Name
    }

    $project = (Get-Project -Name $ProjectName)
    $projectDir = (gci $project.FullName).Directory
    $nuspecFile = (Join-Path $projectDir "$ProjectName.nuspec")

    Write-Message "Initializing project $ProjectName for installer and packaging"

    Add-InstallerTemplate -Destination $nuspecFile -ProjectName $ProjectName

    Set-BuildPackage -Value $true -ProjectName $ProjectName

    Add-FileWithNoOutput -FilePath $nuspecFile -Project $Project

    # open the nuspec file in the editor
    $dte.ItemOperations.OpenFile($nuspecFile) | Out-Null
}

Write-Message "Now to setup the project - so you don't have to..."
Write-Host
Initialize-Shimmer
Write-Host