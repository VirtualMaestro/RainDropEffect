# Phase 3: Package Extraction and Clean-Install Gate

Plan: [index.md](index.md)
Tasks: 9-11
Depends on: Phase 2 / Task 4 (skeleton), Task 8 (coverage masks exist before textures move)

`<pkg>` means `Packages/com.virtualmaestro.raindropeffect`.

## Objective

Move every retained runtime asset into the package with its GUID intact, separate samples from runtime, and prove that the package installs and builds in an empty URP project without samples, demos, or host tooling. This gate is deliberately early (before the simulation rewrite) so boundary mistakes are caught while the code is small.

## Current-Code Evidence

| Path | Symbols / lines | Why it matters |
|------|-----------------|----------------|
| `Assets/RainDropEffect2/Textures/**` | 21 `.tga` with `.meta` GUIDs (table in Phase 7 evidence) | Moved with GUIDs via `AssetDatabase.MoveAsset`; legacy prefabs keep resolving until Task 29 deletes them. |
| `Assets/RainDropEffect2/Demo/**` | 5 scenes, `DemoScene1.cs`, `DemoScene2.cs`, `AxisRotator.cs`, `Sounds/*.wav`, `Objects/Fruits/*`, `Objects/Prototyping/*` | Stay in the host until Task 28 rebuilds samples from them; never enter `Runtime/`. |
| `Assets/RainDropEffect2/Scripts/Misc/BloodRainCameraController.cs`, `FpsDisplay.cs` | gameplay/GUI in library folder | Excluded from the package; sample equivalents written in Task 28. |
| `Assets/Plugins/NuGet/*.dll` (about 40), `Assets/packages.config`, `Assets/NuGet.config` | host MCP tooling | Must not appear in the package or in the clean-install project. |
| `Packages/manifest.json` | `com.ivanmurzak.unity.mcp`, `nugetforunity` | Host-only; the clean-install manifest excludes them. |
| `<pkg>/package.json` `samples` | `Samples~/Basic`, `Samples~/EffectsGallery` | Sample folders must match these paths exactly (U07). |

## Files to Change

| Path | Action | Required change |
|------|--------|-----------------|
| `Assets/RainDropEffect2/Textures/**` → `<pkg>/Runtime/Textures/**` | move (Unity-aware) | Same sub-folders (`Rain/`, `Frozen/`, `Blood/`), same GUIDs. |
| `<pkg>/Editor/Baking/CoverageMaskBaker.cs` | modify | Source path list updated to package paths. |
| `<pkg>/Samples~/Basic/Basic.unity`, `<pkg>/Samples~/Basic/BasicRainProfile.asset` | create | Minimal sample (Task 10). |
| `<pkg>/Samples~/EffectsGallery/README.md` | create | Placeholder describing what Task 28 adds. |
| `<pkg>/Documentation~/index.md` | create | Install/usage skeleton. |
| `tools/clean-install.ps1` | create | Creates a temporary consumer project and runs a batch build (Task 11). |
| `<pkg>/Editor/BuildValidation/RainBuildValidation.cs` | create | `-executeMethod` entry points for batch builds (Task 11). |
| `docs/support-matrix.md` | modify | Append clean-install result row. |

## Task 9: Move retained textures into the package with GUIDs preserved

### Intent

Establish `Runtime/Textures` as the only texture home so profiles created in Phase 7 reference package assets, while legacy prefabs continue to resolve during the staged migration (GUIDs unchanged).

### Implementation Steps

1. Create `<pkg>/Editor/Migration/AssetMover.cs` (`RainDropEffect.Editor`) with menu `Rain Drop Effect/Migration/Move Legacy Textures Into Package` that, inside `AssetDatabase.StartAssetEditing()/StopAssetEditing()`:
   - ensures folders `<pkg>/Runtime/Textures/Rain`, `/Frozen`, `/Blood` exist (`AssetDatabase.CreateFolder` when `IsValidFolder` is false);
   - for each texture under `Assets/RainDropEffect2/Textures/<Sub>/<file>.tga` (enumerate with `AssetDatabase.FindAssets("t:Texture2D", new[]{"Assets/RainDropEffect2/Textures"})`), calls `AssetDatabase.MoveAsset(src, "<pkg>/Runtime/Textures/<Sub>/<file>.tga")` and logs the returned string when non-empty (error);
   - after moving, verifies `AssetDatabase.AssetPathToGUID(dst)` equals the GUID read before the move; any mismatch is an error (see logging).
