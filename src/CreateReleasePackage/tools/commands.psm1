$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# from NuGetPowerTools - https://github.com/davidfowl/NuGetPowerTools/blob/master/MSBuild.psm1

function Resolve-ProjectName {
    param(
        [parameter(ValueFromPipelineByPropertyName = $true)]
        [string[]]$ProjectName
    )

    if($ProjectName) {
        $projects = Get-Project $ProjectName
    }
    else {
        # All projects by default
        $projects = Get-Project
    }

    $projects
}

function Get-MSBuildProject {
    param(
        [parameter(ValueFromPipelineByPropertyName = $true)]
        [string[]]$ProjectName
    )
    Process {
        (Resolve-ProjectName $ProjectName) | % {
            $path = $_.FullName
            @([Microsoft.Build.Evaluation.ProjectCollection]::GlobalProjectCollection.GetLoadedProjects($path))[0]
        }
    }
}

function Set-MSBuildProperty {
    param(
        [parameter(Position = 0, Mandatory = $true)]
        $PropertyName,
        [parameter(Position = 1, Mandatory = $true)]
        $PropertyValue,
        [parameter(Position = 2, ValueFromPipelineByPropertyName = $true)]
        [string[]]$ProjectName
    )
    Process {
        (Resolve-ProjectName $ProjectName) | %{
            $buildProject = $_ | Get-MSBuildProject
            $buildProject.SetProperty($PropertyName, $PropertyValue) | Out-Null
            $_.Save()
        }
    }
}

function Get-MSBuildProperty {
    param(
        [parameter(Position = 0, Mandatory = $true)]
        $PropertyName,
        [parameter(Position = 2, ValueFromPipelineByPropertyName = $true)]
        [string]$ProjectName
    )

    $buildProject = Get-MSBuildProject $ProjectName
    $buildProject.GetProperty($PropertyName)
}

function Get-ProjectItem {
    param(
        [parameter(Position = 0, Mandatory = $true)]
        $FileName,
        [parameter(Position = 1, Mandatory = $true)]
        $Project
    )

    $existingFile = $Project.ProjectItems | Where-Object { $_.Name -eq $FileName }

    if ($existingFile.length -eq 0) {
        return $null
    }

    return $existingFile[0]
}

function Add-FileWithNoOutput {
    [CmdletBinding()]
    param (
        [Parameter(Position=0, ValueFromPipeLine=$true, Mandatory=$true)]
        [string] $FilePath,

        [Parameter(Position=1, ValueFromPipeLine=$true, Mandatory=$true)]
        $Project
    )

    # do we have the existing file in the project?
    # NOTE: this won't work for nested files
    $fileName = (gci $FilePath).Name
    $existingFile = Get-ProjectItem -FileName $fileName -Project $Project

    if ($existingFile -eq $null) {
        Write-Host "Could not find file, let's insert it"
        $Project.DTE.ItemOperations.AddExistingItem($FilePath) | Out-Null
        $Project.Save()
        $existingFile = Get-ProjectItem -FileName $fileName -Project $Project
        if ($existingFile -eq $null) {
            Write-Host "oh dear, it's still not finding the file"
        }
    } else {
        Write-Host "The file exists, we can continue on"
    }

    Write-Host "Setting file to not copy to output directory"
    $copyToOutput1 = $existingFile.Properties.Item("CopyToOutputDirectory")
    $copyToOutput1.Value = 0
    $project.Save()
}

function Set-BuildPackage {
    [CmdletBinding()]
    param (
        [Parameter(Position=0, ValueFromPipeLine=$true, Mandatory=$true)]
        [string] $ProjectName = '',

        [Parameter(Position=0, ValueFromPipeLine=$true, Mandatory=$true)]
        [bool] $Value = $false
    )

    $buildPackage = Get-MSBuildProperty "BuildPackage" $ProjectName
    $buildPackageValue = $buildPackage.EvaluatedValue
    Write-Host "BuildPackage is $buildPackageValue in project $ProjectName"

    if ($buildProjectValue -eq $Value) {
        Write-Host "No need to modify the csproj file"
    } else {
        Write-Host "Inserting <BuildPackage>$Value</BuildPackage> into project $ProjectName"
        Set-MSBuildProperty "BuildPackage" $Value $ProjectName
    }
}

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