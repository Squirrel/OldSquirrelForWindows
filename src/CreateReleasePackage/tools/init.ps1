param($installPath, $toolsPath, $package, $project)
Import-Module (Join-Path $toolsPath utilities.psm1)
Import-Module (Join-Path $toolsPath commands.psm1)

Register-TabExpansion 'New-Release' @{
        ProjectName = { Get-Project -All | Select -ExpandProperty Name }
}

Export-ModuleMember New-Release