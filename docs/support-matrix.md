# Support Matrix

The only place editor, package and platform support is asserted for Rain Drop Effect URP.
Later tasks append rows; verified rows are never rewritten. Anything untested is a named `GAP`.

## Verified combinations

| Editor | URP | Core | Shader Graph | Test Framework | Platform | Status |
| --- | --- | --- | --- | --- | --- | --- |
| 6000.3.23f1 | 17.3.0 | 17.3.0 | 17.3.0 | 1.6.0 | Windows D3D11 editor | verified in Task 3 |
| 6000.3.23f1 | 17.3.0 | 17.3.0 | 17.3.0 | 1.6.0 | Windows D3D11 player | Task 24 clean-install `-Run` and Task 25 benchmark player |
| 6000.3.23f1 | 17.3.0 | 17.3.0 | 17.3.0 | 1.6.0 | WebGL2 (Chrome, ANGLE/D3D11) | Task 25 benchmark: 10/10 scenarios held 60 fps, GC delta 0 |
| 6000.5.10f1 | 17.5.0 | 17.5.0 | 17.5.0 | — | Windows D3D11 player | Task 32 clean install: build `Succeeded`, 0 errors, 103 830 284 B, 0 leaked assemblies |

The newer editor needed one source change, not a version branch (D3 forbids those): 6000.5 marks
`UnityEngine.Object.GetInstanceID()` obsolete-as-error in favour of `GetEntityId()`, which 6000.3 does
not have. The two call sites only needed a per-object key for the once-only warnings, so they use
`RuntimeHelpers.GetHashCode` — a managed identity hash that both editors accept.

All four packages resolve as `builtin` for this editor (`Packages/packages-lock.json`), so the patch
is pinned by the editor version rather than by a hand-written manifest entry.

Host pipeline asset: `Assets/Settings/Rain-URP.asset` with `Assets/Settings/Rain-URP-Renderer.asset`
(Universal Renderer, Forward, Intermediate Texture = Auto, post-processing enabled). Pipeline
settings: HDR off, MSAA disabled, render scale 1, opaque texture off, depth texture off. Assigned as
Default Render Pipeline in Graphics and on all 6 quality levels. Color space stays Gamma (D4).
These are development defaults; Task 7 varies them per test case and restores them.

## Clean install

Produced by `tools/clean-install.ps1`: a throwaway project outside the repository whose
`Packages/manifest.json` declares only URP and this package (absolute `file:` path). No MCP plugin,
no NuGet, no test framework, no samples. Rows are appended, never rewritten.

| Date | Editor | URP | Target | Result | Player size | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| 2026-09-10 | 6000.3.23f1 | 17.3.0 | StandaloneWindows64 | Succeeded, 0 errors | 95 291 993 B (90.9 MiB) | Managed folder carries `RainDropEffect.Runtime.dll` and none of `RainDropEffect.Editor`, `McpPlugin`, `Microsoft.*` (124 assemblies total). |
| 2026-09-10 | 6000.3.23f1 | 17.3.0 | WebGL | Succeeded, 0 errors | 10 103 813 B (9.6 MiB) | IL2CPP; managed-assembly check does not apply (stripped into wasm). Browser run is Task 31. |
| 2026-09-10 | 6000.3.23f1 | 17.3.0 | StandaloneWindows64 `-Run` | Succeeded, 0 errors | 101 674 286 B (97.0 MiB) | Player launched, rendered rain and screenshotted itself: `docs/images/rain-validation-windows.png`. 0 player-log problems, 0 leaked assemblies. `Logs/shader-variants.json` written by the same build. |

`tools/clean-install.ps1 -Run` additionally launches the built Windows player, which plays the Basic
Rain preset, screenshots itself and quits, so the gate proves the package *renders* in a player and
not merely that it links.

The player is built from a generated `Assets/RainValidation/Validation.unity` holding one camera with
`RainEffect` — the smallest scene that proves the component survives into a player build.

Two harness bugs kept this gate from ever completing before 2026-09-10, and both are worth knowing
if the script is ever rewritten: `Start-Process -Wait` waits for every process Unity spawned, some of
which outlive the editor, so the run never returned; and a hand-written consumer manifest declares no
built-in module packages, so the generated validation scene could not see `ScreenCapture`.

## Packaging

Produced by `tools/pack-and-install.ps1`: Package Manager exports the tarball, its contents are
checked, and it is installed into a second throwaway project that has never seen this repository.

| Date | Editor | Tarball | Entries | Contents | Consumer build |
| --- | --- | --- | --- | --- | --- |
| 2026-09-10 | 6000.3.23f1 | `com.virtualmaestro.raindropeffect-2.0.0.tgz`, 55 947 239 B | 467 | `Runtime`, `Editor`, `Samples~`, `Documentation~`, `Tests` all present; no `Assets/`, no `.dll`, no `Logs/` | Succeeded, 0 errors |

## Gaps

| Area | Status |
| --- | --- |
| WebGL2 GPU milliseconds | GAP — `FrameTimingManager` reports no GPU time in a browser |
| WebGL2 in Edge and Firefox | GAP — only Chrome was run; the build and the harness are the same for the other two |
| iOS Metal | GAP — no macOS build host and no device; unsupported-untested |
| Android Vulkan/GLES | GAP — no device attached (`adb devices` lists none); the build module is installed |
| XR / VR | not supported in 2.0.0 (decision D1) |
| World-space glass | not supported in 2.0.0 (decision D1) |
