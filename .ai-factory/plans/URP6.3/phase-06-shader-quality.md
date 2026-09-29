# Phase 6: Shader Quality, Textures and GPU Optimization

Plan: [index.md](index.md)
Tasks: 23-25
Depends on: Phase 5 / Tasks 19-22 (all families), Phase 2 / Task 7 (renderer decision and measurements)

`<pkg>` means `Packages/com.virtualmaestro.raindropeffect`.

## Objective

Finish the GPU side: demand-driven shared blur gated by quality, correct texture roles and platform compression, a verified small shader-variant set with serialized build inclusion, and measured Low/Balanced/High budgets on Windows, WebGL2 and Android. Optional optimizations (field composition, atlasing, Jobs/Burst) are decided here by measurement and recorded, not assumed.

## Current-Code Evidence

| Path | Symbols / lines | Why it matters |
|------|-----------------|----------------|
| `<pkg>/Runtime/Rendering/RainRenderPass.cs` (Task 5) | blur branch: half-resolution `_RainSceneBlur` via `AddBlitPass(BlitMaterialParameters)` when `anyBlur` | Already demand-driven; this phase validates cost and radius. |
| `<pkg>/Runtime/Shaders/RainLens.shader` (Task 6) | `_RAIN_BLUR` keyword, `lerp(col, blurred, saturate(blur·|n.x|·8))` | Only variant dimension; weight constant is tuned here. |
| `<pkg>/Runtime/Shaders/RainBlur.shader` (Task 6) | 9-tap horizontal, `_RainBlurRadius`, `_RainBlurTexel` | Radius per quality from `RainQualityCaps.BlurRadius`. |
| `<pkg>/Runtime/Settings/RainQualityCaps.cs` (Task 12) | `BlurEnabled(Low) == false`, radius 2/3 | Quality policy. |
| `<pkg>/Runtime/Textures/**` importer settings (Task 9) | roles set; no per-platform overrides yet | Task 24 adds platform overrides after builds. |
| `Assets/RainDropEffect2/Shaders/Resources/*.shader` | legacy shaders still present | Removed in Task 29; Task 24 confirms no runtime reference remains. |
| `Logs/benchmark-cpu.json` (Task 22), `docs/renderer-decision.md` §Measurements (Task 7) | CPU p95 and GPU pass timings | Inputs to the optimization disposition. |

## Files to Change

| Path | Action | Required change |
|------|--------|-----------------|
| `<pkg>/Runtime/Rendering/RainRenderPass.cs` | modify | Blur texel/radius plumbing for scaled descriptors; skip blur when no lens layer is visible (Task 23). |
| `<pkg>/Runtime/Shaders/RainLens.shader`, `RainBlur.shader` | modify | Final blur weighting and precision annotations (Task 23). |
| `<pkg>/Runtime/Textures/**/*.meta` | modify | Platform overrides (Task 24). |
| `<pkg>/Editor/BuildValidation/ShaderVariantReport.cs` | create | Post-build shader variant report (Task 24). |
| `Assets/Host/Benchmark.unity`, `Assets/Host/BenchmarkDriver.cs`, `<pkg>/Runtime/Profiles/Bench_*.asset` | create | Benchmark scene, driver and three profiles (Task 25). |
| `docs/performance.md`, `docs/support-matrix.md` | create/modify | Measurements and disposition (Task 25). |

## Task 23: Finalize shared blur and quality gating

### Intent

Deliver the "bounded shared blur" as designed: generated once per camera per frame only when a visible lens layer requests it, at half resolution, with a small fixed horizontal kernel, disabled at Low quality, and visually tuned against the baseline.

### Implementation Steps

