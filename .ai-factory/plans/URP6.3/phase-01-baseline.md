# Phase 1: Baseline and Compatibility Contract

Plan: [index.md](index.md)
Tasks: 1-2
Depends on: none

## Objective

Freeze reproducible evidence of the legacy asset's appearance and settings before any URP change, and write the migration matrix that every later conversion task (Phase 7) and acceptance check (Phase 8) uses as its contract. No package, no URP install, no runtime rewrite happens in this phase.

## Current-Code Evidence

| Path | Symbols / lines | Why it matters |
|------|-----------------|----------------|
| `Assets/RainDropEffect2/Scripts/Camera/RainCameraController.cs` | `Update()` L149-192: `cam.orthographic = !VRMode`, `orthographicSize = frustumHeight/2` where `frustumHeight = 2*distance*tan(fov/2)`; children placed at `Vector3.forward * distance` | Defines the legacy world unit: the camera plane at `distance` (8.3) with a 60° camera has height 9.584 units. All size/position conversions derive from this. |
| `Assets/RainDropEffect2/Scripts/Common/RainDropTools.cs` | `GetSpawnLocalPos` L263-276: uniform spawn over `camSize`, `p.y += camSize.y*offsetY`; `CreateQuadMesh` L129-162: unit quad ±1 | Spawn area is the full view; `SpawnOffsetY` is a fraction of view height; quad half-extent equals transform scale. |
| `Assets/RainDropEffect2/Scripts/RainBehaviours/SimpleRain/SimpleRainController.cs` | `UpdateShader` L248-274: drift `+= (-gforced.xy) * 0.01 * PosYOverLifetime(progress)` per frame; wind `+= progress * GlobalWind` per frame | Frame-rate dependent motion; the matrix must record the 60 Hz equivalent speeds. |
| `Assets/RainDropEffect2/Scripts/RainBehaviours/FlowRain/FlowRainController.cs` | `UpdateTransform` L279-323: `y = startY - 0.5*t²*accel - v0*t`; fluctuation coroutine `Wait` L410-434 with 0.01 s steps and `rndMax = (int)(100/fluctuationRate)`; `InitializeDrawer` L273: `vertexDistance = Distance*orthoHeight/(Resolution*10)` | Kinematic and stochastic terms to preserve. |
| `Assets/RainDropEffect2/Scripts/RainBehaviours/FrictionFlowRain/FrictionFlowRainController.cs` | `GetNextPositionWithFriction` L323-375: `iter = clamp(150*dt,2,5)`, `resol = clamp(2*8,2,5)`, score `1 - GetPixel(u*W, -v*H).grayscale`, `PickRandomWeightedElement` L306-320 = argmax; `UpdateTransform` L411-425: `downValue = 0.5*t²*accel*0.1 + v0*t*0.01` | Friction semantics and the 0.1 / 0.01 scaling factors that change effective units. |
| `Assets/RainDropEffect2/Scripts/RainBehaviours/StaticRain/StaticRainController.cs` | `UpdateInstance` L136-163: full-screen scale `orthSize/2`; sized quad at `ScreenToWorldPoint(-W*offX + W/2, -H*offY + H/2)` | Static layer placement formula. |
| `Assets/RainDropEffect2/Shaders/Resources/RainDistortion (Forward).shader` | `o.distortion = _Strength * _GrabTexture_TexelSize.x`; `uvgrab.xy -= distortion*norm.rg`; `col += relf.rgb*_Color.rgb*_Color.a`; `col *= saturate(1 - dark*relf.a)`; `col *= 1 - norm.r*_Relief` | Pixel-unit distortion (`_Strength` pixels horizontally) and tint/darkness/relief formulas to replicate. |
| `Assets/RainDropEffect2/Shaders/Resources/RainDropGlobal.cginc` | `ComputeBlur`: 9 horizontal taps, weights 0.05/0.09/0.12/0.15/0.18, offset `texel.x * kern * (alpha*blur*1000)` where `alpha` receives `norm.rg` (truncated to `.r`) | Legacy blur is horizontal and normal-dependent; new blur is an approximation needing acceptance. |
| `Assets/RainDropEffect2/Prefabs/**` (18 prefabs, see GUID table in Phase 7) | Root `Camera` (fov 60, `orthographic size: 5`) + `RainCameraController`, children behaviours | Inventory to capture and to dispose in the matrix. |
| `ProjectSettings/ProjectSettings.asset` | `m_ActiveColorSpace: 0` (Gamma) | Baseline captures are Gamma-space; Phase 2 keeps Gamma so captures remain comparable. |

