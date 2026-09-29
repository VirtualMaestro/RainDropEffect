<#
.SYNOPSIS
    Builds and runs the Windows benchmark player (Task 25) or the sustained run (Task 31).

.DESCRIPTION
    Editor Play Mode is not a valid GPU measurement, so the numbers in docs/performance.md come from
    a Development player built from Assets/Host/Benchmark.unity. This script drives the whole chain
    in batch mode — profiles, scene, build, run — so a measurement can be repeated exactly.

    The Unity editor must be closed: batch mode cannot take the project lock while it is open.

    Windows PowerShell 5.1 compatible: no pipeline chain operators, no ternary.

.PARAMETER Editor
    Path to Unity.exe. Must be the version in ProjectSettings/ProjectVersion.txt.

.PARAMETER SkipBuild
    Re-run the existing player without rebuilding it.

.PARAMETER Sustained
    Run the 12-minute sustained pass (Task 31) instead of the scenario sweep.

.PARAMETER Width
    Player width. The measurement resolution is recorded in the JSON file name.

.PARAMETER Height
    Player height.
#>
[CmdletBinding()]
param(
    [string] $Editor = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe',
    [switch] $SkipBuild,
    [switch] $Sustained,
    [int] $Width = 1280,
    [int] $Height = 720
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$logs = Join-Path $repo 'Logs'
$player = Join-Path $repo 'Builds\Benchmark\Windows\RainBenchmark.exe'

if (-not (Test-Path $Editor)) { Write-Host "GAP: Unity editor not found at $Editor"; exit 2 }

function Invoke-Unity([string] $method, [string] $log) {
    $logPath = Join-Path $logs $log
    $arguments = @('-batchmode', '-quit', '-nographics', '-projectPath', $repo, '-executeMethod', $method, '-logFile', $logPath)
    Write-Host "  unity -executeMethod $method"

    # Start-Process -Wait also waits for the processes Unity spawned, some of which outlive it;
    # -PassThru without -Wait never populates ExitCode. .NET keeps the handle, so both work.
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $Editor
    $info.UseShellExecute = $false
    $info.Arguments = ($arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '

    $process = [System.Diagnostics.Process]::Start($info)
    $process.WaitForExit()

    if ($process.ExitCode -ne 0) {
        Write-Host "FAILED: $method (exit $($process.ExitCode)). Last 30 lines of $logPath :"
        if (Test-Path $logPath) { Get-Content $logPath -Tail 30 }
        exit 1
    }
}

New-Item -ItemType Directory -Path $logs -Force | Out-Null

if (-not $SkipBuild) {
    Invoke-Unity 'BenchmarkSetup.CreateProfiles' 'benchmark-profiles.log'
    Invoke-Unity 'BenchmarkSetup.CreateBenchmarkScene' 'benchmark-scene.log'
    Invoke-Unity 'BenchmarkSetup.BuildWindowsBenchmarkFromCommandLine' 'benchmark-build.log'
}

if (-not (Test-Path $player)) { Write-Host "GAP: no player at $player"; exit 1 }

$playerLog = Join-Path $logs 'benchmark-player.log'
$arguments = @(
    '-logFile', $playerLog,
    '-screen-width', $Width,
    '-screen-height', $Height,
    '-screen-fullscreen', '0',
    '-benchmarkOut', $logs
)

if ($Sustained) { $arguments += '-sustained' }

Write-Host "  running the benchmark player ($Width x $Height)"
$info = New-Object System.Diagnostics.ProcessStartInfo
$info.FileName = $player
$info.UseShellExecute = $false
$info.Arguments = ($arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '

$process = [System.Diagnostics.Process]::Start($info)

# The sweep is ten 600-frame scenarios; the sustained pass is twelve minutes plus settling.
$budget = 900000
if ($Sustained) { $budget = 1500000 }

if (-not $process.WaitForExit($budget)) {
    Write-Host "The player did not exit within $($budget / 1000) s; killing it."
    try { $process.Kill() } catch {}
    exit 1
}

Write-Host "Player exit code: $($process.ExitCode)"

if (Test-Path $playerLog) {
    Select-String -Path $playerLog -Pattern '\[RainBenchmark\]' | ForEach-Object { Write-Host "  $($_.Line)" }
    $problems = @(Select-String -Path $playerLog -Pattern 'NullReference|Exception|Shader error')
    Write-Host "Player log problems: $($problems.Count)"
    foreach ($problem in $problems) { Write-Host "  $($problem.Line)" }
}

Get-ChildItem $logs -Filter 'benchmark-*.json' | ForEach-Object { Write-Host "Result: $($_.FullName) ($($_.Length) bytes)" }
Get-ChildItem $logs -Filter 'sustained-*.json' | ForEach-Object { Write-Host "Result: $($_.FullName) ($($_.Length) bytes)" }
exit 0
