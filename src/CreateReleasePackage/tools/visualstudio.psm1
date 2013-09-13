$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Import-Module (Join-Path $toolsDir "utilities.psm1")
Import-Module (Join-Path $toolsDir "commands.psm1")

function New-Release {
    [CmdletBinding()]
    param (
        [Parameter(Position=0, ValueFromPipeLine=$true)]
        [string] $ProjectName
    )

    if (-not $ProjectName) {
        $ProjectName = (Get-Project).Name
    }

    $project = Get-Project $ProjectName
    $activeConfiguration = $project.ConfigurationManager.ActiveConfiguration

    Write-Message "Building project $ProjectName"

    $dte.Solution.SolutionBuild.BuildProject($activeConfiguration.ConfigurationName, $project.FullName, $true)

    Write-Message "Publishing release for project $ProjectName"

    $solutionDir = (gci $dte.Solution.FullName).Directory

    $projectDir = (gci $project.FullName).Directory
    $outputDir =  $activeConfiguration.Properties.Item("OutputPath").Value
    
    New-ReleaseForPackage -SolutionDir $solutionDir `
                             -BuildDir (Join-Path $projectDir $outputDir)
}

Register-TabExpansion 'New-Release' @{
        ProjectName = { Get-Project -All | Select -ExpandProperty Name }
}

Export-ModuleMember New-Release