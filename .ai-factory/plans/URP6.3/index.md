<!-- aif:plan-mode:ultra -->
# Ultra Implementation Plan: URP 6.3 Rain Library Migration

Mode: ultra
Branch: URP6.3
Created: 2026-09-09

## Original Request
URP 6.3 rain library migration

## Settings
- Testing: yes
- Logging: verbose
- Docs: yes

## Research Context
Source: `.ai-factory/research/urp63-rain-library/RESEARCH.md` (Active Summary, Updated: 2026-09-09 19:55 Europe/Berlin, SHA256: b686971cee8ccafa2e00456e8805f20e8f24d5dad046f0f2c516c55c27f08d57)

Topic: Restructure Rain Drop Effect 2 as a reusable, optimized URP library for Unity 6.3 and later.

Goal: Deliver an independently installable library preserving the useful rain, static overlay, flowing trails, friction-guided trails, frozen, blood, and splash capabilities, with optional examples and measured Web/iOS/Android performance. Desktop should reuse the same renderer.

Constraints: Work on existing branch `URP6.3`; preserve `master` as the original comparison reference. Current source commit is `2845b4660030d7b36725817a7c98894e121b0326`, master reference is `901824a26f669c0b69bd190a0bfafcb318335875`. Existing editor is `6000.3.23f1`; no URP is installed. Target the Unity 6.3 URP 17.3 family, resolve and lock its exact supported patch during setup. No Built-in/HDRP backend, old Unity version branches, legacy blit path, or Compatibility Mode implementation. “6.3+” means a 6.3 minimum and an explicitly tested newer-version matrix, not an unbounded future compatibility guarantee.

Requirements: REQ-01 standalone UPM package with Runtime/Editor boundaries and opt-in samples; REQ-02 Render Graph renderer with a sampleable scene color including transparents; REQ-03 retain effect families and playback semantics while separating game/demo logic; REQ-04 bounded, allocation-free steady-state simulation and batched geometry; REQ-05 WebGL2/iOS Metal/Android portable raster path; REQ-06 documented migration and GUID/serialized-reference audit; REQ-07 install, player, visual, lifecycle, and device-performance release gates. Proposed numerical budgets are hypotheses in §7, not measurements.

Decisions: Prefer one URP-only package and one camera effect entry point, reusable immutable settings assets, per-camera runtime state, shared HLSL/material resources, and no package dependency on tooling or examples [ADR-0001]. Prefer CPU simulation with reusable batched quad/ribbon geometry. When refraction or blur is visible, use one stable scene-color source per affected camera, copy/draw into a distinct destination and hand the result to URP without a redundant copy-back; overlay-only rendering avoids scene-color capture [ADR-0002]. Run before post-processing after transparents by default. Overlapping drops sample the same undistorted scene; this changes recursive legacy GrabPass overlap and needs visual acceptance. Explicit coverage masks/blend rules must prevent flat-normal quad areas from erasing earlier rain; audit/bake coverage during asset migration and preserve camera alpha separately. Low quality removes optional blur, not the required effect families. Distortion-field composition, Jobs/Burst, compute, and GPU-driven rendering require measured justification; they are not unconditional deliverables. Disable rendering entirely for a visually inactive effect. Preserve bounded winding/order, alpha, resize, camera-stack, resource lifetime, and texture format correctness.

Risks: RISK-01 overlap/blur appearance changes; RISK-02 backbuffer, MSAA, transparent content, render scale and camera stacks; RISK-03 prefab/API migration; RISK-04 friction-map behavior, allocations and frame-dependent motion; RISK-05 mobile/browser bandwidth and thermal limits. Frozen currently means animated masks, not a physical ice-growth solver. Source inspection found allocation sites and ownership concerns, but no profiling or runtime validation was performed. GUID scanning found no library prefab/material/asset references into Demo; HP logic still exists inside a library prefab via `BloodRainCameraController`.