## Files to Change

| Path | Action | Required change |
|------|--------|-----------------|
| `Assets/Baseline/BaselineDriver.cs` | create (host, temporary; deleted in Task 29) | Play-mode driver that plays one prefab and captures screenshots at fixed times. |
| `Assets/Baseline/Editor/BaselineCapture.cs` | create (host, temporary; deleted in Task 29) | Editor menu that iterates the prefab list and runs the driver. |
| `Assets/Baseline/Baseline.unity` | create (host, temporary) | Capture scene: copy of `Demo1.unity` backdrop with one transparent object added. |
| `.ai-factory/baseline/*.png`, `.ai-factory/baseline/metadata.json` | create | Reference captures and capture metadata. |
| `docs/migration-matrix.md` | create | Migration contract: prefab dispositions, field mapping, unit conversion, shader-formula mapping, tolerances, assumptions. |

## Task 1: Capture legacy baseline

### Intent

Produce the reference images and metadata that Task 27 (visual comparison) and Task 30 (release report) compare against. Captures must be made from the current commit before URP is installed, because after Phase 2 the legacy GrabPass path no longer renders.

### Implementation Steps

1. Tag the current commit so the baseline revision is recoverable without touching `master`:
   `git tag legacy-baseline 2845b4660030d7b36725817a7c98894e121b0326`.
2. Create `Assets/Baseline/Baseline.unity` by duplicating `Assets/RainDropEffect2/Demo/Demo1.unity` in the editor (Ctrl+D on the scene asset, then move). Remove every `RainCameraController` prefab instance from the copy. Add one `Quad` at world position `(0.5, 0.5, 2)` in front of the still-life, material = new material using built-in `Legacy Shaders/Transparent/Diffuse`, color `(0.2, 0.6, 1.0, 0.5)`. This transparent object is the reference for the "transparents refract" contract in Phase 2.
3. Create `Assets/Baseline/BaselineDriver.cs` (runtime MonoBehaviour, global namespace) with:
   - `public string PrefabPath; public string OutputDir; public float[] CaptureTimes = {0.5f, 1.5f, 3.0f}; public int Seed = 1234;`
   - `IEnumerator Start()`: `Random.InitState(Seed)`; `Time.captureDeltaTime = 1f/60f`; load prefab via `Resources.Load` is not available for `Assets/RainDropEffect2/Prefabs`, so the editor script passes the prefab through a static field `BaselineDriver.PendingPrefab` (a `GameObject`) before entering Play Mode; instantiate it as a child of the scene's main camera at local position zero and identity rotation; wait one frame; call `RainCameraController.Play()` on every `RainCameraController` in the instance; for each capture time `t`: `yield return new WaitForSeconds(t - previousT)` then `ScreenCapture.CaptureScreenshot(Path.Combine(OutputDir, $"{name}_{t:0.0}s.png"))` and `yield return new WaitForEndOfFrame()`; then write a line to `metadata.json` (see step 5); finally `EditorApplication.isPlaying = false` through a static callback delegate `System.Action OnFinished` set by the editor script (the runtime script must not reference `UnityEditor`).
4. Create `Assets/Baseline/Editor/BaselineCapture.cs` with menu `RainDrop/Baseline/Capture Next` and `RainDrop/Baseline/Capture All`:
   - Prefab list (13 non-VR prefabs, exact order): `Rain1, Rain2, Rain3, Rain4, Rain5, Rain6, Frozen, BloodRain, WaterSplashIn, WaterSplashOut, Mobile/MobileRain1, Mobile/MobileRain2, Mobile/MobileRain3` under `Assets/RainDropEffect2/Prefabs/`.
   - `Capture All` stores the queue index in `EditorPrefs` key `RainBaseline.NextIndex`, opens `Assets/Baseline/Baseline.unity`, sets `BaselineDriver.PendingPrefab`, sets `OutputDir = <repo>/.ai-factory/baseline`, enters Play Mode; `OnFinished` increments the index and re-enters Play Mode for the next prefab via `EditorApplication.delayCall`. Because Play Mode reloads the domain, keep the queue index in `EditorPrefs`, not in a static field.
   - Before entering Play Mode, verify the Game view is set to a fixed 1280×720 resolution; if `Screen.width != 1280 || Screen.height != 720` when the driver starts, log `[RainBaseline] ERROR Game view must be 1280x720 fixed resolution` and abort. The user sets this once through the Game view resolution dropdown ("Add" → 1280×720).
