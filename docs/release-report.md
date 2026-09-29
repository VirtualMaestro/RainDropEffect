# Release Report — Rain Drop Effect URP 2.0.0

The executed evidence matrix for the 2.0.0 release (Tasks 30 and 31). Every row cites a test name, a
JSON file, an image or a log. A row without evidence is a `GAP`, never a pass; "it looked fine" is
not in this document.

Machine unless a row says otherwise: editor `6000.3.23f1`, URP `17.3.0`, Windows 11 (10.0.26200),
Intel Core i9-14900HX, NVIDIA GeForce RTX 4080 Laptop GPU, Direct3D 11.

## How the automated rows were produced

Rendering, URP configuration, Camera policy, State and Ownership are executed by two PlayMode suites
in the package — `ReleaseRenderingTests` and `ReleaseStateTests`. Each test renders into a
RenderTexture and asserts on pixels, or drives the simulation and asserts on state, then records its
row into `Logs/release-matrix.json`. A row is written only after its assertions pass, so this
document and the test run cannot disagree.

```powershell
Unity -batchmode -runTests -projectPath . -testPlatform PlayMode -testResults Logs/playmode.xml
```

Results: EditMode 107/107, PlayMode 49/49
([`evidence/editmode.xml`](evidence/editmode.xml), [`evidence/playmode.xml`](evidence/playmode.xml)).

## Installation

| Case | Method | Result | Evidence |
| --- | --- | --- | --- |
| Clean install builds a Windows player | `tools/clean-install.ps1 -Target Windows` in a throwaway project whose manifest names only URP and this package | PASS | `docs/support-matrix.md` §Clean install, `evidence/…` build report `Succeeded`, 0 errors |
| The player renders rain | `-Run`: the built player plays the Basic preset, screenshots itself and quits | PASS | [`images/rain-validation-windows.png`](images/rain-validation-windows.png), player log: 0 problems |
| Clean install builds a WebGL player | `tools/clean-install.ps1 -Target WebGL` | PASS | `docs/support-matrix.md` §Clean install, 9.6 MiB, 0 errors |
| Both samples import and open with no missing scripts | `Rain Drop Effect/Validation/Import Samples`, then the missing-script audit opens every scene | PASS | `Logs/missing-scripts.json`: 19 prefabs and scenes opened (13 camera prefabs, the three sample scenes, the two host scenes), 0 missing scripts |
| Nothing the package ships points outside it | `Rain Drop Effect/Validate Package References` | PASS | [`evidence/reference-audit.json`](evidence/reference-audit.json): 57 assets, `"external": []` |
| The player carries no editor or tooling assemblies | Managed-folder scan in the clean-install script | PASS | `Managed leak check: 0 unwanted assemblies`, `RainDropEffect.Runtime.dll present: True` |
| Shader variants stay minimal | `ShaderVariantReport` during the clean-install build | PASS | [`evidence/shader-variants.json`](evidence/shader-variants.json): Lens pass 0 fragment = 2 variants, Blur = 1, Overlay = 1 |

### Rendering

| Case | Method | Result | Evidence |
| --- | --- | --- | --- |
| Camera alpha preserved | `CameraAlphaIsPreserved` | PASS | ARGB32 target cleared to alpha 0 reads back alpha 0 |
| Camera rendering to a RenderTexture | `RenderTextureOutputCameraRenders` | PASS | 256x256 RenderTexture target |
| Edge drop clamps, does not wrap | `EdgeDropIsClampedNotWrapped` | PASS | right edge stays blue-dominant: r129 < b255 against a red left half |
| Opaque + transparent content refracted | `OpaqueAndTransparentContentIsRefracted` | PASS | centre pixel changes over a scene holding an opaque cube and a transparent quad |
| Overlapping drops do not erase earlier drops | `OverlappingDropsDoNotEraseEachOther` | PASS | single delta 115, overlapped delta 115 |
| Portrait and landscape | `PortraitAndLandscapeBothRender` | PASS | 180x320 and 320x180 render targets |
| Two effects on one camera blend | `TwoEffectsOnOneCameraBlend` | PASS | both components draw in component order |

### URP configuration

