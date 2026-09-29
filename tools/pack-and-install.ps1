<#
.SYNOPSIS
    Exports the package as a tarball and installs it into a clean project (Task 32).

.DESCRIPTION
    tools/clean-install.ps1 installs the package from its folder. That is not what a consumer gets:
    a consumer gets a tarball produced by Package Manager, which applies its own inclusion rules.
    This script produces that tarball, checks what is inside it, and builds a player from it.

    The Unity editor must be closed: batch mode cannot take the project lock while it is open.

    Windows PowerShell 5.1 compatible: no pipeline chain operators, no ternary.

.PARAMETER Editor
    Path to Unity.exe.

.PARAMETER Keep
    Keep the temporary consumer project.
#>
[CmdletBinding()]
param(
    [string] $Editor = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe',
    [switch] $Keep
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$logs = Join-Path $repo 'Logs'
$packageOut = Join-Path $repo 'Builds\Package'
$editorVersion = (Get-Content (Join-Path $repo 'ProjectSettings\ProjectVersion.txt') | Select-Object -First 1).Split(' ')[1]
$urpVersion = '17.3.0'

function Write-Utf8NoBom([string] $path, [string] $text) {
    [System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
}

function Invoke-Unity([string] $projectPath, [string] $method, [string] $logPath) {
    $arguments = @('-batchmode', '-quit', '-nographics', '-projectPath', $projectPath, '-executeMethod', $method, '-logFile', $logPath)
    Write-Host "  unity -executeMethod $method"

    # Start-Process -Wait also waits for the processes Unity spawned, some of which outlive it;
    # -PassThru without -Wait never populates ExitCode. .NET keeps the handle, so both work.
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $Editor
    $info.UseShellExecute = $false
    $info.Arguments = ($arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '

    $process = [System.Diagnostics.Process]::Start($info)
    $process.WaitForExit()
    return $process.ExitCode
}

if (-not (Test-Path $Editor)) { Write-Host "GAP: Unity editor not found at $Editor"; exit 2 }

New-Item -ItemType Directory -Path $logs -Force | Out-Null
if (Test-Path $packageOut) { Remove-Item -Recurse -Force $packageOut }
New-Item -ItemType Directory -Path $packageOut -Force | Out-Null

$code = Invoke-Unity $repo 'RainDropEffect.Editor.RainBuildValidation.PackPackage' (Join-Path $logs 'pack.log')
if ($code -ne 0) {
    Write-Host "Pack failed (exit $code). Last 30 lines:"
    Get-Content (Join-Path $logs 'pack.log') -Tail 30
    exit 1
}

$tarball = Get-ChildItem $packageOut -Filter '*.tgz' | Select-Object -First 1
if ($null -eq $tarball) { Write-Host "Pack reported success but produced no tarball in $packageOut"; exit 1 }
Write-Host "Tarball: $($tarball.FullName) ($($tarball.Length) bytes)"

# What is inside decides what a consumer gets. Samples~ and Documentation~ are easy to lose (the
# tilde hides them from most tooling), and an editor DLL or a Logs folder must never ship.
$entries = & tar -tzf $tarball.FullName
$required = @('package/Runtime', 'package/Editor', 'package/Samples~', 'package/Documentation~', 'package/Tests')
$missing = @()
foreach ($item in $required) {
    $hit = @($entries | Where-Object { $_.StartsWith($item) })
    if ($hit.Count -eq 0) { $missing += $item }
}

$forbidden = @($entries | Where-Object { $_ -like 'package/Assets/*' -or $_ -like '*.dll' -or $_ -like 'package/Logs/*' })

Write-Host "Tarball entries: $($entries.Count); missing required: $($missing.Count); forbidden: $($forbidden.Count)"
foreach ($item in $missing) { Write-Host "  MISSING $item" }
foreach ($item in $forbidden) { Write-Host "  FORBIDDEN $item" }

if ($missing.Count -gt 0 -or $forbidden.Count -gt 0) { exit 1 }

# Install the tarball into a project that has never seen this repository.
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$proj = Join-Path $env:TEMP "RainTarballInstall\$stamp"
Write-Host "Consumer project: $proj"

New-Item -ItemType Directory -Path (Join-Path $proj 'Assets\Settings') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $proj 'Packages') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $proj 'ProjectSettings') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $proj 'Logs') -Force | Out-Null

Write-Utf8NoBom (Join-Path $proj 'ProjectSettings\ProjectVersion.txt') "m_EditorVersion: $editorVersion`n"

$tarballUrl = 'file:' + $tarball.FullName.Replace([char]92, [char]47)
$manifest = @"
{
  "dependencies": {
    "com.virtualmaestro.raindropeffect": "$tarballUrl",
    "com.unity.render-pipelines.universal": "$urpVersion",
    "com.unity.modules.screencapture": "1.0.0",
    "com.unity.modules.imgui": "1.0.0"
  }
}
"@
Write-Utf8NoBom (Join-Path $proj 'Packages\manifest.json') $manifest

foreach ($name in @('Rain-URP.asset', 'Rain-URP.asset.meta', 'Rain-URP-Renderer.asset', 'Rain-URP-Renderer.asset.meta')) {
    Copy-Item -Path (Join-Path $repo "Assets\Settings\$name") -Destination (Join-Path $proj "Assets\Settings\$name") -Force
}

$code = Invoke-Unity $proj 'RainDropEffect.Editor.RainBuildValidation.ConfigureUrp' (Join-Path $proj 'Logs\configure.log')
if ($code -ne 0) {
    Write-Host "ConfigureUrp failed (exit $code). Last 30 lines:"
    Get-Content (Join-Path $proj 'Logs\configure.log') -Tail 30
    if (-not $Keep) { Remove-Item -Recurse -Force $proj }
    exit 1
}

$code = Invoke-Unity $proj 'RainDropEffect.Editor.RainBuildValidation.BuildWindows' (Join-Path $proj 'Logs\build.log')
if ($code -ne 0) {
    Write-Host "Build failed (exit $code). Last 30 lines:"
    Get-Content (Join-Path $proj 'Logs\build.log') -Tail 30
    if (-not $Keep) { Remove-Item -Recurse -Force $proj }
    exit 1
}

Write-Host 'Build report:'
Get-Content (Join-Path $proj 'Builds\Validation\Windows.json')

if ($Keep) {
    Write-Host "Kept: $proj"
} else {
    Remove-Item -Recurse -Force $proj
}

exit 0