Open questions: Whether XR/VR or world-space glass is required; whether the old C# API must remain source compatible; minimum devices/browser versions and FPS; final package namespace/publisher identity; acceptable overlap/blur differences. Working assumptions are camera-space effect without XR or world-space glass, a new major API with one-time migration and no permanent compatibility runtime, and provisional quality budgets. These are proposals, not user-confirmed decisions. Preserve core blood/frozen/splash capability under these assumptions. PC Windows/D3D11 is an inexpensive validation target, not a separate backend.

Success signals: Clean import into an empty URP project without examples/tooling; effect absent from frames when inactive; no GrabPass, per-drop material/object allocation, or steady-state managed GC from library code; visual and lifecycle acceptance for the supported non-VR presets; correct transparent/camera behavior; measured device budgets; explicit supported editor/URP/browser/API matrix; original master untouched.

Next step: Use `$aif-plan ultra URP 6.3 rain library migration` to turn §8's phase design into an executable plan on `URP6.3`, retaining the decision gates and unresolved hardware/API questions. P1 must validate sampleable scene color, compatible renderer APIs and the direct-versus-field decision before large-scale simulation/prefab conversion. This bundle is detailed implementation design and evidence, not an executable task checklist.

## Architecture and Decisions

Working assumptions carried from the research (not user-confirmed; see Open Questions):

- **D1 Scope.** Camera-space lens effect only. No XR/VR, no world-space glass. The five `VR/*` prefabs are not converted; they remain on `master` and tag `legacy-baseline`.
- **D2 API.** New major API (`RainEffect` + `RainProfile`) with one-time conversion. No deprecated facade for `RainCameraController`/behaviours; `docs/migration-guide.md` documents replacements.
- **D3 Versions.** Declared minimum `6000.3`. Verified: `6000.3.23f1` (primary) and `6000.5.10f1` (installed newer editor, Task 32). URP version is whatever the editor resolves (17.3 family expected), locked in `packages-lock.json` and `docs/support-matrix.md`. No version-conditional code branches.
- **D4 Color space.** Host stays in Gamma (`m_ActiveColorSpace: 0`) so baseline captures remain comparable; texture roles are declared explicitly (normals, sRGB overlays, linear masks/data).

Implementation decisions (final for this plan):

- **D5 Package identity.** Embedded package `Packages/com.virtualmaestro.raindropeffect/`, display name "Rain Drop Effect URP", version `2.0.0-pre.1` → `2.0.0`. Namespaces `RainDropEffect` (Runtime), `RainDropEffect.Editor`, `RainDropEffect.Tests`, `RainDropEffect.Samples`. Assemblies `RainDropEffect.Runtime`, `RainDropEffect.Editor`, `RainDropEffect.Tests.Editor`, `RainDropEffect.Tests.Runtime`. Runtime depends only on URP/Core/Collections. Samples in `Samples~/Basic` and `Samples~/EffectsGallery`. If the user prefers another publisher id, change it before Task 4 (the id appears in asmdef references, `package.json`, `testables`, and hard-coded editor asset paths listed in Task 5/6/13).
- **D6 Renderer integration.** One `RainRendererFeature` (stateless) + one `RainRenderPass` at `RenderPassEvent.BeforeRenderingPostProcessing`, `requiresIntermediateTexture = true`. The feature enqueues only for cameras with a registered, visible `RainEffect` (Preview/Reflection cameras excluded; Scene View only via `PreviewInSceneView`).
- **D7 Injection point** is fixed; no user-selectable injection point in 2.0.0.
- **D8 Pass topology** (revised 2026-09-10 by Q8, option (a)). When any lens layer is visible: `AddBlitPass(activeColorTexture → C0 copy, MSAA none, no depth)`, optional `AddBlitPass(activeColorTexture → half-res blur)` when any visible layer uses blur, one raster pass drawing all batches into `activeColorTexture` (`SetRenderAttachment(activeColorTexture, ReadWrite)`, `UseTexture(C0)`). `resourceData.cameraColor` is **never** reassigned. Overlay-only frames draw straight into `activeColorTexture` with no copy. No pass samples its own attachment; no copy-back. Backbuffer target → warn once, skip.
  The original topology (draw into a copy, then reassign `cameraColor`) is silently dropped when the Base camera has a non-empty camera stack — the Overlay cameras keep the original attachment. Drawing into the attachment and reading from the copy costs the same one copy and needs no special case.