5. `metadata.json` fields per capture: `prefab`, `time`, `file`, `editorVersion` (`Application.unityVersion`), `commit` (`2845b466…`), `resolution`, `colorSpace` (`QualitySettings.activeColorSpace`), `seed`, `shaderType` (root `RainCameraController.ShaderType`), `maxDrawCall` (root `RainCameraController.MaxDrawCall`). Append entries into one JSON array (rewrite the file each time from an in-memory list serialized with `JsonUtility` wrapper class `BaselineMetadata { List<Entry> entries; }`).
6. Run `Capture All`. Confirm 39 PNG files (13 prefabs × 3 times) plus `metadata.json` exist. Commit them (PNG at 720p is roughly 1 MiB each; total under 50 MiB is acceptable for this repository).
7. If a prefab does not render on `6000.3.23f1` (blank or magenta), do not fabricate a capture: keep the PNG, and add `"renderFault": "<description>"` to its metadata entry.

### Required Interfaces and Contracts

- `BaselineDriver` is host-only test tooling; it must not be referenced by any package code.
- File naming: `<PrefabName>_<t>s.png`, for example `Rain1_0.5s.png`, `MobileRain3_3.0s.png`.
- Capture scene, seed, times, and resolution are the same values reused by Task 27 for new captures.

### Error Handling and Logging

- Missing prefab path: `Debug.LogError("[RainBaseline] prefab not found: <path>")`, skip to next.
- Resolution mismatch: error above, abort the whole run.
- Log `[RainBaseline] captured <file>` per file (this is a one-off tool; per-frame logging is not involved).

### Tests

Not applicable for one-off capture tooling; Task 27 verifies that the driver reproduces captures by re-running it on the new package.

### Acceptance Criteria

- `git tag legacy-baseline` exists and points to `2845b466`.
- `.ai-factory/baseline/` contains 39 PNGs and `metadata.json` with 39 entries, all `resolution == "1280x720"`, `colorSpace == "Gamma"`.
- At least `Rain1_1.5s.png` visibly shows distorted drops over the transparent quad and the still-life.

### Verification

- `ls .ai-factory/baseline | wc -l` → expected `40`.
- `git tag --list legacy-baseline` → expected one line.
- Open three PNGs (`Rain1_1.5s.png`, `Frozen_3.0s.png`, `MobileRain3_1.5s.png`) and confirm rain is visible; record any `renderFault` entries in `docs/migration-matrix.md` §Baseline notes.

## Task 2: Write the migration matrix

### Intent

Convert the evidence above into a single contract document that Task 26 (converter) implements and Task 27 (visual report) checks. Every later question of "what is the new value/unit/disposition" must be answerable from this file.

### Implementation Steps

1. Create `docs/migration-matrix.md` with these sections, in this order: `Scope and assumptions`, `Prefab dispositions`, `Component and field mapping`, `Unit conversion`, `Shader formula mapping`, `Timing conversion`, `Tolerances`, `Baseline notes`.
2. `Scope and assumptions` — copy verbatim the working assumptions from the plan index (`index.md` §Architecture and Decisions D1–D4): no XR/VR, no world-space glass, new major API without compatibility facade, provisional device budgets, Gamma color space retained.
3. `Prefab dispositions` — table with all 18 prefabs:
   - `Rain1..Rain6`, `Frozen`, `WaterSplashIn`, `WaterSplashOut` → converted to a `RainProfile` asset of the same name under the package `Runtime/Profiles/`, plus a sample camera prefab in `Samples~/EffectsGallery/Prefabs/`.
   - `BloodRain` → three profiles `BloodFrame` (static), `BloodSplatter` (simple, one-shot), `BloodFlow` (friction) plus sample script `BloodRainSample.cs` (HP logic) in `Samples~/EffectsGallery/Scripts/`.
   - `Mobile/MobileRain1`, `Mobile/MobileRain2` → profiles `MobileRain1`, `MobileRain2` with `Mode = Lens`, `OverlayColor.a = 0`, `Relief = 0`, `Darkness = 0`, `Blur = 0` on every layer (legacy Cheap shader ignored those inputs) and a sample note "use `Quality = Low`".
   - `Mobile/MobileRain3` → profile `MobileRain3` with every layer `Mode = Overlay` (legacy NoDistortion).
   - `VR/*` (5 prefabs) → `not converted` (assumption D1); remain available on `master` and tag `legacy-baseline`.
