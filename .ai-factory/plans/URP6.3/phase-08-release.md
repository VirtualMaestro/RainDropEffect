# Phase 8: Release Qualification and Version Policy

Plan: [index.md](index.md)
Tasks: 30-32
Depends on: Phase 7 / Task 29 (legacy removed), Phase 6 / Task 25 (budgets)

`<pkg>` means `Packages/com.virtualmaestro.raindropeffect`.

## Objective

Execute the full evidence matrix from the research (installation, rendering, URP configuration, camera policy, state, ownership, visual, performance, hardware), validate the minimum editor plus one newer editor/URP combination, package and re-install from an exported tarball, and publish version 2.0.0 with documentation whose claims match actual runs. Unavailable hardware is recorded as a gap, never as a pass.

## Current-Code Evidence

| Path | Symbols / lines | Why it matters |
|------|-----------------|----------------|
| `tools/clean-install.ps1`, `<pkg>/Editor/BuildValidation/RainBuildValidation.cs` (Task 11) | consumer project + batch builds | Installation and build evidence. |
| `<pkg>/Tests/**` (Tasks 4–29) | EditMode/PlayMode suites | State/ownership/geometry evidence. |
| `Assets/Host/Benchmark.unity`, `BenchmarkDriver.cs`, `docs/performance.md` (Task 25) | measured budgets | Performance evidence and sustained-run driver. |
| `docs/migration-report.md` (Task 27), `docs/renderer-decision.md` (Task 7), `docs/support-matrix.md` (Tasks 3, 11, 25) | evidence documents | Linked from the release report. |
| `C:\Program Files\Unity\Hub\Editor\6000.5.10f1` | newer installed editor | The "explicitly tested newer version" (D3). |
| `<pkg>/package.json` `version: 2.0.0-pre.1` | pre-release version | Bumped in Task 32. |

## Files to Change

| Path | Action | Required change |
|------|--------|-----------------|
| `docs/release-report.md` | create | Matrix results with links to evidence (Tasks 30, 31). |
| `Assets/Host/BenchmarkDriver.cs` | modify | Sustained-run mode (Task 31). |
| `<pkg>/package.json`, `CHANGELOG.md`, `README.md`, `Documentation~/*.md` | modify | Release metadata and docs (Task 32). |
| `docs/support-matrix.md` | modify | Final verified combinations (Task 32). |
| `tools/pack-and-install.ps1` | create | Tarball export and re-install (Task 32). |

## Task 30: Execute the installation, rendering, camera, state and ownership matrix

### Intent

Turn the research §7 "Evidence required for release" table into executed checks with recorded results.

### Implementation Steps