- **D9 Coverage and blending.** Lens pass: `Blend SrcAlpha OneMinusSrcAlpha`, `ColorMask RGB`, alpha = `CoverageMask.r × opacity`. Overlay pass: alpha = `tint.a × tex.r·g·b × opacity`. Coverage masks are baked per overlay/normal pair (`<name>_coverage.png`, R8) by `CoverageMaskBaker`; never inferred from normal magnitude at runtime. Camera alpha is never written.
- **D10 Shader contract.** URP HLSL only (`Core.hlsl`, `Packing.hlsl`, `Blit.hlsl`), one local keyword `_RAIN_BLUR`. Vertex layout `RainVertex { float3 Position (x,y normalized units, z = opacity); float2 UV; Color32 Tint; float4 Params (distortion px@1080p, relief, blur 0..1, darkness) }`. Clip position = `(x / aspect, y, 0, 1)` with no flip unless Task 7's orientation check proves otherwise (recorded in `docs/renderer-decision.md`). Scene UV via `GetNormalizedScreenSpaceUV`; refraction offset `px × n × (H/W, 1) / 1080`, clamped to `[0,1]`. Blur weight `saturate(blur × |n.x| × 8)` against a shared half-resolution 9-tap horizontal blur (radius 2/3 px for Balanced/High, none for Low). `float` for positions/UV/offsets, `half` for colors/masks.
- **D11 Ownership.** `RainEffect` (on the camera) owns `RainRuntimeState`: seeded `RainRandom`, ordered `LayerRuntime[]`, one `RainBatchMesh` (persistent managed `RainVertex[]`/`ushort[]` arrays + one `Mesh`; no `Unity.Collections` dependency) and one `Material` per layer, `RainRenderData` (batches, flags, blur radius). Profiles are immutable shared assets; `Rebuild()` recreates state. Multiple `RainEffect` components per camera are allowed and draw in component order (needed for the blood sample); one scene copy per camera per frame regardless.
- **D12 Playback semantics.** `Play()` starts emission (respecting `Delay`, `PlayOnce/Duration`); `Stop()` stops emission and lets drops/trails/fades finish; `Clear()` removes all visible state and pending delays immediately; `Play()` during Delay/Emitting is ignored; `Rebuild()` re-applies profile/quality/seed. Layer `AutoStart` plays on enable in Play Mode only.
- **D13 Timing.** Simulation ticks once per `Update` with `dt = min(Time.deltaTime, 0.1 s)` (scaled time; pause via `timeScale 0`); geometry is emitted in `LateUpdate`; rendering only reads. Edit-mode preview is opt-in (`PreviewInEditMode`) using real-time deltas. Legacy per-frame terms are converted to per-second rates at 60 Hz equivalence: Simple drift `−Down × DriftSpeed × curve` (curves are negative, drops fall), friction fall speed `Accel·t² + InitialVelocity·t` (legacy advanced by the total displacement every frame), wind cumulative `Wind × progress × dt` in every family.
- **D14 Migration tooling.** Conversion is an explicit Editor window in the host (`Assets/RainDropEffect2/Editor/Migration/LegacyRainConverter*.cs`) that reads legacy types while they still exist, writes new assets only, produces per-prefab reports, and is archived under `Documentation~/legacy-converter/` before the legacy code is deleted. Nothing converts on import.
- **D15 Units.** Normalized screen units: `1.0 = half the viewport height`, `x ∈ [−aspect, aspect]`. Legacy conversion factor `k = 1 / (distance × tan(fov/2))` (default `0.20868`). Full table in `docs/migration-matrix.md` (Task 2). Distortion stays in "pixels at 1080p reference height".
- **D16 Budgets.** Research §7 numbers are the provisional gates (Low ≤ 2 ms GPU / ≤ 0.5 ms CPU native; Balanced ≤ 3 / ≤ 0.75; High ≤ 4 / ≤ 1; Web CPU ×2; RT memory ≤ 16 MiB @720p LDR, ≤ 32 MiB @1080p LDR). Quality caps: drops 48/128/256, trails 8/16/32, points 32/64/96. Conditional optimizations (distortion field, curve baking, atlas, Jobs/Burst) are triggered only by Task 25's measured rules and become new tasks, never silent scope.
- **D17 Logging.** `RainLog` in the package: `Verbose` compiled only with `RAINDROP_VERBOSE_LOG` (host dev define), `Warn/Error` always, `WarnOnce` keyed per camera/layer. Verbose events: enable/disable, rebuild, play/stop/clear, feature create, shader-set assignment, dt clamping (throttled), bake summaries. No per-frame logs anywhere.
- **D18 Testing.** Unity Test Framework in package `Tests/Editor` and `Tests/Runtime` (host manifest `testables`); allocation assertions with `Is.Not.AllocatingGCMemory()`; rendering contract tests render to RenderTextures; benchmarks are informational JSON. Commands are listed in each phase.

