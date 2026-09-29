<#
.SYNOPSIS
    Clean-install gate for com.virtualmaestro.raindropeffect (Task 11).

.DESCRIPTION
    Creates a throwaway Unity project outside the repository whose only dependencies are URP and
    this package (installed by absolute file: path), assigns the shipped URP asset, and builds a
    player. That is the whole point: if anything in the package needs the host project, the MCP
    plugin, NuGet or the test framework, this build fails and the release does not ship.

    Windows PowerShell 5.1 compatible: no pipeline chain operators, no ternary.

.PARAMETER Editor
    Path to Unity.exe. Must be the version in ProjectSettings/ProjectVersion.txt.

.PARAMETER EditorVersion
    Editor version written into the throwaway project's ProjectVersion.txt. Defaults to the version
    in ProjectSettings/ProjectVersion.txt; pass the newer editor's version when testing against it,
    or the project will be opened as an upgrade instead of as a native project.

.PARAMETER Target
    Windows, WebGL or Android.

.PARAMETER Keep
    Keep the temporary project (and its build output) instead of deleting it.

.PARAMETER Run
    Windows only. Launch the built player for a few seconds so it can screenshot itself, then check
    the player log. This is what proves the package renders in a player, not merely that it links.
#>
[CmdletBinding()]
param(
    [string] $Editor = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe',
    [ValidateSet('Windows', 'WebGL', 'Android')]
    [string] $Target = 'Windows',
    [switch] $Keep,
    [switch] $Run,
    [string] $EditorVersion = ''
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$package = Join-Path $repo 'Packages\com.virtualmaestro.raindropeffect'
$editorVersion = $EditorVersion
if ([string]::IsNullOrEmpty($editorVersion)) {
    # The folder name of an editor installed through the Hub is its version, which is what the
    # consumer project must declare when the editor is not the one this repository targets.
    $folder = Split-Path -Leaf (Split-Path -Parent (Split-Path -Parent $Editor))
    if ($folder -match '^\d+\.\d+\.\d+') {
        $editorVersion = $folder
    } else {
        $editorVersion = (Get-Content (Join-Path $repo 'ProjectSettings\ProjectVersion.txt') | Select-Object -First 1).Split(' ')[1]
    }
}
$urpVersion = '17.3.0'

# Unity's manifest parser rejects a UTF-8 BOM; Set-Content -Encoding utf8 writes one on PS 5.1.
function Write-Utf8NoBom([string] $path, [string] $text) {
    [System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
}

function Write-Gap([string] $reason) {
    Write-Host "GAP: $reason"
    exit 2
}

if (-not (Test-Path $Editor)) { Write-Gap "Unity editor not found at $Editor" }
if (-not (Test-Path $package)) { Write-Gap "package not found at $package" }

$moduleByTarget = @{
    'Windows' = 'windowsstandalonesupport'
    'WebGL'   = 'WebGLSupport'
    'Android' = 'AndroidPlayer'
}
$playbackEngines = Join-Path (Split-Path -Parent $Editor) 'Data\PlaybackEngines'
$module = Join-Path $playbackEngines $moduleByTarget[$Target]
if (-not (Test-Path $module)) { Write-Gap "build module for $Target is not installed ($module)" }

# The temp project must live outside the repository: an embedded package inside the consumer would
# defeat the test, and Unity would also index the repo's Library.
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$proj = Join-Path $env:TEMP "RainCleanInstall\$stamp"
Write-Host "Clean-install project: $proj"
Write-Host "Editor: $Editor ($editorVersion), target: $Target"

New-Item -ItemType Directory -Path (Join-Path $proj 'Assets\Settings') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $proj 'Packages') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $proj 'ProjectSettings') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $proj 'Logs') -Force | Out-Null

Write-Utf8NoBom (Join-Path $proj 'ProjectSettings\ProjectVersion.txt') "m_EditorVersion: $editorVersion`n"

# UPM wants forward slashes in a file: path, even on Windows.
$packageUrl = 'file:' + $package.Replace([char]92, [char]47)
# The module packages are the harness's own dependencies, not the package's: a hand-written
# manifest gets no built-in modules, and the generated validation scene needs ScreenCapture to
# photograph itself. The rain package still declares nothing but URP.
$manifest = @"
{
  "dependencies": {
    "com.virtualmaestro.raindropeffect": "$packageUrl",
    "com.unity.render-pipelines.universal": "$urpVersion",
    "com.unity.modules.screencapture": "1.0.0",
    "com.unity.modules.imgui": "1.0.0"
  }
}
"@
Write-Utf8NoBom (Join-Path $proj 'Packages\manifest.json') $manifest

$settings = @('Rain-URP.asset', 'Rain-URP.asset.meta', 'Rain-URP-Renderer.asset', 'Rain-URP-Renderer.asset.meta')
foreach ($name in $settings) {
    Copy-Item -Path (Join-Path $repo "Assets\Settings\$name") -Destination (Join-Path $proj "Assets\Settings\$name") -Force
}