| Case | Method | Result | Evidence |
| --- | --- | --- | --- |
| Dynamic resolution | `DynamicResolutionRenders` | PASS | ScalableBufferManager.ResizeBuffers(0.7, 0.7) |
| HDR on and off | `HdrOnAndOffBothRender` | PASS | supportsHDR false and true |
| MSAA off / 2x / 4x | `MsaaLevelsAllRender` | PASS | msaaSampleCount 1, 2 and 4 |
| Perspective and orthographic | `PerspectiveAndOrthographicBothRender` | PASS | both projections render the drop |
| Post-processing on and off | `PostProcessingOnAndOffBothRender` | PASS | renderPostProcessing false and true |
| Render scale 0.5 and 1.0 | `RenderScaleHalfAndFullBothRender` | PASS | renderScale 0.5 and 1.0 |

### Camera policy

| Case | Method | Result | Evidence |
| --- | --- | --- | --- |
| Preview and reflection cameras show no rain | `PreviewAndReflectionCamerasShowNoRain` | PASS | the rendered centre differs from the Game camera's for CameraType.Preview and Reflection |
| Simulation ticks once per frame with three cameras | `SimulationTicksOncePerFrameWithThreeCameras` | PASS | 2 layers / 1 batches with one camera and with three |
| Two independent cameras render side by side | `TwoCamerasRenderIndependently` | PASS | two cameras, two RenderTextures, one profile |

### State

| Case | Method | Result | Evidence |
| --- | --- | --- | --- |
| Delay 2 s then Clear at 1 s never spawns | `ClearDuringDelayNeverSpawns` | PASS | 0 batches after 3 further seconds |
| Intensity 0 produces no passes | `ZeroIntensityProducesNoBatches` | PASS | batch count drops to 0 |
| Long frame clamped to 0.1 s | `LongFrameIsClampedToOneTenthOfASecond` | PASS | a 3 s delta advances the state exactly as a 0.1 s delta does |
| Play() during the delay is ignored | `RestartDuringDelayIsIgnored` | PASS | nothing at 1 s, batches present after 2 s |
| timeScale 0 freezes, non-zero advances | `TimeScaleZeroFreezesTheSimulation` | PASS | identical sample across 30 zero-length ticks |

### Ownership

| Case | Method | Result | Evidence |
| --- | --- | --- | --- |
| 100 play/stop/clear cycles do not grow | `HundredPlayStopClearCyclesDoNotGrow` | PASS | meshes 29 -> 29, allocated delta -128 B |
| 20 rebuilds across all quality tiers do not grow resources | `RepeatedRebuildDoesNotGrowResources` | PASS | meshes 29 -> 6, materials 67 -> 44 |
| Disable and destroy release the camera registry | `DisableAndDestroyReleaseTheRegistry` | PASS | registry count 1 -> 0 -> 1 -> 0 |
| Five target resizes create no resources | `ResizingTheTargetDoesNotGrowResources` | PASS | meshes stayed at 6 |

## Web

| Case | Method | Result | Evidence |
| --- | --- | --- | --- |
| WebGL2 runs the effect in Chrome | The benchmark scene built for WebGL2, served over `http://localhost`, ten scenarios × 600 frames | PASS | [`evidence/benchmark-WebGL-1280x720.json`](evidence/benchmark-WebGL-1280x720.json); ANGLE/D3D11, canvas 960 × 600 |
| WebGL2 holds the refresh rate | Frame time p50 and p95 per scenario | PASS | 17.0 ms for every scenario including `Bench_Combined` High — no dropped frame in 600 |
| WebGL2 allocates nothing per frame | `GC.CollectionCount(0)` delta per scenario | PASS | 0 for all ten scenarios on IL2CPP |
| WebGL2 GPU milliseconds | — | GAP | `FrameTimingManager` reports no GPU time in a browser, and deriving it from the frame rate would be a guess |
| Edge and Firefox | — | GAP | only Chrome was run; the build and the harness are the same for the other two |
| Background tab and resume | — | GAP | needs a driven browser session; the delta-time clamp behind it is covered by `LongFrameIsClampedToOneTenthOfASecond` |
| Android Chrome, iOS Safari | — | GAP | no mobile device on this machine |

## Android