1. In `RainRenderPass.RecordRenderGraph`, compute the blur source size correctly for both descriptor modes: when `desc.sizeMode == TextureSizeMode.Scale`, use `cameraData.scaledWidth/scaledHeight` for `_RainBlurTexel` (`1/w, 1/h, w, h`); otherwise use `desc.width/height`. The blur output is half of that size. Set `blurMaterial.SetFloat(_RainBlurRadius, blurRadius)` where `blurRadius` comes from `RainRenderData.BlurRadius` (max across effects on the camera).
2. `RainBlur.shader`: sample the source at full resolution while writing half resolution; taps at `uv.x + texel.x × kern × radius` where `texel` is the source texel; clamp `uv` to `[0,1]`. Keep 9 taps and legacy weights. Add `#pragma target 3.0` and `half` for colors.
3. `RainLens.shader`: keep `w = saturate(blur × |n.x| × 8)`; after Task 27's A/B on `Rain1`/`Rain2` (blur-enabled presets), if the visual comparison shows too little blur, change the constant to 16 (one bounded retune, recorded in `docs/performance.md` §Blur tuning). Declare `_RainSceneBlur` sampling with `sampler_LinearClamp`.
4. Quality gating (already in `LayerRuntime.UsesBlur`): add `RainEffect.OnValidate` → `rebuildRequested` when `Quality` changes (Task 13 already covers `Quality`); verify that switching to `Low` removes the `Rain Blur Scene Color` pass in the Frame Debugger.
5. Demand-driven check: with a profile whose layers all have `Blur = 0`, no blur pass is recorded (already true via `AnyBlur`); with `Blur > 0` on an invisible layer (drained), no blur pass either (`UsesBlur` is per layer, `AnyBlur` is only set for visible layers — confirm the `Emit` loop sets `AnyBlur` only when `l.Visible`).
6. Memory: record in `docs/performance.md` the RT budget per resolution: copy `w×h×4 B` (LDR) + blur `(w/2)×(h/2)×4 B`; compare with the research ceilings (16 MiB at 720p LDR, 32 MiB at 1080p LDR) using Render Graph Viewer's resource sizes. HDR doubles the copy; document separately.

### Required Interfaces and Contracts

- Blur radius (half-res pixels): Balanced 2, High 3, Low none. Kernel shape fixed (9 horizontal taps).
- One blur texture per camera per frame regardless of layer count (shared).

### Error Handling and Logging

- Blur material null (shader set missing on the feature): blur silently disabled and `RainLog.Error` once in `Create` (Task 5).

### Tests

- `<pkg>/Tests/Runtime/RainBlurPassTests.cs` (PlayMode): render the Basic profile with `Blur = 0.8` on the static layer at `Quality High` into a RenderTexture (through `RenderPipeline.SubmitRenderRequest`, Task 7 helper); then with `Quality Low`; assert the two center-pixel results differ by more than 4/255 in at least one channel (blur active vs inactive) and that with `Blur = 0` at High the result equals the Low result within 1/255.
- Frame Debugger manual check in step 4.

### Acceptance Criteria

- `Rain Blur Scene Color` pass appears only when a visible lens layer has `Blur > 0` and quality is not Low.
- RT budget table in `docs/performance.md` filled for 720p and 1080p LDR/HDR.
- `RainBlurPassTests` passes.

### Verification

- Frame Debugger toggling `Quality` between High and Low in Play Mode.
- PlayMode test run → `RainBlurPassTests` passed.

## Task 24: Texture import policy, shader variants, and build inclusion audit

### Intent

Ship correctly decoded normal maps and masks with platform-appropriate compression, a minimal shader variant set, serialized shader references only, and no leftover legacy shader/resource dependencies in the package.

### Implementation Steps

1. Platform overrides on package textures (set through a menu `Rain Drop Effect/Migration/Apply Texture Platform Overrides` added to `AssetMover.cs`, so settings are reproducible):
   - Normal maps (`*_normal`): Standalone `BC5` (`TextureImporterFormat.BC5`), Android `ASTC 6x6` (`ASTC_6x6`), iOS `ASTC 6x6`, WebGL `DXT5` (`DXT5` → Unity uses DXT5nm packing for normal maps); `maxTextureSize`: 2048 (Standalone), 1024 (Android/iOS/WebGL).
   - Overlays: Standalone `BC7`, Android/iOS `ASTC 6x6`, WebGL `DXT1` (`DXT1`; no alpha needed), `maxTextureSize` 2048/1024/1024/1024.
   - Coverage masks (`*_coverage`): Standalone `BC4`, Android/iOS `ASTC 6x6` (single channel), WebGL `DXT1`; `maxTextureSize 1024`.
   - `friction_map.tga`: no longer needed at runtime after Task 21; keep as source with `maxTextureSize 512`, `isReadable 0`, no overrides (it is not referenced by any profile; if the reference audit in step 5 confirms zero references, move it to `Editor/BakeSources/` so it is excluded from players).
   - `rain_white*`: keep 32 px.