4. `Component and field mapping` — one table per legacy type listing every serialized field from the Phase 1 evidence and its destination (new type.field) or `dropped (reason)`. Required rows:
   - `RainCameraController.Alpha → RainEffect.Intensity`; `GlobalWind → RainEffect.Wind` (converted, see units); `GForceVector → RainEffect.Gravity`; `ShaderType → RainEffect.Quality (Expensive→High, Cheap→Low, NoDistortion→Low) and layer Mode (NoDistortion→Overlay)`; `RenderQueue, distance, VRMode → dropped (renderer no longer uses a camera plane)`.
   - `RainBehaviourBase.Depth → layer order (ascending Depth, stable by family order Static, Simple, Flow, Friction, then child index)`.
   - `SimpleRainVariables.*`, `StaticRainVariables.*`, `FlowRainVariables.*`, `FrictionFlowRainVariables.*` → fields of `SimpleLayerSettings`, `StaticLayerSettings`, `FlowLayerSettings`, `FrictionLayerSettings` exactly as declared in Phase 4 Task 12. Curves copy verbatim. `OverlayTexture → OverlayTexture`, `NormalMap → NormalMap`, plus new `CoverageMask` = the baked mask for that overlay/normal pair (Task 8). `FrictionMap → FrictionField` asset baked by Task 21.
   - `BloodRainCameraController.* → BloodRainSample.*` (sample script; `FrameBloodCamera/SplatterBloodCamera` become two `RainEffect` references).
5. `Unit conversion` — define the new normalized unit: **1.0 = half of the viewport height; x spans ±aspect**. Define `k = 1 / (distance × tan(fov/2))`, read per prefab from the root camera (`fov 60`, `distance 8.3` → `k = 0.20868`). Rows:
   - sizes (`SizeMinX/MaxX/MinY/MaxY`, `SizeX/SizeY`, flow width `SizeMinX/MaxX`): `× k`.
   - emitter `SpawnOffsetY`: normalized offset `2 × SpawnOffsetY` (legacy shifts by `camSize.y × offset`).
   - static `SpawnOffsetX/Y`: stored unchanged as fractions; runtime center `(-2·offX·aspect, -2·offY)`; `FullScreen` unchanged.
   - flow `Amplitude` → `LateralAmplitude = 0.1 × Amplitude × k`; `Smooth` unchanged (`LateralSmooth`); `fluctuationRateMin/Max` unchanged (events per second).
   - flow `InitialVelocity`, `AccelerationMin/Max`: `× k`.
   - friction `InitialVelocity`: `× 0.6 × k`; `AccelerationMin/Max`: `× 3 × k`. Legacy advances the trail **every frame** by the total kinematic displacement since spawn (`downValue = 0.05·a·t² + 0.01·v0·t` from the current position, `FrictionFlowRainController.cs:415-417`), so the effective speed at 60 Hz is `60 × downValue = 3·a·t² + 0.6·v0·t` units/s. The new model integrates that speed: `step = (Accel·t² + InitialVelocity·t) × dt`.
   - trail `Resolution` → `PointSpacing = max(2·k·distance·tan(fov/2)·2/(Resolution·10) , 0.008)`; with defaults this is `max(1.659/Resolution, 0.008)`.
   - simple drift: `DriftSpeed = 0.01 × 60 × k = 0.1252` normalized units per second; direction is `−Down × DriftOverLifetime(p)` (legacy `+= (−gforced) × 0.01 × PosYOverLifetime`, `SimpleRainController.cs:270`; the shipped curves are negative, e.g. `Rain1` `−0.95`, so drops fall); `PosYOverLifetime → DriftOverLifetime` copied verbatim, no sign change.
   - wind: `RainEffect.Wind = GlobalWind × 60 × k`, applied cumulatively as `pos += Wind × progress × dt` in every family. Legacy Simple/Friction accumulate per frame; legacy Flow applies an instantaneous `progress × GlobalWind` offset (`FlowRainController.cs:322`). All 18 prefabs ship `GlobalWind = (0,0)`, so this unification is tolerance (g), not a visible change.
   - `Distortion` (legacy `DistortionValue`): unchanged number; new semantic "pixels at 1080p reference height".
   - `Blur`: Simple/Static `Blur01 = clamp(Blur / 2, 0, 1)`; Flow/Friction `Blur01 = clamp(Blur / 20, 0, 1)`.
   - `Relief`, `Darkness`, `OverlayColor`, `FadeTime`, `Duration`, `Delay`, `Lifetime*`, `EmissionRate*`, `MaxRainSpawnCount → MaxCount`, `AutoStart`, `PlayOnce`, `AutoRotate`: unchanged.
