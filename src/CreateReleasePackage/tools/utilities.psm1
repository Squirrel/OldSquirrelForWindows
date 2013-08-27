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

        ###### start infinite crying

        # SO, have a guess at what this line of code is supposed to do
        #
        # $Project.DTE.ItemOperations.AddExistingItem($FilePath)
        #
        # Not sure? Well, according to MSDN (http://msdn.microsoft.com/en-us/library/envdte.itemoperations.addexistingitem(v=vs.110).aspx)
        # it says "Adds an existing item to the current project." but
        # my testing indicates it'll often just add it to the solution
        # under a new Solution Folder, which isn't what I wanted either
        # and after a few tries running this it'll add it to the
        # project so I have two nuspec files in the solution.

        # Fuck Yeah!

        # this is the evil code to make this all better

        # let's use the native MSBuild object
        # so we don't force a reload after install
        $msbuildProj = Get-MSBuildProject $Project.Name
        $xml = $msbuildProj.Xml

        # create the new elements with *just* the nuspec file
        $itemGroup = $xml.AddItemGroup()
        $none = $xml.CreateItemElement("None")

        $none.Include = $fileName
        $itemGroup.AppendChild($none) | Out-Null

        $msbuildProj.Save()

        ###### end infinite crying
    } else {
        Write-Message "Ensuring nuspec file is excluded from build output"

        $copyToOutput = $existingFile.Properties.Item("CopyToOutputDirectory")
        $copyToOutput.Value = 0
        $project.Save()
    }
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

    if ([System.Convert]::ToBoolean($buildPackage.EvaluatedValue) -eq $Value) {
        Write-Message "No need to modify the csproj file as BuildPackage is set to $Value"
    } else {
        Write-Message "Changing BuildPackage from '$buildPackageValue' to '$Value' in project file"
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