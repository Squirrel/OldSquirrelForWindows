## http://stackoverflow.com/questions/5935162/nuget-error-external-packages-cannot-depend-on-packages-that-target-projects-w
param($installPath, $toolsPath, $package, $project)
$project.Object.Project.ProjectItems.Item("keep.me").Delete()