function Invoke-Unity([string] $method, [string] $log) {
    $logPath = Join-Path $proj "Logs\$log"
    $arguments = @('-batchmode', '-quit', '-nographics', '-projectPath', $proj, '-executeMethod', $method, '-logFile', $logPath)
    Write-Host "  unity -executeMethod $method"

    # Neither Start-Process form works here: -Wait also waits for every process Unity spawned
    # (shader compilers, the licensing client), some of which outlive the editor, so the run never
    # returns; -PassThru without -Wait returns a Process whose ExitCode is never populated.
    # Starting it through .NET keeps the handle, so waiting and reading the exit code both work.
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $Editor
    $info.UseShellExecute = $false
    $info.Arguments = ($arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '

    $process = [System.Diagnostics.Process]::Start($info)
    $process.WaitForExit()
    return @{ Code = $process.ExitCode; Log = $logPath }
}

function Stop-Run([int] $code, [string] $log, [string] $message) {
    Write-Host $message
    if (Test-Path $log) { Get-Content $log -Tail 30 }
    if (-not $Keep) { Remove-Item -Recurse -Force $proj }
    exit $code
}

$configure = Invoke-Unity 'RainDropEffect.Editor.RainBuildValidation.ConfigureUrp' 'configure.log'
if ($configure.Code -ne 0) {
    Stop-Run 1 $configure.Log "ConfigureUrp failed (exit $($configure.Code)). Last 30 lines:"
}

$build = Invoke-Unity "RainDropEffect.Editor.RainBuildValidation.Build$Target" 'build.log'
if ($build.Code -ne 0) {
    Stop-Run 1 $build.Log "Build failed (exit $($build.Code)). Last 30 lines of build.log:"
}

$reportPath = Join-Path $proj "Builds\Validation\$Target.json"
if (-not (Test-Path $reportPath)) {
    Stop-Run 1 $build.Log "Build reported success but $reportPath is missing. Last 30 lines of build.log:"
}

Write-Host "Build report ($reportPath):"
Get-Content $reportPath

# The whole point of the gate: nothing editor-only, MCP or NuGet may reach the player.
if ($Target -eq 'Windows') {
    $managed = Join-Path $proj 'Builds\Validation\Windows\RainValidation_Data\Managed'
    if (Test-Path $managed) {
        $leaked = @(Get-ChildItem $managed -Filter *.dll | Where-Object { $_.Name -match 'RainDropEffect\.Editor|McpPlugin|^Microsoft\.' })
        Write-Host "Managed leak check: $($leaked.Count) unwanted assemblies"
        foreach ($dll in $leaked) { Write-Host "  LEAK $($dll.Name)" }
        Write-Host "RainDropEffect.Runtime.dll present: $(Test-Path (Join-Path $managed 'RainDropEffect.Runtime.dll'))"
        if ($leaked.Count -gt 0) {
            if (-not $Keep) { Remove-Item -Recurse -Force $proj }
            exit 1
        }
    }
}

if ($Run -and $Target -eq 'Windows') {
    $exe = Join-Path $proj 'Builds\Validation\Windows\RainValidation.exe'
    $playerLog = Join-Path $proj 'Logs\player.log'
    $shot = Join-Path $proj 'Builds\Validation\Windows\rain-validation.png'

    Write-Host "  running the player"
    $playerArgs = @('-logFile', $playerLog, '-screen-width', '640', '-screen-height', '360', '-screen-fullscreen', '0')
    $player = Start-Process -FilePath $exe -ArgumentList $playerArgs -PassThru

    # The scene screenshots itself after two seconds and quits; do not wait forever if it hangs.
    if (-not $player.WaitForExit(60000)) {
        Write-Host 'Player did not exit within 60 s; killing it.'
        try { $player.Kill() } catch {}
    }

    if (Test-Path $shot) {
        $destination = Join-Path $proj 'Builds\Validation\rain-validation.png'
        Copy-Item -Path $shot -Destination $destination -Force
        Write-Host "Player screenshot: $destination ($((Get-Item $destination).Length) bytes)"
    } else {
        Write-Host "GAP: the player produced no screenshot at $shot"
    }

    if (Test-Path $playerLog) {
        $problems = @(Select-String -Path $playerLog -Pattern 'not found|Shader error|NullReference|Exception' -SimpleMatch:$false)
        Write-Host "Player log problems: $($problems.Count)"
        foreach ($problem in $problems) { Write-Host "  $($problem.Line)" }
        Select-String -Path $playerLog -Pattern '\[RainValidation\]' | ForEach-Object { Write-Host "  $($_.Line)" }
        if ($problems.Count -gt 0) {
            if (-not $Keep) { Remove-Item -Recurse -Force $proj }
            exit 1
        }
    }
}

if ($Keep) {
    Write-Host "Kept: $proj"
} else {
    Remove-Item -Recurse -Force $proj
}
exit 0
