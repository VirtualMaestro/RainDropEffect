# Phase 7: Asset Migration, Samples and Legacy Removal

Plan: [index.md](index.md)
Tasks: 26-29
Depends on: Phase 1 / Task 2 (matrix), Phase 2 / Task 8 (coverage masks), Phase 5 / Task 21 (friction field), Phase 6 / Task 23 (final blur semantics)

`<pkg>` means `Packages/com.virtualmaestro.raindropeffect`.

## Objective

Convert every supported legacy preset into profiles and sample prefabs through an explicit, reportable, non-destructive converter; rebuild the demos as opt-in samples without demo-specific resolution/orientation overrides; then remove the legacy runtime, shaders, prefabs, and temporary host tooling so the shipped tree contains no old rendering path.

## Current-Code Evidence

| Path | Symbols / lines | Why it matters |
|------|-----------------|----------------|
| `Assets/RainDropEffect2/Prefabs/*.prefab` (18) | root `Camera` + `RainCameraController` (`Alpha`, `GlobalWind`, `GForceVector`, `ShaderType`, `distance`, `VRMode`); children `StaticRainBehaviour`/`SimpleRainBehaviour`/`FlowRainBehaviour`/`FrictionFlowRainBehaviour` with `Depth` and `Variables`; `BloodRain.prefab` has two child cameras plus a nested FrictionFlowRain prefab instance and `BloodRainCameraController` | Converter input model. Script GUIDs: `fc2899…` camera, `773d7a…` static, `ce59bd…` simple, `d08878…` flow, `447fcd…` friction, `f2ed70…` blood. |
| Prefab GUIDs | `Rain1 8d4d0d3e…`, `Rain2 970c7635…`, `Rain3 f6c21f02…`, `Rain4 fa42c0f4…`, `Rain5 965bc870…`, `Rain6 c309cfab…`, `Frozen d196e5cd…`, `BloodRain 544f43d0…`, `WaterSplashIn c314c9f2…`, `WaterSplashOut a5d23c8b…`, `MobileRain1 26db4a75…`, `MobileRain2 2d27f5ee…`, `MobileRain3 9b211f3a…`, VR ×5 | Report keys; VR prefabs are listed as not converted. |
| Texture GUIDs (unchanged by Task 9) | `rain_drop 6d402e4b…`/`_normal 8c654113…`, `rain_frame 2afc50fc…`/`7b791819…`, `flow d4dda272…`/`7c66e71c…`, `bubble e0b27b58…`/`febb8fda…`, `wash c6b05867…`/`6bfe3f8e…`, `rain_white cea358a0…`/`55c60230…`, `frozenfill 10feca35…`/`6de9a03e…`, `frozenframe 3824f004…`/`6ad3cdd2…`, `bloodframe 4997870a…`/`64926e2d…`, `bloodsplatter 77223c4c…`/`51b21dda…`, `friction_map b78240cf…` | Legacy `Variables.NormalMap/OverlayTexture/FrictionMap` resolve to package paths; converter maps overlay → `<name>_coverage.png` and friction map → `FrictionField_friction_map.asset`. |
| `Assets/RainDropEffect2/Demo/Scripts/DemoScene1.cs`, `DemoScene2.cs`, `AxisRotator.cs`; `Scripts/Misc/BloodRainCameraController.cs`, `FpsDisplay.cs` | `Screen.SetResolution(…512…)`, landscape lock, `targetFrameRate 60`, IMGUI buttons, HP/attack curves, audio sources | Sample logic to rewrite against the new API without host-level overrides. |
| `Assets/RainDropEffect2/Demo/Sounds/*.wav`, `Objects/Fruits/*`, `Objects/Prototyping/*` | demo media (17 Standard-shader materials, FBX, textures) | Move to `Samples~/EffectsGallery/` (materials converted to URP/Lit by the Render Pipeline Converter inside the sample only). |
| `docs/migration-matrix.md` (Task 2) | conversion constants | Converter implements this file. |
| `ProjectSettings/EditorBuildSettings.asset` | only `Demo1.unity` enabled | Replaced by the host smoke scene in Task 29. |