2. Run the menu. Confirm `Assets/RainDropEffect2/Textures/` is empty except folder `.meta` files; delete the empty folders through the Project window (Unity removes their `.meta`).
3. Update `CoverageMaskBaker` source list to `<pkg>/Runtime/Textures/...` paths and re-run `Bake Coverage Masks`; outputs overwrite the same `Coverage/*_coverage.png` files (unchanged GUIDs because the asset paths are identical).
4. Open `Assets/RainDropEffect2/Prefabs/Rain1.prefab` in the inspector: `StaticRainBehaviour.Variables.NormalMap` still shows `rain_frame_normal` (GUID reference intact). Repeat for `Frozen.prefab` and `BloodRain.prefab`.
5. Set importer overrides now (they are role-based, not platform-guessed; Task 24 adds per-platform compression):
   - all `*_normal.tga`: `textureType = NormalMap`, `mipmapEnabled = true`, `wrapMode = Clamp`, `filterMode = Trilinear`, `maxTextureSize = 2048`;
   - all overlay `.tga` (`rain_drop`, `rain_frame`, `flow`, `bubble`, `wash`, `rain_white`, `frozenfill`, `frozenframe`, `bloodframe`, `bloodsplatter`): `sRGBTexture = true` (Gamma project, values used as authored), `alphaSource = None`, `wrapMode = Clamp`, `mipmapEnabled = true`;
   - `friction_map.tga`: `isReadable = true` retained until Task 21 bakes `FrictionField`; then Task 21 sets `isReadable = false`;
   - `rain_white.tga` and `rain_white_normal.tga`: `maxTextureSize = 32` retained (legacy placeholder).
   Apply through a second menu `Rain Drop Effect/Migration/Apply Texture Roles` in `AssetMover.cs` that sets the fields via `TextureImporter` and `SaveAndReimport()`.
6. Commit the move as one commit (paths + `.meta` renames appear as renames in `git status`).

### Required Interfaces and Contracts

- Every moved texture keeps its GUID (list in Phase 7 evidence). Task 26 relies on `AssetDatabase.GUIDToAssetPath` resolving legacy references to the new package paths.
- `Runtime/Textures` contains only textures used by shipped profiles; Demo textures never move here.

### Error Handling and Logging

- `MoveAsset` error string → `RainLog.Error($"move failed {src} -> {dst}: {error}")`; the run continues and the summary lists failures.
- GUID mismatch after move → `RainLog.Error("GUID changed for " + dst)`; this must not happen with `MoveAsset` and blocks the task if it does.
- Summary: `RainLog.Verbose($"moved {n} textures, {failures} failures")`.

### Tests

- `<pkg>/Tests/Editor/TextureLayoutTests.cs`: `AllPackageTexturesHaveExpectedGuids`: dictionary of the 21 expected `path → GUID` pairs (from the Phase 7 evidence table); assert `AssetDatabase.AssetPathToGUID(path) == guid` for each. `NormalMapsAreImportedAsNormalMaps`: for every `*_normal.tga` in `<pkg>/Runtime/Textures`, `TextureImporter.textureType == NormalMap`.

### Acceptance Criteria

- 21 textures under `<pkg>/Runtime/Textures/{Rain,Frozen,Blood}` with unchanged GUIDs.
- `Assets/RainDropEffect2/Textures` no longer exists.
- Legacy prefabs show no missing texture references.
- Tests pass.

### Verification

- `git status --short | grep -c "^R"` → ≥ 42 (21 textures + 21 `.meta` renames) at the time of the commit.
- EditMode test run → `TextureLayoutTests` passed.

## Task 10: Create the Basic sample and documentation skeleton

### Intent

Provide the minimal consumer experience the clean-install gate imports and the starting point for the gallery (Task 28), and validate the `Samples~` convention (U07) early.

### Implementation Steps

