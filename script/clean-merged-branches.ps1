Set-PSDebug -Strict
$ErrorActionPreference = "Stop"

$branches = git branch -a --merged | 
    ?{$_ -match "remotes\/origin"} | 
    ?{$_ -notmatch "\/master"} | 
    %{$_.Replace("remotes/origin/", "").Trim() }

if (-not $branches) {
    echo "No merged branches detected"
    exit 0
}

echo $branches

$title = "Delete Merged Branches"
$message = "Do you want to delete the already-merged remote branches displayed above??"

$yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes", `
    "Delete the remote branches listed."

$no = New-Object System.Management.Automation.Host.ChoiceDescription "&No", `
    "Leave the branches alone."
$options = [System.Management.Automation.Host.ChoiceDescription[]]($yes, $no)

$result = $host.ui.PromptForChoice($title, $message, $options, 1) 

if ($result -eq 1) {
    exit 0
}

$branches | %{ git push origin ":$_" }
