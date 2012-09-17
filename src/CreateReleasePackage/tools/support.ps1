##
## Most of this is blatantly cribbed from https://github.com/davidfowl/NuGetPowerTools
##

function Get-SolutionDir {
    if($dte.Solution -and $dte.Solution.IsOpen) {
        return Split-Path $dte.Solution.Properties.Item("Path").Value
    }
    else {
        throw "Solution not avaliable"
    }
}

function Get-ProjectPropertyValue {
    param(
        [parameter(Mandatory = $true)]
        [string]$ProjectName,
        [parameter(Mandatory = $true)]
        [string]$PropertyName
    )    
    try {
        $property = (Get-Project $ProjectName).Properties.Item($PropertyName)
        if($property) {
            return $property.Value
        }
    }
    catch {
    }
    return $null
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
        $projects = Get-Project -All
    }
    
    $projects
}

function Get-InstallPath {
    param(
        $package
    )
    # Get the repository path
    $componentModel = Get-VSComponentModel
    $repositorySettings = $componentModel.GetService([NuGet.VisualStudio.IRepositorySettings])
    $pathResolver = New-Object NuGet.DefaultPackagePathResolver($repositorySettings.RepositoryPath)
    $pathResolver.GetInstallPath($package)
}

function Ensure-NuGetBuild {
    # Install the nuget command line if it doesn't exist
    $solutionDir = Get-SolutionDir
    $nugetToolsPath = (Join-Path $solutionDir .nuget)
    
    if(!(Test-Path $nugetToolsPath) -or !(Get-ChildItem $nugetToolsPath)) {
        Install-Package NuGet.Build -Source 'https://go.microsoft.com/fwlink/?LinkID=206669'
        
        $nugetBuildPackage = @(Get-Package NuGet.Build)[0]
        $nugetExePackage = @(Get-Package NuGet.CommandLine)[0]
        
        if(!$nugetBuildPackage -and !$nugetExePackage) {
            return $false
        }
        
        # Get the package path
        $nugetBuildPath = Get-InstallPath $nugetBuildPackage
        $nugetExePath = Get-InstallPath $nugetExePackage
        
        if(!(Test-Path $nugetToolsPath)) {
            mkdir $nugetToolsPath | Out-Null
        }

        Write-Host "Copying nuget.exe and msbuild scripts to $nugetToolsPath"

        Copy-Item "$nugetBuildPath\tools\*.*" $nugetToolsPath -Force | Out-Null
        Copy-Item "$nugetExePath\tools\*.*" $nugetToolsPath -Force | Out-Null
        Uninstall-Package NuGet.Build -RemoveDependencies

        Write-Host "Don't forget to commit the .nuget folder"
    }

    return $true
}

function Add-NuGetTargets {
    param(
        [parameter(ValueFromPipelineByPropertyName = $true)]
        [string[]]$ProjectName
    )
    Process {
        if($ProjectName) {
            $projects = Get-Project $ProjectName
        }
        else {
            # All projects by default
            $projects = Get-Project -All
        }

        if(!$projects) {
            Write-Error "Unable to locate project. Make sure it isn't unloaded."
            return
        }
        
        $targetsPath = '$(SolutionDir)\.nuget\NuGet.targets'
        
        $projects | %{ 
            $project = $_
            try {
                 if($project.Type -eq 'Web Site') {
                    Write-Warning "Skipping '$($project.Name)', Website projects are not supported"
                    return
                 }
                 
                 if(!$initialized) {
                    # Make sure the nuget tools exists
                    $initialized = Ensure-NuGetBuild
                 }
                 
                 $project | Add-SolutionDirProperty
                 
                 $buildProject = $project | Get-MSBuildProject
                 if(!($buildProject.Xml.Imports | ?{ $_.Project -eq $targetsPath } )) {
                    $buildProject.Xml.AddImport($targetsPath) | Out-Null
                    $project.Save()
                    $buildProject.ReevaluateIfNecessary()

                    "Updated '$($project.Name)' to use 'NuGet.targets'"
                 }
            }
            catch {
                Write-Warning "Failed to add import 'NuGet.targets' to $($project.Name)"
            }
        }
    }
}

function Enable-PackageRestore {
    param(
        [parameter(ValueFromPipelineByPropertyName = $true)]
        [string[]]$ProjectName
    )
    
    # Add the nuget targets on demand
    Add-NuGetTargets $ProjectName
    
    (Resolve-ProjectName $ProjectName) | %{ 
        $_ | Set-MSBuildProperty RestorePackages true
        "Enabled package restore for $($_.Name)"
    }
}

function Disable-PackageRestore {
    param(
        [parameter(ValueFromPipelineByPropertyName = $true)]
        [string[]]$ProjectName
    )
    (Resolve-ProjectName $ProjectName) | %{ 
        $_ | Set-MSBuildProperty RestorePackages false
        "Disabled package restore for $($_.Name)"
    }
}

function Enable-PackageBuild {
    param(
        [parameter(ValueFromPipelineByPropertyName = $true)]
        [string[]]$ProjectName
    )
    
    # Add the nuget targets on demand
    Add-NuGetTargets $ProjectName
    
    (Resolve-ProjectName $ProjectName) | %{ 
        $_ | Set-MSBuildProperty BuildPackage true
        "Enabled package build for $($_.Name)"
    }
}

function Disable-PackageBuild {
    param(
        [parameter(ValueFromPipelineByPropertyName = $true)]
        [string[]]$ProjectName
    )
    (Resolve-ProjectName $ProjectName) | %{ 
        $_ | Set-MSBuildProperty BuildPackage false
        "Disabled package build for $($_.Name)"
    }
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

function Add-Import {
    param(
        [parameter(Position = 0, Mandatory = $true)]
        [string]$Path,
        [parameter(Position = 1, ValueFromPipelineByPropertyName = $true)]
        [string[]]$ProjectName
    )
    Process {
        (Resolve-ProjectName $ProjectName) | %{
            $buildProject = $_ | Get-MSBuildProject
            $buildProject.Xml.AddImport($Path)
            $_.Save()
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

function Add-SolutionDirProperty {  
    param(
        [parameter(ValueFromPipelineByPropertyName = $true)]
        [string[]]$ProjectName
    )
    
    (Resolve-ProjectName $ProjectName) | %{
        $buildProject = $_ | Get-MSBuildProject
        
         if(!($buildProject.Xml.Properties | ?{ $_.Name -eq 'SolutionDir' })) {
            # Get the relative path to the solution
            $relativeSolutionPath = [NuGet.PathUtility]::GetRelativePath($_.FullName, $dte.Solution.Properties.Item("Path").Value)
            $relativeSolutionPath = [IO.Path]::GetDirectoryName($relativeSolutionPath)
            $relativeSolutionPath = [NuGet.PathUtility]::EnsureTrailingSlash($relativeSolutionPath)
            
            $solutionDirProperty = $buildProject.Xml.AddProperty("SolutionDir", $relativeSolutionPath)
            $solutionDirProperty.Condition = '$(SolutionDir) == '''' Or $(SolutionDir) == ''*Undefined*'''
            $_.Save()
         }
     }
}

