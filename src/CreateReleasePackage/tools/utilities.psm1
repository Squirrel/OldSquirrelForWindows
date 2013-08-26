$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# from NuGetPowerTools - https://github.com/davidfowl/NuGetPowerTools/blob/master/MSBuild.psm1

function Write-Message {
    param(
        [parameter(ValueFromPipelineByPropertyName = $true, Mandatory = $true)]
        [string[]]$Message
    )

    Write-Host "Shimmer: " -f blue -nonewline;
    Write-Host $Message
}

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

# helper functions to take care of the nastiness of manipulating everything

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
        Write-Message "Could not find file '$FilePath', adding it to project"
        $Project.DTE.ItemOperations.AddExistingItem($FilePath) | Out-Null
        $Project.Save()
        $existingFile = Get-ProjectItem -FileName $fileName -Project $Project
    } else {
        Write-Message "The file '$FilePath' exists already, excellent!"
    }

    Write-Message "Modifying project file to exclude csproj file from build output"
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

    if ($buildProjectValue -eq $Value) {
        Write-Message "No need to modify the csproj file as BuildPackage is set to $Value"
    } else {
        Write-Message "Setting BuildPackage to $Value in project file"
        Set-MSBuildProperty "BuildPackage" $Value $ProjectName
    }
}

function Add-InstallerTemplate {
    [CmdletBinding()]
    param (
        [Parameter(Position=0, ValueFromPipeLine=$true, Mandatory=$true)]
        [string] $Destination,

        [Parameter(Position=0, ValueFromPipeLine=$true, Mandatory=$true)]
        [string] $ProjectName = ''
    )

    if (Test-Path $Destination) {
         Write-Message "The file '$Destination' already exists, will not overwrite this file..."
    } else {
        $content = Get-Content (Join-Path $toolsDir template.nuspec.temp) | `
                   Foreach-Object { $_ -replace '{{project}}', $ProjectName }

        Set-Content -Path $Destination -Value $content	| Out-Null
    }
}