1. Create `<pkg>/Samples~/Basic/Basic.unity`: a scene with a directional light, a plane with a checker material (URP/Lit, use a 4×4 generated checker texture stored as `Samples~/Basic/Checker.png`), one URP/Lit transparent cube (alpha 0.5), and `Main Camera` with `RainEffect` (shell) and profile `BasicRainProfile.asset` (created in Task 12; until then leave the profile field empty and add a `TODO` note in `Samples~/Basic/README.md`, which Task 17 removes).
2. Create `<pkg>/Samples~/Basic/README.md`: how to add the renderer feature, add `RainEffect`, assign a profile, call `Play()`.
3. Create `<pkg>/Samples~/EffectsGallery/README.md` describing the planned content (presets, blood HP demo, splash and frozen controls) and that Task 28 fills it.
4. Create `<pkg>/Documentation~/index.md` with headings `Installation`, `Renderer feature setup`, `Usage`, `Quality`, `Migration from 1.x`, `Support matrix` (short text now; Task 32 finalizes) and `<pkg>/Documentation~/legacy-converter/README.md` placeholder for Task 29.
5. Because `Samples~` is hidden from the Asset Database, author sample assets by temporarily renaming the folder to `Samples` (without tilde), editing in the editor, then renaming back to `Samples~` and deleting the generated `Samples.meta`. Document this workflow in `Documentation~/contributing.md`.
6. Import the sample through Package Manager → Rain Drop Effect URP → Samples → Basic Setup → Import; confirm it lands in `Assets/Samples/Rain Drop Effect URP/2.0.0-pre.1/Basic Setup/` and the scene opens without missing references. Remove the imported copy afterwards (it is a host artifact).

### Required Interfaces and Contracts

- Sample scenes reference only package runtime assets and their own sample assets; never `Assets/RainDropEffect2/**` or host files.
- Sample folder names must equal `package.json` `samples[].path`.

### Error Handling and Logging

- Not applicable (assets and docs). Missing references after import are a failure of this task.

### Tests

- `<pkg>/Tests/Editor/SamplesLayoutTests.cs`: read `package.json`, for each `samples[].path` assert `Directory.Exists(Path.Combine(packageRoot, path))` and that the folder contains at least one `.unity` or `README.md`.

### Acceptance Criteria

- Sample import succeeds and the imported scene opens with zero missing references (Console has no `Missing` warnings).
- `SamplesLayoutTests` passes.

### Verification

- `ls Packages/com.virtualmaestro.raindropeffect/Samples~/Basic` → shows `Basic.unity`, `README.md`.
- EditMode test run → `SamplesLayoutTests` passed.

## Task 11: Clean-install and no-sample player build gate

### Intent

Prove REQ-01 now: the package installs into an empty URP consumer and builds a player without samples, demos, MCP or NuGet, and the Runtime assembly cannot reference Editor/Samples/host code.

### Implementation Steps

1. Create `<pkg>/Editor/BuildValidation/RainBuildValidation.cs` (`RainDropEffect.Editor`) with static methods:
   - `public static void BuildWindows()` → `BuildPlayer` with scenes = `[]` when no scene is enabled (URP requires at least one scene; the script creates an empty scene with a camera + `RainEffect` at `Assets/RainValidation/Validation.unity` if `EditorBuildSettings.scenes` is empty), `target = StandaloneWindows64`, output `Builds/Validation/Windows/RainValidation.exe`, options `None`; exits with `EditorApplication.Exit(1)` when `BuildReport.summary.result != Succeeded`.
   - `public static void BuildWebGL()` same with `target = WebGL`, output `Builds/Validation/WebGL`.
   - `public static void BuildAndroid()` same with `target = Android`, `EditorUserBuildSettings.buildAppBundle = false`, output `Builds/Validation/Android/RainValidation.apk`.
   Each logs `[RainDropEffect] build <target> result=<result> size=<totalSize>` and writes `Builds/Validation/<target>.json` with `result`, `totalSize`, `totalTime`, `errors` count.