1. Create `docs/release-report.md` with the sections below; each row has `Case`, `Method`, `Result (PASS/FAIL/GAP)`, `Evidence link`.
2. Installation: (a) `tools/clean-install.ps1 -Target Windows` → PASS requires build success and `rain-validation.png` shows rain; (b) same with `-Target WebGL`; (c) in the clean-install project (`-Keep`), import `Basic` then `EffectsGallery` samples through Package Manager and open both scenes: zero console errors; (d) `Logs/reference-audit.json` empty external list; (e) player `Managed/` folder contains no editor/tool DLLs.
3. Rendering (host `Gallery.unity`, manual with Frame Debugger and screenshots into `docs/images/release/`): opaque + transparent objects refracted; overlapping drops (Rain6) do not erase earlier drops outside masks; Frozen + BloodFrame combined (enable both profiles on two effects) blends; portrait (Game view 720×1280) and landscape; drop at the screen edge shows clamped, not wrapped, refraction; RenderTexture output camera; camera alpha preserved (render to an RGBA RenderTexture with `Clear Flags = Solid Color alpha 0`, read back a rain pixel, assert alpha unchanged).
4. URP configuration (change host settings, run `Gallery.unity`, screenshot): perspective/orthographic; HDR on/off; MSAA off/2x/4x; render scale 0.5/1.0; dynamic resolution enabled on the camera with `ScalableBufferManager.ResizeBuffers(0.7f, 0.7f)` from a temporary host script; post-processing on/off (with Intermediate Texture Auto vs Always per Task 7 M5); Universal Renderer Forward (baseline), Forward+, and Deferred (rain is a post-transparent pass and must render identically; record any difference).
5. Camera policy: one camera; two independent cameras sharing `Rain1` with different seeds rendering to two RenderTextures shown side by side; Base + Overlay stack per Task 7 M7 result; Scene View opt-in on/off; Preview camera (open a material preview) and a reflection probe realtime capture show no rain; tick-once-per-frame counter with three cameras (host script asserting `RainEffect.TickForTest` count equals frame count).
6. State: run `EmitterStateTests`, `SimpleLayerRuntimeTests`, `FlowLayerRuntimeTests`, `FrictionLayerRuntimeTests` (Tasks 18, 22) and add manual checks: `Delay 2 s` then `Clear()` at 1 s → nothing ever spawns; restart during delay; `Intensity 0` → no passes; `Time.timeScale = 0` freezes drops; `timeScale 2` doubles speed; pause/resume the editor (Play Mode pause) leaves state intact; simulate tab resume by setting `Time.maximumDeltaTime` high and blocking the main thread 3 s with `Thread.Sleep` in a host script → next frame advances by at most 0.1 s (log the clamped dt).
7. Ownership: `RainEffectLifecycleTests` (100 cycles), plus manual: scene reload 10× (`SceneManager.LoadScene` of `Gallery.unity`) with `Resources.FindObjectsOfTypeAll<Mesh>()` count logged before/after; component disable/destroy; renderer-feature removal and re-add at runtime through the inspector; Enter Play Mode Options with domain reload disabled (10 Play/Stop cycles, no errors, registry consistent); resize the Game view five times and rotate portrait/landscape; no growth in mesh/material/texture counts (`Profiler.GetTotalAllocatedMemoryLong` within 5% after warm-up).
8. Attach automated results: copy `Logs/editmode.xml`, `Logs/playmode.xml` to `docs/evidence/` and link them.

### Required Interfaces and Contracts

- Every row cites a file (screenshot, XML, JSON) or a test name; rows without evidence are `GAP`.

### Error Handling and Logging

- Any FAIL row blocks Task 32; the fix is made in the owning phase's code and the row re-run.

### Tests

- Existing suites; no new unit tests. Manual host scripts used for tick counting/dt clamping live in `Assets/Host/ReleaseChecks.cs` (host only).

### Acceptance Criteria

- `docs/release-report.md` sections Installation, Rendering, URP configuration, Camera policy, State, Ownership complete with PASS/GAP only.

### Verification

- `grep -c "FAIL" docs/release-report.md` → `0`.
- `grep -c "PASS" docs/release-report.md` → ≥ 40.

## Task 31: Device, browser and sustained-run validation

### Intent

Provide real-device and real-browser evidence for the portable path, including thermal/sustained behaviour and background/resume, and name every remaining gap.

### Implementation Steps