## Files to Change

| Path | Action | Required change |
|------|--------|-----------------|
| `Assets/RainDropEffect2/Editor/Migration/LegacyRainConverter.cs`, `LegacyRainConverterWindow.cs` | create (host; archived then deleted in Task 29) | Converter and its window (Task 26). |
| `Assets/RainDropEffect2/Editor/Migration/ImageCompare.cs` | create (host, temporary) | Baseline vs new capture comparison (Task 27). |
| `Assets/Baseline/BaselineDriver.cs` | modify | Add "new package" capture mode (Task 27). |
| `<pkg>/Runtime/Profiles/*.asset` (Rain1–6, Frozen, BloodFrame, BloodSplatter, BloodFlow, WaterSplashIn, WaterSplashOut, MobileRain1–3) | create | Converted profiles (Task 27). |
| `<pkg>/Samples~/EffectsGallery/**` | create | Gallery scenes, scripts, prefabs, media (Task 28). |
| `<pkg>/Samples~/Basic/**` | modify | Final Basic sample (Task 28). |
| `.ai-factory/baseline/new/*.png`, `docs/migration-report.md` | create | Visual comparison evidence (Task 27). |
| `docs/migration-guide.md` | create | Consumer-facing migration guide (Task 29). |
| `<pkg>/Documentation~/legacy-converter/LegacyRainConverter.cs.txt`, `README.md` | create | Archived opt-in converter (Task 29). |
| `Assets/RainDropEffect2/**`, `Assets/Baseline/**`, `Assets/Spike/**` | delete | Legacy removal (Task 29). |
| `Assets/Host/Smoke.unity`, `ProjectSettings/EditorBuildSettings.asset` | create/modify | Host smoke scene replaces `Demo1` (Task 29). |
| `AGENTS.md` | modify | Project structure map updated to the package layout (Task 29). |

## Task 26: Implement the legacy prefab converter

### Intent

A repeatable Editor tool that reads legacy prefabs (while their scripts still exist) and writes new profiles, camera prefabs, and a per-prefab report, applying only the matrix's mapping. It never edits legacy assets and never runs on import.

### Implementation Steps

