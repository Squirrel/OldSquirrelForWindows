$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Path

function Initialize-ProjectForShimmer {
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

function Publish-ShimmerRelease {
    [CmdletBinding()]
    param (
        [Parameter(Position=0, ValueFromPipeLine=$true)]
        [string] $ProjectName
    )

    if (-not $ProjectName) {
        $ProjectName = (Get-Project).Name
    }

    Write-Message "Publishing release for project $ProjectName"

    $solutionDir = (gci $dte.Solution.FullName).Directory

    $project = Get-Project $ProjectName
    $outputDir =  $project.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value

    $createReleaseScript = Join-Path $toolsDir "Create-Release.ps1"

    . $createReleaseScript -ProjectNameToBuild $ProjectName  `
                           -SolutionDir $solutionDir `
                           -BuildDirectory $outputDir
}

# Statement completion for project names
'Initialize-ProjectForShimmer', 'Publish-ShimmerRelease' | %{ 
    Register-TabExpansion $_ @{
        ProjectName = { Get-Project -All | Select -ExpandProperty Name }
    }
}

Export-ModuleMember Initialize-ProjectForShimmer
Export-ModuleMember Publish-ShimmerRelease