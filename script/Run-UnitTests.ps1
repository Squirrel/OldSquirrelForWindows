$scriptPath = Split-Path $MyInvocation.MyCommand.Path
$rootDirectory = join-path $scriptPath ..
$srcFolder = join-path $scriptPath "..\src"

function Run-XUnit([string]$project, [int]$timeoutDuration) {
    $dll = "$rootDirectory\bin\$project.dll"

    $xunitDirectory = Join-Path $rootDirectory ext\xunit
    $consoleRunner = Join-Path $xunitDirectory xunit.console.clr4.x86.exe
    $xml = Join-Path $rootDirectory "nunit-$project.xml"
    $outputPath = [System.IO.Path]::GetTempFileName()

    $args = $dll, "/noshadow", "/nunit", $xml, "/silent"
    [object[]] $output = "$consoleRunner " + ($args -join " ")
    $process = Start-Process -PassThru -NoNewWindow -RedirectStandardOutput $outputPath $consoleRunner ($args | %{ "`"$_`"" })
    Wait-Process -InputObject $process -Timeout $timeoutDuration -ErrorAction SilentlyContinue
    if ($process.HasExited) {
        $output += Get-Content $outputPath
        $exitCode = $process.ExitCode
    } else {
        $output += "Tests timed out. Backtrace:"
        $exitCode = 9999
    }
    Stop-Process -InputObject $process
    Remove-Item $outputPath

    $result = New-Object System.Object
    $result | Add-Member -Type NoteProperty -Name Output -Value $output
    $result | Add-Member -Type NoteProperty -Name ExitCode -Value $exitCode
    $result
}

$exitCode = 0

Write-Output "Running Shimmer.Tests..."
$result = Run-XUnit Shimmer.Tests 300
if ($result.ExitCode -eq 0) {
    # Print out the test result summary.
    Write-Output $result.Output[-1]
} else {
    $exitCode = $result.ExitCode
    Write-Output $result.Output
}
Write-Output ""

exit $exitCode
