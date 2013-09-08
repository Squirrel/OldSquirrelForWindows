$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Import-Module (Join-Path $toolsDir "utilities.psm1")

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