1. Create `Assets/RainDropEffect2/Editor/Migration/LegacyRainConverter.cs` (global namespace is acceptable for host editor code; `using RainDropEffect;`). Public entry: `public static ConversionResult Convert(GameObject legacyPrefab, string profileOutputFolder, string prefabOutputFolder, bool dryRun)`.
2. Reading: `PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab))` → traverse; for each `RainCameraController` found (root, or child for BloodRain): collect `Camera` (`fieldOfView`), controller fields, and its child `RainBehaviourBase` components (direct descendants only; nested prefab instances count as children). Unload with `PrefabUtility.UnloadPrefabContents`.
3. Per controller → one `RainProfile` named `<PrefabName>` (or `<PrefabName><CameraSuffix>` for BloodRain: `BloodFrame` for "Frame Blood Camera", `BloodSplatter` for "Splatter Camera", `BloodFlow` for the nested friction instance) with layers built from behaviours:
   - `k = 1 / (controller.distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad))`.
   - Common: `Name = behaviour.gameObject.name`, `Depth = behaviour.Depth`, `Mode = controller.ShaderType == NoDistortion ? Overlay : Lens`, `NormalMap`, `OverlayTexture` from `Variables`, `CoverageMask = LoadCoverage(OverlayTexture)` (`<pkg>/Runtime/Textures/Coverage/<overlayName>_coverage.png`; if missing → report warning and leave null), `OverlayColor`, `Distortion = DistortionValue`, `Relief = ReliefValue`, `Darkness`, `Blur = Blur01(...)` per family, `AutoStart`.
   - `ShaderType == Cheap` → `OverlayColor.a = 0`, `Relief = 0`, `Darkness = 0`, `Blur = 0` (matrix).
   - Static: `FullScreen`, `Size = (SizeX·k, SizeY·k)`, `Offset = (SpawnOffsetX, SpawnOffsetY)`, `FadeTime = fadeTime`, `FadeCurve = FadeinCurve` (copy keys via `new AnimationCurve(curve.keys)`).
   - Emitter (Simple/Flow/Friction): `PlayOnce`, `Duration`, `Delay`, `MaxCount = MaxRainSpawnCount`, `LifetimeRange`, `EmissionRateRange`, `SpawnOffsetY` (raw; runtime applies ×2), curves `AlphaOverLifetime`, `DistortionOverLifetime`, `ReliefOverLifetime`, `BlurOverLifetime` copied.
   - Simple: `AutoRotate`, `SizeMin = (SizeMinX·k, SizeMinY·k)`, `SizeMax = (SizeMaxX·k, SizeMaxY·k)`, `SizeOverLifetime`, `DriftOverLifetime = PosYOverLifetime`, `DriftSpeed = 0.6·k`.
   - Flow: `WidthRange = (SizeMinX·k, SizeMaxX·k)`, `TrailWidth`, `PointSpacing = max(2·k·distance·tan(fov/2)·2/(Resolution·10), 0.008)`, `MaxPoints = 64`, `LateralAmplitude = 0.1·Amplitude·k`, `LateralSmooth = Smooth`, `FluctuationRateRange = (fluctuationRateMin, fluctuationRateMax)`, `InitialVelocity = InitialVelocity·k`, `AccelerationRange = (AccelerationMin·k, AccelerationMax·k)`.
   - Friction: as Flow for width/spacing/points; `Field = FrictionField_<mapName>.asset` (must exist; if the legacy map is not `friction_map`, bake it with `FrictionFieldBaker.Bake` into `<pkg>/Runtime/Data/`), `InitialVelocity = InitialVelocity·0.6·k`, `AccelerationRange = (AccelerationMin·3·k, AccelerationMax·3·k)` (60 Hz-equivalent speed of the legacy per-frame total-displacement step; matrix friction row), `ScanRate 150`, `LateralSamples 5`, `LateralStepFactor 0.375`.
   - Empty curves (no keys) are replaced by `AnimationCurve.Constant(0,1,0)` and reported as `empty curve → constant 0` (legacy `Evaluate` on an empty curve returns 0).
