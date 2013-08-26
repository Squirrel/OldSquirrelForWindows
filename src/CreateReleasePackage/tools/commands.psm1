function Initialize-Installer {
    [CmdletBinding()]
    param (
        [Parameter(Position=0, ValueFromPipeLine=$true, Mandatory=$true)]
        [string] $ProjectName = ''
    )

    $project = (Get-Project -Name $ProjectName)
    $projectDir = (gci $project.FullName).Directory
    $nuspecFile = (Join-Path $projectDir "$ProjectName.nuspec")

    if (Test-Path $nuspecFile) {
        Write-Host "The file already exists, no need to overwrite it..."
    } else {
        $nuspecTemplate = (Join-Path $toolsDir template.nuspec.temp)
        Copy-Item $nuspecTemplate $nuspecFile -Force | Out-Null
    }

    Set-BuildPackage -Value $true -ProjectName $ProjectName

    Add-FileWithNoOutput -FilePath $nuspecFile -Project $Project

	## open the nuspec file in the editor
    $dte.ItemOperations.OpenFile($nuspecFile) | Out-Null
}

function Publish-Release {
    [CmdletBinding()]
    param (
        [Parameter(Position=0, ValueFromPipeLine=$true, Mandatory=$true)]
        [string] $ProjectName = ''
    )

    Write-Host "TODO: move the Powershell functionality into this project"
}

Export-ModuleMember Initialize-Installer
Export-ModuleMember Publish-Release