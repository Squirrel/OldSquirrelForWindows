$here = Split-Path -Parent $MyInvocation.MyCommand.Path

function Get-TemporaryFolder {
    $id = [System.Guid]::NewGuid().ToString()
    $folder = [System.IO.Path]::Combine($env:TEMP, $id)
    New-Item -ItemType Directory $folder
    return $folder
}

Describe "Create-Release" {
  Context "When checking an existing directory for packages" {
  
    $newPath = (Get-TemporaryFolder)
    $newPath | Should Exist
    
    $otherProject = "$here\..\.."
    . "$here\..\src\CreateReleasePackage\tools\Create-Release.ps1" `
            -ProjectNameToBuild "TestApp" `
            -SolutionDir "$otherProject\TestApp\" `
            -BuildDirectory "$otherProject\TestApp\bin\Debug\"

    # TODO: get these parameters

    It "It finishes!" {

    }
   }
}