6. `Shader formula mapping` — table legacy expression → new expression, taken from the evidence table above and from Phase 2 Task 6's shader contract: distortion offset, overlay tint, darkness, relief, overlay-only alpha (`tex.r*tex.g*tex.b`), blur (legacy horizontal 9-tap normal-scaled → new half-resolution horizontal 9-tap weighted by `saturate(blur × |n.x| × 8)`), coverage (new; `CoverageMask.r × opacity`).
7. `Timing conversion` — state that all motion is integrated with clamped `Time.deltaTime` (max 0.1 s); flow vertical motion uses closed-form `y0 - (0.5·a·t² + v0·t)`; friction motion integrates the legacy 60 Hz-equivalent speed `(Accel·t² + InitialVelocity·t)` per tick along the friction-guided path; fluctuation timers sample `-ln(1-u)/rate`; trail ages use simulation time (not `Time.time`).
8. `Tolerances` — accepted differences for Task 27: (a) overlapping drops refract undistorted scene (no recursive refraction); (b) blur shape differs (half-res, symmetric weights, bounded radius); (c) friction tie-breaking is seeded random among maxima; (d) lateral flow jitter is a lerp toward target, not `Vector3.Slerp`; (e) per-frame drift/friction-step converted to per-second rates at 60 Hz equivalence; (f) trails capped by `MaxPoints` per quality; (g) wind is cumulative in every family (legacy Flow used an instantaneous offset; all shipped presets have zero wind). Comparison is by side-by-side inspection at the three capture times plus a numeric mean absolute RGB difference reported per image (informational, no pass threshold).
9. `Baseline notes` — list any `renderFault` entries from Task 1.

### Required Interfaces and Contracts

- The matrix is the single source for Task 26's conversion constants; Task 26 must not invent additional factors.
- Field names in the mapping must match Task 12's declarations character for character; when Task 12 is implemented, re-open this file and correct any mismatch (matrix follows code, code follows plan).

### Error Handling and Logging

- Documentation only. Any unresolved mapping is written as `TODO-DECISION: <question>` and listed in the plan index's open questions before Phase 7 starts.

### Tests

Not applicable (documentation). Task 26's converter tests use this matrix's constants.

### Acceptance Criteria

- All 18 prefabs have a disposition row.
- Every serialized field from all four `*Variables` classes, `RainCameraController`, `RainBehaviourBase`, and `BloodRainCameraController` appears in the mapping tables (count: SimpleRain 28 fields, StaticRain 15, FlowRain 32, FrictionFlowRain 29, camera 7, base 1, blood 8).
- Unit conversion table includes `k` with its formula and the default value `0.20868`.

### Verification

- `grep -c "^| " docs/migration-matrix.md` → at least 140 table rows.
- `grep -n "TODO-DECISION" docs/migration-matrix.md` → expected no output, or each hit is mirrored in `index.md` open questions.

## Phase Risks and Mitigations

- Risk: the legacy project does not render correctly on Unity 6000.3.23f1 (GrabPass under Built-in RP still works in Unity 6, but the project was authored for Unity 5.x).
  Mitigation: record `renderFault` per capture and rely on source-derived values in the matrix; never manufacture reference images.
- Risk: capture nondeterminism (random spawn positions differ between runs).
  Mitigation: fixed seed, `captureDeltaTime = 1/60`, identical capture scene and times; comparison is visual-intent based, not pixel-exact.

## Phase Completion Checklist

- Every Task N in this phase satisfies its acceptance criteria.
- Required verification commands pass.
- `index.md` task checkboxes are updated immediately after verified completion.