4. Camera prefab per converted controller: new GameObject `<ProfileName> Camera` with `Camera` (clear flags/culling copied from the legacy camera are irrelevant: the new camera is the consumer's; the sample prefab uses default `Camera` settings), `RainEffect { Profile, Intensity = controller.Alpha, Wind = controller.GlobalWind × 60 × k, Gravity = controller.GForceVector, Quality = ShaderType == Expensive ? High : Low, Seed = 0 }`; saved with `PrefabUtility.SaveAsPrefabAsset` at `<prefabOutputFolder>/<ProfileName>.prefab`. For BloodRain, one prefab `BloodRain Camera` with three `RainEffect` components in order Frame, Flow, Splatter (component order = draw order).
5. Report: `Logs/migration/<PrefabName>.md` with sections `Source` (prefab GUID, controller path, fov, distance, k), `Layers` (table legacy field → new field → value), `Unmapped` (fields dropped: `RenderQueue`, `distance`, `VRMode`, `Shiness`, per-controller `Resolution` if unchanged), `Warnings` (missing coverage, missing friction field, empty curves, `MaxCount` above quality cap), `Outputs` (asset paths). `dryRun = true` writes the report only.
6. Window `Assets/RainDropEffect2/Editor/Migration/LegacyRainConverterWindow.cs`: menu `Rain Drop Effect/Migration/Legacy Prefab Converter`; fields: prefab list (drag), output folders (defaults `Packages/com.virtualmaestro.raindropeffect/Runtime/Profiles` and `Packages/com.virtualmaestro.raindropeffect/Samples/EffectsGallery/Prefabs` — note the un-tilded `Samples` folder used during authoring, Task 10 workflow), `Dry run` toggle, `Convert` button; existing output assets are overwritten only after an `EditorUtility.DisplayDialog` confirmation listing the paths.
7. No `AssetPostprocessor`, no automatic conversion on import (D14).

### Required Interfaces and Contracts

- Converter reads legacy types directly (`RainCameraController`, `*Behaviour`, `*Variables`), which is why it lives in the host `Assembly-CSharp-Editor` and is removed with the legacy scripts.
- Conversion constants come only from the matrix; the report makes every value auditable.
- Idempotent: running twice yields byte-identical profiles (asset GUIDs of outputs preserved across re-runs because assets are overwritten in place via `EditorUtility.CopySerialized` into the existing asset when present).

### Error Handling and Logging

- Missing texture/field references → warning in the report and `RainLog.Warn`; conversion continues.
- Unknown behaviour type → `RainLog.Error` and the layer is skipped, reported under `Unmapped`.
- Summary log per prefab: `converted <name>: <n> layers, <w> warnings`.

### Tests

- `<pkg>/Tests/Editor/…` cannot reference host code; put converter tests in `Assets/RainDropEffect2/Editor/Migration/Tests/LegacyRainConverterTests.cs` with its own asmdef `LegacyMigration.Tests.asmdef` (Editor only, references `RainDropEffect.Runtime`, test framework; `autoReferenced false`). Since asmdefs cannot reference `Assembly-CSharp-Editor`, instead keep the tests in `Assembly-CSharp-Editor` by omitting the asmdef and using `#if UNITY_INCLUDE_TESTS` guards; Unity compiles `Assets/**/Editor` tests into the host editor assembly when the Test Framework is present. Tests:
  - `ConvertsRain1IntoThreeLayersInDepthOrder` (dry run against `Rain1.prefab`; expect static, friction, simple ordered by their `Depth`).
  - `UnitConversionUsesK` (Simple `SizeMinX 0.75` → `0.75 × 0.20868 = 0.1565 ± 1e-3`; Rain1 friction `InitialVelocity 12.3` → `12.3 × 0.6 × 0.20868 = 1.540 ± 1e-3`, `AccelerationMin 0.39` → `0.39 × 3 × 0.20868 = 0.2442 ± 1e-3`).
  - `CheapShaderZeroesTint` (MobileRain1 → all layers `OverlayColor.a == 0 && Relief == 0`).
  - `NoDistortionBecomesOverlay` (MobileRain3 → `Mode == Overlay`).
  - `BloodRainProducesThreeProfiles`.
  - `ReportListsDroppedFields` (report contains `RenderQueue`, `distance`, `VRMode`).

### Acceptance Criteria

- Dry run on all 13 non-VR prefabs produces 13 reports with zero `Error` lines.
- Tests pass.

### Verification

- `ls Logs/migration/*.md | wc -l` → `13`.
- `grep -l "ERROR" Logs/migration/*.md` → no output.
- EditMode test run → converter tests passed.

## Task 27: Convert all presets and produce the visual comparison report

### Intent

Produce the shipped profiles and gallery prefabs, capture them with the same protocol as the baseline, and record every accepted difference so the release claims are honest.

### Implementation Steps

0. **Convert the reference scene's backdrop first** (decided 2026-09-10; the conversion used to sit in Task 29,
   which runs after this one, so the new captures would have shown a magenta backdrop and the comparison
   would have been meaningless). Select the Built-in Standard/Legacy materials used by `Assets/Baseline/Baseline.unity`
   and run Window → Rendering → Render Pipeline Converter (Built-in to URP → Material Upgrade) on that selection only.
   Do not touch `Assets/RainDropEffect2/Shaders/**` — those are replaced, not converted (Task 3 step 7). Record in
   `docs/migration-report.md` that the backdrop shading moved from Standard to URP/Lit, so backdrop-only differences
   against `.ai-factory/baseline/` are expected and are not a rain defect.
