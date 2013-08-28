$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Path

function Initialize-Installer {
    [CmdletBinding()]
    param (
        [Parameter(Position=0, ValueFromPipeLine=$true, Mandatory=$true)]
        [string] $ProjectName = ''
    )

    $project = (Get-Project -Name $ProjectName)
    $projectDir = (gci $project.FullName).Directory
    $nuspecFile = (Join-Path $projectDir "$ProjectName.nuspec")

    Add-InstallerTemplate -Destination $nuspecFile -ProjectName $ProjectName

    Set-BuildPackage -Value $true -ProjectName $ProjectName

    Add-FileWithNoOutput -FilePath $nuspecFile -Project $Project

    # open the nuspec file in the editor
    $dte.ItemOperations.OpenFile($nuspecFile) | Out-Null
}

function Publish-Release {
    [CmdletBinding()]
    param (
        [Parameter(Position=0, ValueFromPipeLine=$true, Mandatory=$true)]
        [string] $ProjectName = ''
    )

    $solutionDir = (gci $dte.Solution.FullName).Directory

    $project = Get-Project $ProjectName
    $outputDir =  $project.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value

    $createReleaseScript = Join-Path $toolsDir "Create-Release.ps1"

    . $createReleaseScript -ProjectNameToBuild $ProjectName  `
                           -SolutionDir $solutionDir `
                           -BuildDirectory $outputDir
}

Export-ModuleMember Initialize-Installer
Export-ModuleMember Publish-Release