2. Create `tools/clean-install.ps1` (PowerShell 5.1 compatible, no `&&`):
   - parameters `-Editor` (default `C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe`), `-Target` (`Windows|WebGL|Android`, default `Windows`), `-Keep` switch;
   - creates `$env:TEMP\RainCleanInstall\<timestamp>\` with `Assets/`, `ProjectSettings/ProjectVersion.txt` (`m_EditorVersion: 6000.3.23f1`), and `Packages/manifest.json` = `{ "dependencies": { "com.virtualmaestro.raindropeffect": "file:<absolute path to repo>/Packages/com.virtualmaestro.raindropeffect", "com.unity.render-pipelines.universal": "<locked version>" } }` (no MCP, no NuGet, no test framework);
   - copies `Assets/Settings/Rain-URP.asset`, `Rain-URP-Renderer.asset` and their `.meta` into the temp project `Assets/Settings/` and writes a minimal `ProjectSettings/GraphicsSettings.asset`? Not portable by hand; instead the validation script step 1 assigns the pipeline: add `public static void ConfigureUrp()` in `RainBuildValidation` that loads `Assets/Settings/Rain-URP.asset` and sets `GraphicsSettings.defaultRenderPipeline` and each `QualitySettings.SetRenderPipelineAssetAt(i, asset)`, then `AssetDatabase.SaveAssets()`;
   - runs `& $Editor -batchmode -nographics -quit -projectPath $proj -executeMethod RainDropEffect.Editor.RainBuildValidation.ConfigureUrp -logFile "$proj\Logs\configure.log"`;
   - runs `& $Editor -batchmode -quit -projectPath $proj -executeMethod RainDropEffect.Editor.RainBuildValidation.Build$Target -logFile "$proj\Logs\build.log"`;
   - prints the last 30 lines of `build.log` on failure, prints `Builds/Validation/$Target.json` on success, and removes the temp project unless `-Keep`.
   Note: the `file:` path to an embedded package in another project is valid for UPM local packages; the temp project must not be inside the repository.
3. Run `powershell -NoProfile -ExecutionPolicy Bypass -File tools\clean-install.ps1 -Target Windows`. Fix any compile error that appears only in the consumer (typical: Runtime code referencing `UnityEditor` without `#if UNITY_EDITOR`, or a test/sample type referenced from Runtime).
4. Run with `-Target WebGL` if the Web build module is installed; record the result or a `GAP:`.
5. Inspect the Windows build folder: `RainValidation_Data/Managed/` must contain `RainDropEffect.Runtime.dll` and must not contain `RainDropEffect.Editor.dll`, `McpPlugin.dll`, or any `Microsoft.*` NuGet DLL.
6. Append to `docs/support-matrix.md` §Clean install: date, editor, URP version, target, result, player size.
7. Add to the repository `.gitignore` (currently lacks these): `/Logs/`, `/Builds/`, `/Assets/Samples/` (imported sample copies from Package Manager). Evidence that must be kept is copied into `docs/evidence/` by Task 30; benchmark and test JSON/XML under `Logs/` are transient.

### Required Interfaces and Contracts

- `RainBuildValidation` methods are also reused by Task 25 (benchmark builds) and Task 32 (release builds); keep them parameterless and driven by `EditorUserBuildSettings`.
- The consumer manifest is the reference for the package's declared dependencies: if the build fails because a dependency is missing, add it to `package.json` `dependencies`, not to the consumer manifest.

### Error Handling and Logging

- Build failure → exit code 1 from Unity, script prints log tail and exits 1.
- Missing editor path or target module → script prints `GAP: <reason>` and exits 2 (distinct from build failure).

### Tests

- Not a Unity test; the script is the test. Add `tools/README.md` with the exact invocation and expected output.

### Acceptance Criteria

- Windows clean-install build succeeds with zero errors and the managed folder contains only `RainDropEffect.Runtime.dll` from this package.
- WebGL result or `GAP:` recorded.
- `docs/support-matrix.md` has the clean-install row.

### Verification

- `powershell -NoProfile -ExecutionPolicy Bypass -File tools\clean-install.ps1 -Target Windows -Keep` → exit code 0 and `Builds/Validation/Windows.json` shows `"result":"Succeeded"`.
- `ls <temp>/Builds/Validation/Windows/RainValidation_Data/Managed | grep -i -c "RainDropEffect.Editor\|McpPlugin\|Microsoft"` → `0`.

## Phase Risks and Mitigations

- Risk: moving textures breaks legacy prefab references if `MoveAsset` is bypassed by a file-system move.
  Mitigation: Task 9 uses `AssetDatabase.MoveAsset` only and verifies GUIDs; the test pins all 21 GUIDs.
- Risk: `Samples~` editing friction leads to samples authored under `Assets/` and never moved.
  Mitigation: documented rename workflow (Task 10 step 5) and the layout test.
- Risk: the consumer project cannot resolve `file:` embedded package path.
  Mitigation: use an absolute path with forward slashes; if UPM refuses, fall back to `npm pack`-style tarball via `UnityEditor.PackageManager.Client.Pack` and `file:<tarball>.tgz` (Task 32 uses the tarball route anyway).

## Phase Completion Checklist

- Every Task N in this phase satisfies its acceptance criteria.
- Required verification commands pass.
- `index.md` task checkboxes are updated immediately after verified completion.