1. Rename `<pkg>/Samples~` → `Samples` (authoring session). Run the converter (non-dry) on the 13 prefabs with the default output folders. Expected outputs: 15 profiles (`Rain1…Rain6`, `Frozen`, `BloodFrame`, `BloodSplatter`, `BloodFlow`, `WaterSplashIn`, `WaterSplashOut`, `MobileRain1…3`) and 13 camera prefabs.
2. Extend `Assets/Baseline/BaselineDriver.cs` with `public bool NewPackageMode; public GameObject CameraPrefab;`: in new mode it instantiates the converted camera prefab as a child of the scene camera's parent, copies the scene camera's transform, disables the scene's own camera (the converted prefab carries its own camera and `RainEffect`), sets `Seed = 1234` on every `RainEffect`, waits one frame, calls `Play()` on each, and captures at the same times into `.ai-factory/baseline/new/<Name>_<t>s.png`. Because the new camera renders the whole scene itself, the still-life and transparent quad are identical to the baseline scene.
3. Run captures for all 13 (`BaselineCapture` gets a `Capture New Package` menu that iterates the converted prefabs).
4. Create `Assets/RainDropEffect2/Editor/Migration/ImageCompare.cs`: menu `Rain Drop Effect/Migration/Compare Captures`; for each baseline PNG with a matching new PNG, load both (`ImageConversion.LoadImage` into RGBA32 textures), compute mean absolute difference over RGB (0–255 scale) and the percentage of pixels with a difference above 24; write `docs/migration-report.md` table rows `| preset | t | MAD | %diff>24 | baseline | new | note |` with image links relative to the repo (`../.ai-factory/baseline/…`). Copy the six most informative pairs (Rain1 1.5 s, Rain2 1.5 s, Frozen 3.0 s, BloodRain 1.5 s, WaterSplashIn 0.5 s, MobileRain3 1.5 s) into `docs/images/compare/` for the docs site.
5. Review every pair against the matrix tolerances (a)–(f). For each preset write a one-line note: `accepted`, or `tuned: <field> <old>→<new>` (allowed tunings: `DriftSpeed`, `Wind`, `LateralAmplitude`, `PointSpacing`, `Blur`, blur constant in Task 23 step 3), or `defect: <description>` when a family is missing or wrong (blocks the task until fixed in the runtime, never by editing the profile to hide the problem).
6. If a tuning changes a matrix constant (not a per-preset value), update `docs/migration-matrix.md`, update the converter, re-run conversion for all presets, re-capture, re-compare (one iteration recorded in the report's `Tuning history`).
7. Rename `Samples` back to `Samples~`, delete `Samples.meta`. Commit profiles, prefabs, captures, and the report.

### Required Interfaces and Contracts

- Capture protocol (scene, seed, times, resolution) identical to Task 1.
- `docs/migration-report.md` is release evidence (Task 30 links it).

### Error Handling and Logging

- Missing counterpart image → row with `MISSING` and a `RainLog.Warn`.
- Converter errors during the real run are treated exactly as in Task 26.

### Tests

- Not a unit test; the report and its notes are the deliverable. The converter tests (Task 26) guard regressions in constants.

### Acceptance Criteria

- 15 profiles and 13 prefabs exist under the package.
- 39 new captures exist; `docs/migration-report.md` has 39 rows, each with a note; no `defect:` rows remain.
- Every accepted difference maps to a matrix tolerance letter.

### Verification

- `ls Packages/com.virtualmaestro.raindropeffect/Runtime/Profiles/*.asset | wc -l` → `18` (15 presets + 3 `Bench_*`).
- `ls .ai-factory/baseline/new | wc -l` → `39`.
- `grep -c "defect:" docs/migration-report.md` → `0`.

## Task 28: Build the Basic and Effects Gallery samples

### Intent

Replace the five demo scenes and their scripts with two opt-in samples that use only the public API, ship the demo media and HP/audio/FPS logic outside `Runtime`, and drop host-level overrides (`Screen.SetResolution`, orientation lock, `targetFrameRate`).

### Implementation Steps

1. Authoring session (rename `Samples~` → `Samples`). Move demo media with GUIDs using `AssetDatabase.MoveAsset` through a `Rain Drop Effect/Migration/Move Demo Media Into Gallery Sample` menu in `AssetMover.cs`: `Assets/RainDropEffect2/Demo/Sounds/*` → `Samples/EffectsGallery/Sounds/`, `Demo/Objects/Fruits/*` → `Samples/EffectsGallery/Objects/Fruits/`, `Demo/Objects/Prototyping/*` → `Samples/EffectsGallery/Objects/Prototyping/`. Convert the moved Standard materials to URP/Lit with Window → Rendering → Render Pipeline Converter (Built-in to URP → Material Upgrade) restricted to that folder (select the materials first; run "Convert Selected" if available, otherwise convert all and verify only these 21 materials changed).
2. Scripts in `Samples/EffectsGallery/Scripts/` (namespace `RainDropEffect.Samples`, no asmdef so the sample compiles into the consumer's assembly):
   - `GallerySample.cs`: serialized `RainEffect[] Presets` (one per camera prefab instance, all on separate child cameras that are disabled except the active one; simpler: one camera with a single `RainEffect` and a `RainProfile[] Profiles` array + `RainQuality[] Qualities` — choose this), IMGUI buttons (`GUI.Button`) per profile that set `effect.Profile`, `effect.Quality`, call `Rebuild()` then `Play()`; buttons `Stop`, `Clear`; a quality dropdown (three buttons). No resolution/orientation/frame-rate calls.
   - `BloodRainSample.cs`: `RainEffect Frame, Flow, Splatter; int HP = 100; float FrameEffectInterval = 1f, Smooth = 2f; AnimationCurve HpHigh, HpMid, HpLow;` `Attack(int damage)` → `HP -= damage; Splatter.Clear(); Splatter.Play();`; `ResetHp()`; `Update()` reproduces the legacy alpha lerp/pulse on `Frame.Intensity` (copy formulas from `BloodRainCameraController.Update` L43-79 with `Frame.Play()` each frame replaced by `if (!Frame.IsEmitting) Frame.Play()`).
   - `SplashSample.cs`: `RainEffect SplashIn, SplashOut, Frozen; AudioSource SplashInAudio, SplashOutAudio, WindAudio;` methods `PlaySplashIn()`, `PlaySplashOut()`, `SetFrozen(float)` (`Frozen.Intensity = v; if (v > 0 && !Frozen.IsPlaying) Frozen.Play(); if (v == 0) Frozen.Stop();`), `WindAudio.volume = v`.
   - `FpsDisplaySample.cs`: copy of `FpsDisplay.cs` logic (`OnGUI` label).
   - `AxisRotatorSample.cs`: copy of `AxisRotator.cs`.
   - `GalleryMenuSample.cs`: IMGUI for the blood/splash scene (buttons `Blood`, `Hit (30)`, `Splash In`, `Splash Out`, `Reset`, frozen slider) calling the two samples above, replacing `DemoScene2.OnGUI`.
3. Scenes:
   - `Samples/EffectsGallery/Gallery.unity` (from `Demo1.unity` content: still-life, rotating props with `AxisRotatorSample`, main camera with `RainEffect`, `GallerySample` listing the 9 general profiles plus the 3 mobile ones with `Quality Low`, `FpsDisplaySample`).
   - `Samples/EffectsGallery/BloodAndSplash.unity` (from `Demo2.unity`: main camera with three `RainEffect` components (BloodFrame, BloodFlow, BloodSplatter) plus three more for `WaterSplashIn`, `WaterSplashOut`, `Frozen` (six components on one camera, all `AutoStart` handled by profiles: blood/splash profiles converted with `AutoStart false` where the legacy prefab relied on manual `Play`; set `AutoStart = false` on the six sample profiles' layers if the converter carried `true`, and record it in the migration report), audio sources with the moved clips, `BloodRainSample`, `SplashSample`, `GalleryMenuSample`).
   - `Samples/Basic/Basic.unity`: keep from Task 17; add one IMGUI `Play/Stop` button script `BasicSample.cs`.
4. Sample prefabs from Task 27 stay in `Samples/EffectsGallery/Prefabs/` for users who prefer prefabs.
5. `Samples/EffectsGallery/README.md`: what each scene shows, which profile each button uses, note that Mobile presets are the same renderer at `Quality Low`.
6. Import both samples into the host via Package Manager and open each scene: no missing scripts/references, all buttons work. Remove the imported copies. Rename back to `Samples~`.

### Required Interfaces and Contracts

- Samples reference only `RainDropEffect` public API and their own assets.
- No `Screen.SetResolution`, `Screen.orientation`, or `Application.targetFrameRate` in samples (`grep` check in Task 29).

### Error Handling and Logging

- Sample scripts guard null `RainEffect` references with `Debug.LogWarning` once in `Awake` and disable themselves.

### Tests

- Not unit-tested (samples). Task 10's `SamplesLayoutTests` continues to check the folders; add `GallerySampleHasNoHostOverrides` test in `<pkg>/Tests/Editor/SamplesLayoutTests.cs` that scans `Samples~/**/*.cs` text for `SetResolution(`, `Screen.orientation`, `targetFrameRate` and expects zero hits.

### Acceptance Criteria

- Both samples import cleanly and run: Gallery switches all 12 presets; BloodAndSplash plays blood on hit, splash in/out with audio, frozen slider fades the frozen layers.
- Legacy `Demo/` folder now contains only the five `.unity` scenes and `Scripts/` (media moved), ready for deletion in Task 29.

### Verification

- `ls Packages/com.virtualmaestro.raindropeffect/Samples~/EffectsGallery` → `Gallery.unity`, `BloodAndSplash.unity`, `Scripts`, `Prefabs`, `Sounds`, `Objects`, `README.md`.
- EditMode test run → `SamplesLayoutTests` passed (including the new override check).

## Task 29: Remove the legacy runtime and host scaffolding

### Intent

Complete REQ-06/REQ-01: the repository ships no old renderer, shaders, prefabs, or demos, the converter is archived as an opt-in tool, the host has a smoke scene, and the project map reflects the package layout.

### Implementation Steps

1. Archive the converter: copy `Assets/RainDropEffect2/Editor/Migration/LegacyRainConverter.cs` and `LegacyRainConverterWindow.cs` to `<pkg>/Documentation~/legacy-converter/LegacyRainConverter.cs.txt` and `LegacyRainConverterWindow.cs.txt`; write `<pkg>/Documentation~/legacy-converter/README.md`: prerequisites (a project containing both the original Rain Drop Effect 2 asset and this package), install steps (copy the two files into any `Editor/` folder, remove the `.txt` suffix), usage (menu path, dry run, outputs), and the statement that the runtime package contains no legacy types.
2. Write `docs/migration-guide.md` for consumers: API mapping table (`RainCameraController.Play/Stop/StopImmidiate/Refresh/Alpha/GlobalWind/GForceVector/IsPlaying` → `RainEffect.Play/Stop/Clear/Rebuild/Intensity/Wind/Gravity/IsPlaying`; behaviours → profile layers; `ShaderType` → `Quality` + layer `Mode`; prefabs → profiles + camera component), breaking changes (no VR prefabs, no Built-in RP, no `Shader.Find` names, new units), step-by-step conversion using the archived converter, and the tolerances list from the matrix.
3. Delete with `git rm -r` (Unity-aware deletion is not required for whole folders whose contents are no longer referenced; the `.meta` files go with them): `Assets/RainDropEffect2/` (Scripts, Shaders, Editor, Prefabs, Demo remnants), `Assets/Baseline/`, `Assets/Spike/`. Keep `.ai-factory/baseline/` (evidence) and the `legacy-baseline` tag.
4. Host smoke scene: create `Assets/Host/Smoke.unity` = copy of the Basic sample scene content (camera + `RainEffect` + `BasicRainProfile` + backdrop); set it as the only enabled scene in Build Settings (replacing `Demo1.unity`). `Assets/Host/Benchmark.unity` (Task 25) remains, disabled in Build Settings.
5. Run the reference audit (Task 24 menu) and a missing-script scan: add `Rain Drop Effect/Validate Missing Scripts` to `ReferenceAudit.cs` that iterates all prefabs/scenes under `Assets/` and `<pkg>` (including `Samples~` by temporary rename or by reading `Samples~` through `AssetDatabase` after import into `Assets/Samples/`), counts `GameObjectUtility.GetMonoBehavioursWithMissingScriptCount`, and writes `Logs/missing-scripts.json`. Expect zero.
6. Text checks (see verification) for old terms in executable runtime code: `GrabPass`, `_GrabTexture`, `_BackgroundTexture`, `RainDrop/Internal`, `UnityCG`, `CGPROGRAM`, `RainCameraController`, `RainBehaviourBase`, `RainDrawer`, `DropTrail`, `Shader.Find` must not appear under `<pkg>/Runtime` or `<pkg>/Samples~`.
7. Update `AGENTS.md` §Project Structure and §Key Entry Points to the package layout (`Packages/com.virtualmaestro.raindropeffect/{Runtime,Editor,Tests,Samples~,Documentation~}`, `Assets/Host/`, `Assets/Settings/`) and the key files (`Runtime/RainEffect.cs`, `Runtime/Settings/RainProfile.cs`, `Runtime/Rendering/RainRendererFeature.cs`, `Runtime/Shaders/RainLens.shader`). Leave `.ai-factory/DESCRIPTION.md`, `ARCHITECTURE.md`, and `rules/base.md` for their owner workflows (note in `index.md` Definition of Done).
8. Run the full EditMode + PlayMode suites and the Windows clean-install build.

### Required Interfaces and Contracts

- After this task, `Assets/` contains only `Host/`, `Settings/`, `Plugins/` (NuGet tooling), `Packages/` (NuGet), `NuGet.config`, `packages.config`.
- The package's `Runtime` has zero references to legacy identifiers.

### Error Handling and Logging

- Missing-script or reference-audit failures block the task; fix references in samples/profiles rather than deleting the offending asset.

### Tests

- Existing suites must stay green; add `<pkg>/Tests/Editor/LegacyTermsTests.cs`: scan `Runtime/**/*.cs`, `Runtime/**/*.shader`, `Runtime/**/*.hlsl` for the step 6 terms; expect zero hits.

### Acceptance Criteria

- `Assets/RainDropEffect2`, `Assets/Baseline`, `Assets/Spike` no longer exist.
- `Logs/missing-scripts.json` and `Logs/reference-audit.json` report zero issues.
- `docs/migration-guide.md` and the archived converter exist.
- All tests pass; clean-install build succeeds.
- `AGENTS.md` structure map updated.

### Verification

- `ls Assets` → `Host  NuGet.config  Packages  Plugins  Settings  packages.config` (plus `.meta` files).
- `grep -rn "GrabPass\|_GrabTexture\|_BackgroundTexture\|RainDrop/Internal\|UnityCG\|CGPROGRAM\|RainCameraController\|RainBehaviourBase\|RainDrawer\|DropTrail\|Shader.Find" Packages/com.virtualmaestro.raindropeffect/Runtime Packages/com.virtualmaestro.raindropeffect/Samples~` → no output.
- `grep -c "0" Logs/missing-scripts.json` → count present; `grep -c '"external": \[\]' Logs/reference-audit.json` → `1`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File tools\clean-install.ps1 -Target Windows` → exit 0.

## Phase Risks and Mitigations

- Risk: converter and legacy scripts must coexist with the new package; type name collisions (`RainDropEffect` namespace already used by legacy `RainDrawer`).
  Mitigation: the package uses the same `RainDropEffect` namespace but different type names; the only legacy type in that namespace is `RainDrawer`, which the package does not declare.
- Risk: visual comparison reveals a family defect late.
  Mitigation: defects block Task 27 and are fixed in Phase 4/5 code; tuning of constants is bounded to one recorded iteration.
- Risk: deleting `Assets/RainDropEffect2` removes an asset still referenced by a sample.
  Mitigation: reference audit and missing-script scan run before deletion (Task 24 step 5, Task 29 step 5) and after.

## Phase Completion Checklist

- Every Task N in this phase satisfies its acceptance criteria.
- Required verification commands pass.
- `index.md` task checkboxes are updated immediately after verified completion.