## Phase Index
1. [Phase 1: Baseline and Compatibility Contract](phase-01-baseline.md) — Tasks 1-2
2. [Phase 2: URP Installation and Rendering Spike](phase-02-urp-spike.md) — Tasks 3-8
3. [Phase 3: Package Extraction and Clean-Install Gate](phase-03-package-boundary.md) — Tasks 9-11
4. [Phase 4: Playback, Settings, Simple and Static Layers](phase-04-core-runtime.md) — Tasks 12-18
5. [Phase 5: Flow, Friction and Ribbon Trails](phase-05-flow-friction.md) — Tasks 19-22
6. [Phase 6: Shader Quality, Textures and GPU Optimization](phase-06-shader-quality.md) — Tasks 23-25
7. [Phase 7: Asset Migration, Samples and Legacy Removal](phase-07-asset-migration.md) — Tasks 26-29
8. [Phase 8: Release Qualification and Version Policy](phase-08-release.md) — Tasks 30-32

## Cross-Phase Dependencies
- Task 3 depends on Task 1 because the legacy Built-in/GrabPass renderer must be captured before URP replaces it.
- Task 7 depends on Tasks 5, 6 and on Task 8's first mask (`rain_drop_coverage`) for the overlap check; Task 8 depends on Task 6's coverage contract.
- Task 12 (settings data model) depends only on Task 4 and may be implemented in parallel with the spike; Tasks 13–18 depend on Task 7's accepted renderer decision. If Task 7 records a topology change, Task 5/6 are amended before Task 13 starts.
- Task 9 depends on Task 8 (masks are baked from `Assets/` paths, then the baker's source list moves with the textures).
- Task 16 depends on Task 17's `BasicRainProfile` for the host smoke scene; Task 17 depends on Tasks 12–13 for the types it edits (implement 12 → 13 → 14 → 15 → 17 → 16 → 18).
- Tasks 19–22 depend on Tasks 12–16 (settings, emitter, batch wiring) and Task 2's friction/flow unit rows.
- Task 23 depends on Tasks 19–22 (blur is tuned on flow/friction presets) and Task 7's blur measurements.
- Task 25 depends on Task 11's build script and Task 22's CPU benchmark.
- Task 26 depends on Task 2 (constants), Task 8 (masks), Task 21 (friction field), Task 12 (field names).
- Task 27 depends on Tasks 23 and 26; Task 28 depends on Task 27's prefabs; Task 29 depends on Tasks 24, 27, 28.
- Tasks 30–32 depend on Task 29 (legacy removed) and Task 25 (budgets); Task 32 depends on Tasks 30–31 for support claims.

## Tasks

### Phase 1: Baseline and Compatibility Contract
- [x] Task 1: Capture legacy baseline ([details](phase-01-baseline.md#task-1-capture-legacy-baseline))
- [x] Task 2: Write the migration matrix ([details](phase-01-baseline.md#task-2-write-the-migration-matrix)) (depends on 1)

### Phase 2: URP Installation and Rendering Spike
- [x] Task 3: Install and lock URP for the host project ([details](phase-02-urp-spike.md#task-3-install-and-lock-urp-for-the-host-project)) (depends on 1)
- [x] Task 4: Create the package skeleton and assembly boundaries ([details](phase-02-urp-spike.md#task-4-create-the-package-skeleton-and-assembly-boundaries)) (depends on 3)
- [x] Task 5: Implement the renderer feature and Render Graph pass ([details](phase-02-urp-spike.md#task-5-implement-the-renderer-feature-and-render-graph-pass)) (depends on 4)
- [x] Task 6: Author the shader contract and reusable batch mesh ([details](phase-02-urp-spike.md#task-6-author-the-shader-contract-and-reusable-batch-mesh)) (depends on 4)
- [x] Task 7: Build and validate the vertical slice ([details](phase-02-urp-spike.md#task-7-build-and-validate-the-vertical-slice)) (depends on 5, 6, 8)
- [x] Task 8: Bake coverage masks for all texture pairs ([details](phase-02-urp-spike.md#task-8-bake-coverage-masks-for-all-texture-pairs)) (depends on 6)

### Phase 3: Package Extraction and Clean-Install Gate
- [x] Task 9: Move retained textures into the package with GUIDs preserved ([details](phase-03-package-boundary.md#task-9-move-retained-textures-into-the-package-with-guids-preserved)) (depends on 4, 8)
- [x] Task 10: Create the Basic sample and documentation skeleton ([details](phase-03-package-boundary.md#task-10-create-the-basic-sample-and-documentation-skeleton)) (depends on 4)
- [x] Task 11: Clean-install and no-sample player build gate ([details](phase-03-package-boundary.md#task-11-clean-install-and-no-sample-player-build-gate)) (depends on 9, 10)

### Phase 4: Playback, Settings, Simple and Static Layers
- [x] Task 12: Declare settings types, quality caps, and the profile asset ([details](phase-04-core-runtime.md#task-12-declare-settings-types-quality-caps-and-the-profile-asset)) (depends on 4)
- [x] Task 13: Implement the RainEffect component, runtime state, and lifecycle ([details](phase-04-core-runtime.md#task-13-implement-the-raineffect-component-runtime-state-and-lifecycle)) (depends on 12, 7)
- [x] Task 14: Implement shared emission state and the Simple layer ([details](phase-04-core-runtime.md#task-14-implement-shared-emission-state-and-the-simple-layer)) (depends on 13)
- [x] Task 15: Implement the Static layer ([details](phase-04-core-runtime.md#task-15-implement-the-static-layer)) (depends on 13)
- [x] Task 16: Wire simulations into render data and implement the inactive fast path ([details](phase-04-core-runtime.md#task-16-wire-simulations-into-render-data-and-implement-the-inactive-fast-path)) (depends on 14, 15, 17)
- [x] Task 17: Editor inspector, profile creation, and edit-mode preview ([details](phase-04-core-runtime.md#task-17-editor-inspector-profile-creation-and-edit-mode-preview)) (depends on 13)
- [x] Task 18: Tests for state, lifecycle, allocations, and camera independence ([details](phase-04-core-runtime.md#task-18-tests-for-state-lifecycle-allocations-and-camera-independence)) (depends on 16)

### Phase 5: Flow, Friction and Ribbon Trails
- [x] Task 19: Implement the bounded trail buffer and ribbon emission ([details](phase-05-flow-friction.md#task-19-implement-the-bounded-trail-buffer-and-ribbon-emission)) (depends on 16)
- [x] Task 20: Implement the Flow layer ([details](phase-05-flow-friction.md#task-20-implement-the-flow-layer)) (depends on 19)
- [x] Task 21: Bake the friction field and implement the Friction layer ([details](phase-05-flow-friction.md#task-21-bake-the-friction-field-and-implement-the-friction-layer)) (depends on 19, 2)
- [x] Task 22: Trail, flow, friction tests and the dense-trail benchmark ([details](phase-05-flow-friction.md#task-22-trail-flow-friction-tests-and-the-dense-trail-benchmark)) (depends on 20, 21)

### Phase 6: Shader Quality, Textures and GPU Optimization
- [x] Task 23: Finalize shared blur and quality gating ([details](phase-06-shader-quality.md#task-23-finalize-shared-blur-and-quality-gating)) (depends on 22)
- [x] Task 24: Texture import policy, shader variants, and build inclusion audit ([details](phase-06-shader-quality.md#task-24-texture-import-policy-shader-variants-and-build-inclusion-audit)) (depends on 11, 23)
- [x] Task 25: Quality presets, benchmark scene and measured budgets ([details](phase-06-shader-quality.md#task-25-quality-presets-benchmark-scene-and-measured-budgets)) (depends on 24)

### Phase 7: Asset Migration, Samples and Legacy Removal
- [x] Task 26: Implement the legacy prefab converter ([details](phase-07-asset-migration.md#task-26-implement-the-legacy-prefab-converter)) (depends on 2, 8, 21, 12)
- [x] Task 27: Convert all presets and produce the visual comparison report ([details](phase-07-asset-migration.md#task-27-convert-all-presets-and-produce-the-visual-comparison-report)) (depends on 23, 26)
- [x] Task 28: Build the Basic and Effects Gallery samples ([details](phase-07-asset-migration.md#task-28-build-the-basic-and-effects-gallery-samples)) (depends on 27)
- [x] Task 29: Remove the legacy runtime and host scaffolding ([details](phase-07-asset-migration.md#task-29-remove-the-legacy-runtime-and-host-scaffolding)) (depends on 24, 27, 28)

### Phase 8: Release Qualification and Version Policy
- [x] Task 30: Execute the installation, rendering, camera, state and ownership matrix ([details](phase-08-release.md#task-30-execute-the-installation-rendering-camera-state-and-ownership-matrix)) (depends on 29, 25)
- [x] Task 31: Device, browser and sustained-run validation ([details](phase-08-release.md#task-31-device-browser-and-sustained-run-validation)) (depends on 29, 25)
- [x] Task 32: Version policy, packaging, newer-editor validation and documentation ([details](phase-08-release.md#task-32-version-policy-packaging-newer-editor-validation-and-documentation)) (depends on 30, 31)

## Commit Plan
- **Commit 1** (after tasks 1-2): "chore(baseline): capture legacy reference renders and write migration matrix"
- **Commit 2** (after tasks 3-4): "build(urp): install URP for Unity 6.3 and add package skeleton with asmdefs"
- **Commit 3** (after tasks 5-8): "feat(render): Render Graph rain pass, lens/overlay/blur shaders, batch mesh, coverage baker and spike results"
- **Commit 4** (after tasks 9-11): "refactor(package): move textures into package, add Basic sample and clean-install gate"
- **Commit 5** (after tasks 12-15): "feat(runtime): profiles, RainEffect lifecycle, emitter, simple and static layers"
- **Commit 6** (after tasks 16-18): "feat(runtime): render-data wiring, inactive fast path, inspector, lifecycle and allocation tests"
- **Commit 7** (after tasks 19-22): "feat(runtime): bounded trails, flow and friction layers with baked friction field and tests"
- **Commit 8** (after tasks 23-25): "perf(render): shared blur, texture policy, variant audit and measured quality budgets"
- **Commit 9** (after tasks 26-27): "feat(migration): legacy prefab converter, converted profiles and visual comparison report"
- **Commit 10** (after tasks 28-29): "refactor(samples): Basic and Effects Gallery samples; remove legacy runtime, shaders, prefabs and demos"
- **Commit 11** (after tasks 30-32): "release: 2.0.0 qualification report, support matrix, packaging and documentation"

## Definition of Done
- All 32 task checkboxes above are checked with their phase acceptance criteria and verification commands satisfied.
- `master` still points to `901824a26f669c0b69bd190a0bfafcb318335875`; tag `legacy-baseline` points to `2845b4660030d7b36725817a7c98894e121b0326`; tag `v2.0.0` exists on `URP6.3`.
- `Packages/com.virtualmaestro.raindropeffect` builds in an empty URP project without samples (Task 11/32 scripts) and its Runtime contains no legacy identifiers, `GrabPass`, `Shader.Find`, or `Resources.Load`.
- EditMode and PlayMode suites are green on 6000.3.23f1; allocation tests pass for the densest profile; lifecycle test shows zero resource growth across 100 cycles.
- `docs/release-report.md` contains no `FAIL`; every untested platform is a named `GAP` mirrored in `docs/support-matrix.md` and the package README.
- `docs/migration-matrix.md`, `docs/migration-report.md`, `docs/migration-guide.md`, `docs/renderer-decision.md`, `docs/performance.md`, `docs/support-matrix.md` exist and agree with the code.
- Docs checkpoint executed via `/aif-docs` (plan setting `Docs: yes`); `AGENTS.md` structure map updated. `.ai-factory/DESCRIPTION.md`, `ARCHITECTURE.md`, and `rules/base.md` still describe the legacy system and are updated by their owner workflows after this plan (out of scope here).

## Open Questions
None of these block implementation; each has a working assumption used by the plan. Answering one changes the listed tasks.

- **Q1 XR/VR required?** Assumed no (D1). Yes → new tasks for stereo descriptors/eye UVs and headset validation before Task 7 is accepted; VR prefabs re-enter Task 26.
- **Q2 World-space glass required?** Assumed no (D1). Yes → separate feature plan; not covered by this migration.
- **Q3 Old C# API source compatibility?** Assumed no (D2). Yes → add a deprecated facade task after Task 13 with an explicit removal policy.
- **Q4 Target devices/browsers/FPS?** Assumed research §7 budgets (D16) and whatever hardware is on hand. Named devices → rebudget in Task 25 and re-run Task 31.
- **Q5 Strict legacy overlap/blur parity?** Assumed no; stable-source rule accepted with documented tolerances (Task 2). Yes → revisit Task 7/23 before Phase 4.
- **Q6 Publisher/package id?** Assumed `com.virtualmaestro.raindropeffect` (D5). Different id → change before Task 4.
- **Q8 Camera stacks (raised by Task 7, M7) — ANSWERED.** With a Base camera that has a non-empty camera
  stack, the pass's `resourceData.cameraColor = target` reassignment is not honoured: the Overlay
  cameras keep rendering into URP's original camera-color attachment, so the copy carrying the rain
  is dropped and nothing is visible (measured mean difference 0.00 against a no-rain frame; every
  other matrix case passes). Fix candidates, both keeping "one copy, no copy-back, never sample the
  attachment": (a) invert the topology — copy `activeColorTexture` into a read-only `C0`, then draw
  into `activeColorTexture` and never reassign `cameraColor`; (b) keep the current topology for
  single cameras and use (a) only when the camera has a stack. Option (a) is the smaller rule and
  costs the same one copy.
  **Answered 2026-09-10: option (a).** D8 above is revised; Task 13 implements the inverted topology
  and Task 30's camera matrix tests it.

- **Q7 Physical freezing expected?** Assumed no; frozen stays mask-based (Task 15). Yes → separate feature with its own budget.