1. Extend `Assets/Host/BenchmarkDriver.cs` with `SustainedMode`: runs `Bench_Combined` at `Balanced` for 12 minutes, sampling frame time every 10 s into `Logs/sustained-<platform>.json` (p50/p95 per minute, `SystemInfo.batteryLevel`, `Application.isFocused` transitions); on WebGL logs to the browser console (copy via DevTools export).
2. Web desktop: WebGL2 build (from Task 25 or rebuild) served locally (`python -m http.server` or Unity's Build and Run); run `Gallery.unity` and the sustained mode in current stable Chrome, Edge, Firefox on Windows; record exact browser versions (`navigator.userAgent`), GPU (`chrome://gpu`), frame-time results; test background tab for 2 minutes then resume: rain continues without a burst (dt clamp) and no console errors.
3. Web mobile: same build on Android Chrome and, if available, iOS Safari; record device/OS/browser versions and results or `GAP:`.
4. Android native: the IL2CPP build (GLES3 and Vulkan) on the available device(s): Gallery smoke (all presets), sustained 12 min, home-button background/resume, rotation; record `SystemInfo.graphicsDeviceName/Version`, results; Adreno/Mali coverage listed explicitly.
5. iOS native: Metal build if a macOS/Xcode host and device are available; otherwise `GAP: no iOS build host` (stated in `docs/support-matrix.md` as unsupported-untested).
6. Desktop optional: Windows D3D11 sustained run from the same build used in Task 25; if it passes the smoke/sustained checks, Windows joins the supported list.
7. Add sections `Web`, `Android`, `iOS`, `Desktop`, `Sustained`, `Gaps` to `docs/release-report.md`; update `docs/support-matrix.md` §Verified combinations and §Gaps.

### Required Interfaces and Contracts

- Support claims in Task 32 docs are derived only from PASS rows here.

### Error Handling and Logging

- Device crashes or driver errors are recorded verbatim (first decisive log line) with the device name.

### Tests

- Not unit tests. Sustained JSON files are the evidence.

### Acceptance Criteria

- Web desktop rows complete for at least Chrome and one other browser; Android native rows complete for at least one device (or explicit `GAP:` with reason for each missing platform).
- Sustained-run JSON exists for every platform tested; no platform shows p95 degrading by more than 30% between minute 1 and minute 12 (thermal gate), else recorded as a limitation.

### Verification

- `ls Logs/sustained-*.json | wc -l` → ≥ 2.
- `grep -c "GAP:" docs/release-report.md` → equals the number of untested platforms listed in `docs/support-matrix.md` §Gaps.

## Task 32: Version policy, packaging, newer-editor validation and documentation

### Intent

Ship 2.0.0: exported package verified from a tarball in a clean project on both the minimum editor and the newer installed editor, with README/CHANGELOG/Documentation/support matrix matching the evidence, and a docs checkpoint.

### Implementation Steps

1. Newer editor validation: run `tools/clean-install.ps1 -Editor "C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe" -Target Windows -Keep` (the script writes `ProjectVersion.txt` from the editor's version: add a `-EditorVersion` parameter defaulting to the folder name); in the kept project also run EditMode+PlayMode tests (`-runTests`) with the package's tests enabled via `testables`; record URP version resolved by 6000.5.10f1 and results in `docs/support-matrix.md` (`6000.5.10f1 | URP <x> | tests PASS/FAIL | build PASS/FAIL`). Fix source-breaking API differences in package code without adding version-conditional branches (D3); if a fix would require a branch, record the limitation and keep 6000.3 as the only verified line.
2. Create `tools/pack-and-install.ps1`: runs Unity batchmode `-executeMethod RainDropEffect.Editor.RainBuildValidation.PackPackage` (new method: `UnityEditor.PackageManager.Client.Pack("Packages/com.virtualmaestro.raindropeffect", "Builds/Package")`, waits for completion, logs the tarball path), then creates a clean project whose manifest references `"file:<tarball path>"` and runs the Windows build; verifies the tarball contents list (`tar -tzf`) contains `package/Runtime`, `package/Editor`, `package/Samples~`, `package/Documentation~`, `package/Tests` and does not contain `Assets/`, `.dll` files, or `Logs/`.
3. Version bump: `package.json` `version: "2.0.0"`, `unity: "6000.3"`, `unityRelease` omitted (any 6000.3 patch is the declared minimum; the verified patch list lives in the support matrix); `CHANGELOG.md` `## [2.0.0] - <date>` with sections Added/Changed/Removed (URP Render Graph renderer; profiles; quality; removed Built-in/GrabPass, VR prefabs, legacy API) and a link to `docs/migration-guide.md`.
4. `README.md` (package) and `Documentation~/index.md`: installation (tarball, git URL, or embedded), renderer feature setup (inspector button), quick start, profiles and layers reference (field table generated from Task 12 declarations), quality presets and measured budgets (link `docs/performance.md`), camera policy (stack rule from Task 7 M7), Scene View preview, verbose logging define `RAINDROP_VERBOSE_LOG`, migration guide link, support matrix (copy of verified rows and explicit gaps), known differences (matrix tolerances), attribution (MIT, original author).
5. Repository `README.md`: replace historical claims (DX9/VR) with a short description, link to the package README, support matrix, and the `legacy-baseline` tag for the original asset.
6. Docs checkpoint (plan setting `Docs: yes`): run `/aif-docs` to generate/refresh `docs/` landing pages from the documents above; ensure `docs/` links resolve (`grep -o "\]([^)]*\.md)" docs/*.md` and check each path exists).
7. Tag `v2.0.0` after the final commit of this task (the commit itself is made by the implementer per the commit plan; tagging is part of this task's verification).
8. Final gate: all EditMode/PlayMode tests green on 6000.3.23f1; clean-install and pack-and-install builds succeed; `docs/release-report.md` has no FAIL; `git diff master --stat -- Assets/RainDropEffect2` is not applicable (folder removed) and `git log master -1` still shows `901824a` (master untouched).

### Required Interfaces and Contracts

- Declared minimum: `6000.3`; verified: `6000.3.23f1` and `6000.5.10f1` (or documented failure); no compatibility branches.
- Distribution artifact: `Builds/Package/com.virtualmaestro.raindropeffect-2.0.0.tgz`.

### Error Handling and Logging

- Pack or tarball-install failure blocks the release; log tails are attached to `docs/release-report.md` §Packaging.

### Tests

- Full suites on both editors (via the scripts). Add `<pkg>/Tests/Editor/PackageMetadataTests.cs`: `VersionIsSemver` (`^\d+\.\d+\.\d+(-.+)?$`), `UnityMinimumIs6000_3`, `DependenciesContainOnlyUrp` (`dependencies` keys ⊆ `{com.unity.render-pipelines.universal}`), `SamplesPathsExist`.

### Acceptance Criteria

- `docs/support-matrix.md` lists both editors with results; the newer editor either passes or has a documented limitation.
- Tarball produced, installed in a clean project, Windows build succeeded.
- `package.json` is `2.0.0`; CHANGELOG, README, Documentation, migration guide, support matrix complete; `/aif-docs` checkpoint done.
- Tag `v2.0.0` exists; `master` unchanged at `901824a`.

### Verification

- `powershell -NoProfile -ExecutionPolicy Bypass -File tools\pack-and-install.ps1` → exit 0 and `Builds/Package/*.tgz` exists.
- `tar -tzf Builds/Package/com.virtualmaestro.raindropeffect-2.0.0.tgz | grep -c "\.dll$"` → `0`.
- `git rev-parse master` → `901824a26f669c0b69bd190a0bfafcb318335875`.
- `git tag --list v2.0.0` → one line.

## Phase Risks and Mitigations

- Risk: 6000.5.10f1 ships a URP where a Render Graph API changed.
  Mitigation: D3 forbids version branches; if incompatible, the support matrix states "6000.5: not supported by 2.0.0" and a follow-up plan targets it.
- Risk: no iOS/mac hardware.
  Mitigation: explicit `GAP` rows; README lists iOS as untested rather than supported.
- Risk: tarball excludes `Samples~` or includes host artifacts.
  Mitigation: tarball content check in `pack-and-install.ps1`.

## Phase Completion Checklist

- Every Task N in this phase satisfies its acceptance criteria.
- Required verification commands pass.
- `index.md` task checkboxes are updated immediately after verified completion.