2. Shader variants: confirm the only `multi_compile` in the package is `_RAIN_BLUR` (local fragment). Create `<pkg>/Editor/BuildValidation/ShaderVariantReport.cs` implementing `IPreprocessShaders` that counts compiled variants for shaders whose name starts with `Hidden/RainDropEffect/` and writes `Logs/shader-variants.json` `{ shader, pass, variantCount }` per build; expected: Lens ≤ 2 variants × platform keywords, Blur 1, Overlay 1.
3. Build inclusion: verify that `RainShaderSet.asset` is referenced from (a) the host renderer feature and (b) every profile-using prefab through `RainEffect.shaderSet`; run the Windows clean-install build (Task 11 script) and open the player log to confirm no `Shader ... not found` and rain renders in the validation scene (add a `Rain` marker capture: the validation scene's `RainEffect` uses `BasicRainProfile` and `ScreenCapture.CaptureScreenshot("rain-validation.png")` after 2 s; the script copies it next to the build json).
4. Remove any `Shader.Find`/`Resources.Load` from package code (`grep -rn "Shader.Find\|Resources.Load" Packages/com.virtualmaestro.raindropeffect/Runtime` → 0 hits).
5. Reference audit: `AssetDatabase.GetDependencies` for every asset under `<pkg>/Runtime/Profiles` and `Samples~` (after Task 27/28) must resolve only to package paths; implement as `<pkg>/Editor/BuildValidation/ReferenceAudit.cs` menu `Rain Drop Effect/Validate Package References` writing `Logs/reference-audit.json` with any path outside `Packages/com.virtualmaestro.raindropeffect/` or `Packages/com.unity.render-pipelines.*`. Run it now (profiles from Task 17 only) and again in Task 29.
6. Precision annotations: review `RainCommon.hlsl`/`RainLens.shader` for `float` on positions/UVs/offsets and `half` on colors/masks; no blanket conversion (D10).

### Required Interfaces and Contracts

- Texture roles and overrides are applied by the reproducible menu, not by hand.
- `Logs/shader-variants.json` and `Logs/reference-audit.json` are release evidence consumed by Task 30.

### Error Handling and Logging

- Reference audit failures are listed in the JSON and logged with `RainLog.Error` per offending path.
- Variant report only logs a one-line summary per build.

### Tests

- `<pkg>/Tests/Editor/TexturePolicyTests.cs`: for each package texture, assert the Android override format is `ASTC_6x6` for normals/overlays/masks and Standalone normals are `BC5`; `NoShaderFindInRuntime` (scan `Runtime/**/*.cs` text for `Shader.Find(` and `Resources.Load(`; expect none).

### Acceptance Criteria

- Overrides present on all shipped textures; `TexturePolicyTests` pass.
- `Logs/shader-variants.json` shows ≤ 2 Lens pass-0 variants per platform.
- Clean-install Windows build renders rain in its validation scene (capture present).
- Reference audit reports zero external references.

### Verification

- EditMode test run → `TexturePolicyTests` passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File tools\clean-install.ps1 -Target Windows -Keep` → succeeded and `rain-validation.png` next to `Windows.json` shows rain.

## Task 25: Quality presets, benchmark scene and measured budgets

### Intent

Turn the research's provisional numbers into measured p50/p95 CPU/GPU costs for Low/Balanced/High on Windows D3D11, WebGL2, and Android, decide the conditional optimizations from evidence, and record incremental memory and pass counts.

### Implementation Steps

1. Create three benchmark profiles under `<pkg>/Runtime/Profiles/`: `Bench_Sparse` (1 static frame + 32 simple drops), `Bench_Trails` (16 flow + 16 friction trails, 64 points, blur 0.5 on flow), `Bench_Combined` (static frame + 256 simple + 32 flow + 32 friction with blur), each with `AutoStart`.
2. Create `Assets/Host/Benchmark.unity` (host): the still-life backdrop plus 20 URP/Lit transparent quads and a rotating cube (transparent content for the copy cost), main camera with `RainEffect` (profile switchable) and `Assets/Host/BenchmarkDriver.cs`:
   - runs a fixed script: warm-up 120 frames; then for each of `{none, Bench_Sparse, Bench_Trails, Bench_Combined} × {Low, Balanced, High}` (none only once): 600 frames sampling `Time.unscaledDeltaTime` and, where supported, `FrameTimingManager` (`FrameTimingManager.CaptureFrameTimings(); GetLatestTimings(1, timings)` → `cpuFrameTime`, `gpuFrameTime`); computes p50/p95; records `Profiler.GetTotalAllocatedMemoryLong()` and `GC.CollectionCount(0)` delta; writes `Logs/benchmark-<platform>-<resolution>.json` (on WebGL writes to `Application.persistentDataPath` and prints to console; copy manually);
   - renders at a fixed internal size using `UniversalRenderPipelineAsset.renderScale` unchanged and Game view 1280×720 (Windows), the device's native size (mobile), and the browser canvas 1280×720 (WebGL);
   - logs one line per scenario with `RainLog.Verbose` after the 600 frames.
3. Run on Windows D3D11 (editor Play Mode is not a valid GPU measurement; use a Development build of the host with the Benchmark scene through `RainBuildValidation.BuildWindows` after temporarily adding the scene to Build Settings), WebGL2 in Chrome (GPU timings unavailable; report frame time deltas only and say so), Android (IL2CPP, GLES3 and Vulkan; switch `scriptingBackend` for Android to IL2CPP in Player Settings) on the available device; iOS if a device and macOS build host are available, else `GAP:`.
4. Capture Render Graph Viewer/Frame Debugger pass counts for `none` vs `Bench_Combined` (native passes with rain − without rain) on Windows and Android.
5. Write `docs/performance.md`: method, hardware/browser versions, per-scenario table (CPU p50/p95, GPU p50/p95 or "n/a", GC delta = 0 expected, passes delta, RT memory), comparison against the research §7 budgets (Low ≤ 2 ms GPU, ≤ 0.5 ms CPU native / ≤ 1 ms Web; Balanced ≤ 3 / ≤ 0.75 / ≤ 1.5; High ≤ 4 / ≤ 1 / ≤ 2), and an `Optimization disposition` table:
   - reduced-resolution distortion field + composite: build only if `Bench_Combined High` GPU exceeds its budget on Android by more than 25%; otherwise record "not built, measured within budget";
   - curve baking: only if CPU p95 of `Bench_Combined` exceeds its budget and profiler shows `AnimationCurve.Evaluate` above 20% of tick time;
   - atlas/instancing/Jobs/Burst: not built unless the same CPU criterion holds after curve baking;
   - half precision: keep D10 policy; note any device where `half` on UV/offset caused artifacts (none expected because offsets are `float`).
   If a conditional optimization is triggered, add it as a new task in `index.md` (open question → task) before Phase 7; do not implement it inside this task.
6. Update `docs/support-matrix.md` with the measured device/browser rows.

### Required Interfaces and Contracts

- Benchmark profiles ship in `Runtime/Profiles` (useful as stress presets) and are excluded from the gallery sample list.
- `BenchmarkDriver` is host-only.

### Error Handling and Logging

- `FrameTimingManager` unsupported → JSON field `gpuFrameTime: null` with `note: "GPU timing unavailable on this platform"`; never derive GPU ms from FPS.
- Any scenario with GC delta > 0 is a failure of NFR-01 and is listed in `docs/performance.md` §Failures with the profiler call stack.

### Tests

- Not a unit test. `DenseTrailBenchmarkTests` (Task 22) remains the automated CPU sanity check.

### Acceptance Criteria

- `docs/performance.md` contains measured tables for Windows and WebGL2 and Android (or `GAP:` with reason), the RT memory table, and the disposition table with a decision per optimization.
- GC delta is 0 for every scenario on Windows.

### Verification

- `ls Logs/benchmark-*.json | wc -l` → ≥ 2 (Windows and one more platform).
- `grep -c "GAP:" docs/performance.md` → number of unmeasured platforms, each with a reason.

## Phase Risks and Mitigations

- Risk: `FrameTimingManager` GPU numbers are noisy on mobile.
  Mitigation: 600-frame windows, p95, repeat twice and keep the worse; use Xcode/Android GPU Inspector where available and say which tool produced each number.
- Risk: measured costs exceed budgets on the available Android device.
  Mitigation: the disposition table triggers the field-composition task by rule; budgets themselves are revised only with the user's device list (open question Q4 in `index.md`).
- Risk: texture overrides change the look (ASTC on normals).
  Mitigation: compare `Rain1` captures between Standalone and Android at the same scene; if visible, use `ASTC 4x4` for normals only and record the size cost.

## Phase Completion Checklist

- Every Task N in this phase satisfies its acceptance criteria.
- Required verification commands pass.
- `index.md` task checkboxes are updated immediately after verified completion.