| Case | Result | Evidence |
| --- | --- | --- |
| Any Android row | GAP | No device is attached (`adb devices` lists none). The build module is installed; `RainBuildValidation.BuildAndroid` and `BenchmarkDriver` are ready for one. |

## iOS

| Case | Result | Evidence |
| --- | --- | --- |
| Any iOS row | GAP | No macOS build host and no device. `docs/support-matrix.md` lists iOS as unsupported-untested. |

## Desktop and sustained

| Case | Method | Result | Evidence |
| --- | --- | --- | --- |
| Windows D3D11 measured budgets | Development player, ten scenarios × 600 frames | PASS | [`evidence/benchmark-WindowsPlayer-1280x720.json`](evidence/benchmark-WindowsPlayer-1280x720.json), `docs/performance.md` |
| Windows allocates nothing per frame | `GC.CollectionCount(0)` delta per scenario | PASS | 0 for all ten scenarios |
| Sustained 12-minute run | `tools/run-benchmark.ps1 -Sustained`: `Bench_Combined` at `Balanced`, 71 ten-second buckets | PASS | `Logs/sustained-WindowsPlayer.json`; p95 0.846 ms in minute 1 vs 0.836 ms in minute 12 — no thermal drift, 5 focus changes with no burst on resume |

## Packaging and version policy

| Case | Method | Result | Evidence |
| --- | --- | --- | --- |
| The package exports as a tarball | `tools/pack-and-install.ps1` → Package Manager `Client.Pack` | PASS | `Builds/Package/com.virtualmaestro.raindropeffect-2.0.0.tgz`, 55 947 239 B |
| The tarball carries what it should and nothing else | `tar -tzf` content check | PASS | 467 entries; `Runtime`, `Editor`, `Samples~`, `Documentation~`, `Tests` present; no `Assets/`, no `.dll`, no `Logs/` |
| The tarball installs in a project that has never seen this repository | Second throwaway project, manifest points at the `.tgz` | PASS | Windows build `Succeeded`, 0 errors |
| The declared minimum editor is 6000.3 | `PackageMetadataTests` | PASS | `UnityMinimumIs6000_3`, `VersionIsSemver`, `DependenciesContainOnlyUrp`, `SamplesPathsExist` |
| A newer editor builds the package | `tools/clean-install.ps1 -Editor …6000.5.10f1…` | PASS | 6000.5.10f1 resolves URP 17.5.0 |
| `master` is untouched | `git rev-parse master` | PASS | `901824a26f669c0b69bd190a0bfafcb318335875` |

One source change was needed for 6000.5 and it is not a version branch: `Object.GetInstanceID()` is
obsolete-as-error there in favour of `GetEntityId()`, which 6000.3 does not have. Both call sites only
needed a per-object key for the once-only warnings, so they now use `RuntimeHelpers.GetHashCode`.

## Gaps, with reasons

Each of these needs something this machine or this harness does not have. None is a failure; every
one is a check that was not run.

| Area | Why it is a gap |
| --- | --- |
| Frame Debugger / Render Graph Viewer captures | Both are interactive editor windows. The pass set they would show is asserted instead by `RainRenderPassTests` and `RainBlurPassTests`, and derived in `docs/performance.md` §Pass counts. |
| Release screenshots under `docs/images/release/` | Replaced by pixel assertions: every rendering row above reads back the framebuffer and compares channels, which is stricter than an image somebody looked at. |
| Universal Renderer Forward+ and Deferred | Switching `UniversalRendererData.renderingMode` at runtime needs the renderer data asset, which a player build does not have. Forward is the verified configuration; the rain pass runs after transparents and does not read the G-buffer, so no difference is expected. |
| Scene View preview on/off | Needs an editor Scene View window. The opt-in path is `RainEffect.PreviewInSceneView` and `RainRendererFeature`'s `CameraType.SceneView` branch. |
| Editor Play Mode pause/resume, domain-reload-disabled cycles | Editor-only interactions with no batch equivalent. |
| Scene reload 10×, renderer-feature remove/re-add at runtime | Not automated. The closest executed checks are `HundredPlayStopClearCyclesDoNotGrow`, `RepeatedRebuildDoesNotGrowResources` and `DisableAndDestroyReleaseTheRegistry`. |
