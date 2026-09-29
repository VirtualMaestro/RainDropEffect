# tools

## clean-install.ps1

Proves REQ-01: the package installs into an empty URP consumer project and builds a player without
the host project, samples, demos, the MCP plugin or NuGet.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\clean-install.ps1 -Target Windows
```

Parameters:

| Parameter | Default | Meaning |
| --- | --- | --- |
| `-Editor` | `C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe` | Unity.exe to run in batch mode |
| `-Target` | `Windows` | `Windows`, `WebGL` or `Android` |
| `-Keep` | off | keep the temporary project and its build output |

The script creates `%TEMP%\RainCleanInstall\<timestamp>\`, whose `Packages/manifest.json` declares
only URP and this package (by absolute `file:` path), copies the shipped URP assets, then runs
Unity twice in batch mode: `ConfigureUrp` and `Build<Target>`.

Expected output on success:

```
Clean-install project: C:\Users\<you>\AppData\Local\Temp\RainCleanInstall\20260910-120000
Editor: ...\Unity.exe (6000.3.23f1), target: Windows
  unity -executeMethod RainDropEffect.Editor.RainBuildValidation.ConfigureUrp
  unity -executeMethod RainDropEffect.Editor.RainBuildValidation.BuildWindows
Build report (...\Builds\Validation\Windows.json):
{
  "result": "Succeeded",
  "totalSize": ...,
  "totalTime": "...",
  "errors": 0
}
Managed leak check: 0 unwanted assemblies
RainDropEffect.Runtime.dll present: True
```

Exit codes: 0 success, 1 build failure or a leaked editor/MCP/NuGet assembly, 2 a `GAP:` line —
the editor or the target build module is not installed.
