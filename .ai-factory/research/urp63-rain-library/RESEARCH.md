# Research: URP 6.3 rain-effect library migration

Updated: 2026-09-09 19:55 Europe/Berlin
Status: active
Index: [INDEX.md](INDEX.md)

## Active Summary (input for $aif-plan)
<!-- aif:active-summary:start -->
Topic: Restructure Rain Drop Effect 2 as a reusable, optimized URP library for Unity 6.3 and later.

Goal: Deliver an independently installable library preserving the useful rain, static overlay, flowing trails, friction-guided trails, frozen, blood, and splash capabilities, with optional examples and measured Web/iOS/Android performance. Desktop should reuse the same renderer.

Constraints: Work on existing branch `URP6.3`; preserve `master` as the original comparison reference. Current source commit is `2845b4660030d7b36725817a7c98894e121b0326`, master reference is `901824a26f669c0b69bd190a0bfafcb318335875`. Existing editor is `6000.3.23f1`; no URP is installed. Target the Unity 6.3 URP 17.3 family, resolve and lock its exact supported patch during setup. No Built-in/HDRP backend, old Unity version branches, legacy blit path, or Compatibility Mode implementation. “6.3+” means a 6.3 minimum and an explicitly tested newer-version matrix, not an unbounded future compatibility guarantee.

Requirements: REQ-01 standalone UPM package with Runtime/Editor boundaries and opt-in samples; REQ-02 Render Graph renderer with a sampleable scene color including transparents; REQ-03 retain effect families and playback semantics while separating game/demo logic; REQ-04 bounded, allocation-free steady-state simulation and batched geometry; REQ-05 WebGL2/iOS Metal/Android portable raster path; REQ-06 documented migration and GUID/serialized-reference audit; REQ-07 install, player, visual, lifecycle, and device-performance release gates. Proposed numerical budgets are hypotheses in §7, not measurements.

Decisions: Prefer one URP-only package and one camera effect entry point, reusable immutable settings assets, per-camera runtime state, shared HLSL/material resources, and no package dependency on tooling or examples [ADR-0001]. Prefer CPU simulation with reusable batched quad/ribbon geometry. When refraction or blur is visible, use one stable scene-color source per affected camera, copy/draw into a distinct destination and hand the result to URP without a redundant copy-back; overlay-only rendering avoids scene-color capture [ADR-0002]. Run before post-processing after transparents by default. Overlapping drops sample the same undistorted scene; this changes recursive legacy GrabPass overlap and needs visual acceptance. Explicit coverage masks/blend rules must prevent flat-normal quad areas from erasing earlier rain; audit/bake coverage during asset migration and preserve camera alpha separately. Low quality removes optional blur, not the required effect families. Distortion-field composition, Jobs/Burst, compute, and GPU-driven rendering require measured justification; they are not unconditional deliverables. Disable rendering entirely for a visually inactive effect. Preserve bounded winding/order, alpha, resize, camera-stack, resource lifetime, and texture format correctness.

Risks: RISK-01 overlap/blur appearance changes; RISK-02 backbuffer, MSAA, transparent content, render scale and camera stacks; RISK-03 prefab/API migration; RISK-04 friction-map behavior, allocations and frame-dependent motion; RISK-05 mobile/browser bandwidth and thermal limits. Frozen currently means animated masks, not a physical ice-growth solver. Source inspection found allocation sites and ownership concerns, but no profiling or runtime validation was performed. GUID scanning found no library prefab/material/asset references into Demo; HP logic still exists inside a library prefab via `BloodRainCameraController`.

Open questions: Whether XR/VR or world-space glass is required; whether the old C# API must remain source compatible; minimum devices/browser versions and FPS; final package namespace/publisher identity; acceptable overlap/blur differences. Working assumptions are camera-space effect without XR or world-space glass, a new major API with one-time migration and no permanent compatibility runtime, and provisional quality budgets. These are proposals, not user-confirmed decisions. Preserve core blood/frozen/splash capability under these assumptions. PC Windows/D3D11 is an inexpensive validation target, not a separate backend.

Success signals: Clean import into an empty URP project without examples/tooling; effect absent from frames when inactive; no GrabPass, per-drop material/object allocation, or steady-state managed GC from library code; visual and lifecycle acceptance for the supported non-VR presets; correct transparent/camera behavior; measured device budgets; explicit supported editor/URP/browser/API matrix; original master untouched.

Next step: Use `$aif-plan ultra URP 6.3 rain library migration` to turn §8's phase design into an executable plan on `URP6.3`, retaining the decision gates and unresolved hardware/API questions. P1 must validate sampleable scene color, compatible renderer APIs and the direct-versus-field decision before large-scale simulation/prefab conversion. This bundle is detailed implementation design and evidence, not an executable task checklist.
<!-- aif:active-summary:end -->

## 1. Evidence and scope

This investigation is read-only except for this research bundle. It did not install URP, alter assets, compile shaders, run Unity, measure performance, or capture reference images. Code graph discovery was followed by targeted source analysis and a serialized GUID-reference scan. Unity lifecycle and virtual dispatch are incompletely represented by graph call edges; source is authoritative.

The user's explicit migration request supersedes the old context documents' maintenance-only constraints. It does not remove the need for a concrete strategy for GUIDs, serialized fields and shader references. `.ai-factory/ARCHITECTURE.md`, `DESCRIPTION.md`, and `rules/base.md` remain descriptions of the old system until their owner workflows update them. They were not rewritten here.

### Source evidence register

Paths below are relative to the repository. Line numbers identify the inspected revision, not a promise of future locations.

| ID | Evidence | Finding and interpretation |
|---|---|---|
| E01 | `ProjectSettings/ProjectVersion.txt`; `Packages/manifest.json`; `Packages/packages-lock.json` | Editor `6000.3.23f1`; no `com.unity.render-pipelines.universal` or render-pipeline package in the manifest/lock. URP setup is real work, not a shader-only change. |
| E02 | `Assets/RainDropEffect2/Scripts/Camera/RainCameraController.cs:24,149,199,217` | Discovers child behaviours, sets camera/effect parameters and dispatches playback. Legacy camera-plane rendering and renderQueue order are coupled to effect control. |
| E03 | `Assets/RainDropEffect2/Scripts/RainBehaviours/*/*Behaviour.cs`; `Common/RainBehaviourBase.cs` | Four families: Simple, Static, Flow, FrictionFlow. Behaviours construct controller GameObjects and forward Update; immediate stop destroys controllers. Preserve observable semantics, not this object graph. |
| E04 | `Assets/RainDropEffect2/Scripts/Common/RainDrawer.cs:81-164`; `RainDropTools.cs:52-116` | Per-drawer material/quad/renderer; material replacement on shader changes; string property setters. Refresh invalidates material/mesh references without explicit destruction in this path. Ownership/leak risk requires lifecycle validation; it is not a measured leak claim. |
| E05 | `Assets/RainDropEffect2/Scripts/Common/DropTrail.cs:80,145,216-256` | Active trails update a path list and allocate vertex, UV and index arrays for mesh rebuilding. `Path` objects are also created as points are added. |
| E06 | `Assets/RainDropEffect2/Scripts/RainBehaviours/FlowRain/FlowRainController.cs:186,213,250,279,329` | LINQ/list allocations, coroutine/closure scheduling, material creation on initialization, per-drop renderQueue/material updates. Some movement terms do not multiply by deltaTime. |
| E07 | `Assets/RainDropEffect2/Scripts/RainBehaviours/FrictionFlowRain/FrictionFlowRainController.cs:306-375` | Per-step dictionary, list copy/sort and CPU `GetPixel`; temporary Transform math. “Weighted” selection actually selects a maximum score, randomizing only when all values match. Preserve/redefine this explicitly; do not replace it with weighted random sampling by name alone. Negative viewport Y and texture wrap behavior need boundary fixtures. |
| E08 | `Assets/RainDropEffect2/Shaders/Resources/RainDistortion (Forward).shader:29`; `(Mobile).shader:24`; `(Deffered).shader:23`; `RainDropGlobal.cginc:16-30` | Distortion shaders use GrabPass. Forward/deferred grabs are unnamed; mobile names a shared background. Forward blur uses nine horizontal background samples per shaded fragment. The four shaders depend on legacy Cg/UnityCG; deferred uses a surface shader. |
| E09 | `Assets/RainDropEffect2/Prefabs/Frozen.prefab`; `Scripts/RainBehaviours/StaticRain/StaticRainVariables.cs` | Frozen is a composition of static textured masks and fades; no physical freezing simulation is present. |
| E10 | `Assets/RainDropEffect2/Scripts/Misc/BloodRainCameraController.cs`; `Prefabs/BloodRain.prefab` | HP/Attack/reset/game timing live under library Scripts and are referenced by a reusable prefab. Keep blood rendering data; move this gameplay example out of Runtime. |
| E11 | `Assets/RainDropEffect2/Demo/Scripts/DemoScene1.cs`; `DemoScene2.cs`; `Scripts/Misc/FpsDisplay.cs` | Demo resolution/orientation/GUI/audio and FPS overlay are example concerns. Both demo scripts request a 512-high resolution and refresh-rate argument 15; this is not evidence of asset performance. |
| E12 | Tracked-file and GUID scan at E01 revision | 25 C# files, 4 shaders, 1 cginc, 18 library prefabs: 10 general, 3 Mobile, 5 VR. Five demo scenes. No asset-local asmdefs/tests. No references from scanned library `.prefab/.mat/.asset` files into `Demo/`; this does not prove absence of every dynamic/path reference. |
| E13 | `Assets/Plugins/NuGet/*.dll`; manifest; runtime using directives | 42 tracked NuGet DLLs include MCP, Roslyn and Microsoft dependencies. Runtime scripts import Unity/System/RainDropEffect. Treat tooling as host-only; do not ship it in the effect package or delete it blindly from the development project. |
| E14 | `LICENSE`; `README.md` | MIT license, copyright Gaku Morio, 2017. Retain notices. DX9/VR claims in README are historical and require replacement by a tested support matrix. |
| E15 | `Textures/Rain/friction_map.tga.meta` under the asset root | Readable texture, sRGB flag enabled, legacy importer settings. Changing to linear/baked sampling changes interpretation; migrate against a reference, not by bulk toggling import flags. |

## 2. Product contract

The first release is a camera-image effect: rain on a lens/window in front of the viewer. It includes static droplets/frame masks, transient droplets, flowing trails, friction-guided flow, frozen masks, blood coloration/overlays, and splash-in/out playback. It accepts perspective and orthographic consumer cameras without a second legacy effect camera. Physical fluid interaction, merging droplets, actual ice thermodynamics, windshield wipers and world-space refractive materials are not implied by the existing feature set.

| Requirement | Intended result | Acceptance evidence |
|---|---|---|
| REQ-01 Packaging | Runtime and Editor install without demos, audio, prototype meshes, HP logic, MCP or NuGet tooling | Empty-project import and build with samples absent |
| REQ-02 URP rendering | Render Graph only; correct scene color after transparents; integration on the intended camera once | Frame Debugger / Render Graph capture and pixel fixtures |
| REQ-03 Feature parity | Every supported original non-VR preset maps to a new profile/example with equivalent intent | Before/after captures and explicit approved differences |
| REQ-04 Efficiency | Bounded state, reusable mesh storage, no per-drop GameObjects/materials, no steady-state library GC | CPU/allocation profiler and object-count soak |
| REQ-05 Platforms | One portable raster implementation for WebGL2, Metal, Android GLES3/Vulkan; optional PC | Real player builds and named-device matrix |
| REQ-06 Migration | No silent missing scripts, lost curves, broken GUID references or changed units | Migration report plus prefab/scene validation |
| REQ-07 Release | Minimum-version validation, tested newer versions, repeatable installation and usage | Release report with revisions, settings and device results |

Working assumptions remain open until answered: no XR/VR in the first release; no world-space glass; major-version API changes are acceptable with one-time migration. If XR is required, add single-pass instanced/multiview texture handling, eye-correct UVs, stereo resource descriptors, camera policy and actual headset tests before locking the renderer design. Existing VR prefabs are not evidence that URP XR will work. If old source compatibility is required, retain a thin deprecated facade with an explicit removal policy; do not reintroduce the old rendering backend.

## 3. Library and asset structure

Preferred source boundary: one embedded UPM package at `Packages/<publisher-package-id>/`, consumed by the existing Unity project as the validation host. `<publisher-package-id>` is a deliberate unresolved publisher choice; the structure below describes responsibilities, not files created by this investigation.

| Destination | Contents | Dependency rule |
|---|---|---|
| `Runtime/` | Camera component, profile/serialized settings, simulation, mesh batching, RendererFeature/passes, HLSL, runtime textures and reusable presets | Unity + URP/Core; never Editor, Samples, host or MCP |
| `Editor/` | Inspectors, validation and explicit migration utilities | Runtime + UnityEditor; Editor-only asmdef |
| `Samples~/Basic/`, `Samples~/EffectsGallery/` in distributed package | Minimal setup and gallery, HP/audio/UI/FPS example scripts, showcase assets | Depend on Runtime, never the reverse |
| `Tests/Editor/`, `Tests/Runtime/` | Focused migration/state tests and rendering/lifecycle fixtures, opt-in test assemblies | Depend on Runtime; excluded from normal players |
| `Documentation~/`, package README, CHANGELOG, LICENSE | Integration, quality, migration, tested support and attribution | No runtime references |
| Host `Assets/` + `ProjectSettings/` | Minimal acceptance scenes, build profiles, renderer/pipeline configuration and development tooling | Host consumes package |

Use Runtime and Editor asmdefs because the library boundary now has a concrete purpose. Do not create a separate assembly per rain family or an abstraction package for one renderer. A single URP dependency is intentional; a general pipeline interface provides no value here.

For manually authored UPM distribution, use an actual `Samples~` path and matching manifest entries. If adopting Unity 6.3's native Create/Export package workflow instead, use its documented source `Samples` path and automatic export rename. Verify the exported manifest and import path in a clean consumer project; do not mix the two workflows accidentally. [U06, U07]

Migration mapping: existing effect textures/profiles belong in Runtime when default presets need them. Demo sounds, prototype materials/meshes, rotating objects and HP example belong in Samples. Blood visuals remain available without the HP script. Replace separate Cheap/Expensive/mobile prefab hierarchies with shared profiles plus quality settings. Under the no-XR assumption, VR scene/prefab code is excluded from the new deliverable and stays accessible in master; removal occurs only after the scope decision and reference audit.

Retain original GUIDs for assets/types that remain the same identity. Move asset plus `.meta` through Unity-aware operations. New profile/shader identities receive new GUIDs. Conversion must remap materials/shaders/fields explicitly; preserving every legacy shader name inside the new runtime would conflict with removing the old backend. A major-version migration can break old script calls, but must document replacements. Retain MIT attribution and inspect third-party sample notices before redistribution.

## 4. Renderer design

The preferred portable design is explained in [ADR-0002](ADR-0002-portable-rain-rendering.md) and [C4-COMPONENT-rendering.md](C4-COMPONENT-rendering.md). It is selected as a design proposal; implementation and profiling must validate it.

### Frame and resource contract

At the default injection point after scene transparents and before post-processing, take the current sampleable camera color C0. Allocate an appropriate destination C1 through Render Graph, preserving the required camera format, dimensions, texture dimension, viewport and dynamic-scale semantics. Initialize C1 with C0, then rasterize ordered rain batches into C1 while sampling C0. Forward C1 as the new URP camera color. C0 stays immutable throughout the effect. No pass samples its own render attachment and no final copy-back is added merely to restore the old handle. [U02, U03, U04]

A prototype may perform the initial copy and rain draws in one raster pass if the URP API/resource contract permits; otherwise use one copy pass and one raster pass. Measure resulting native passes instead of equating logical pass count with GPU bandwidth. Render Graph can optimize lifetimes and merge eligible passes; refraction's arbitrary offset scene reads still require external texture bandwidth. Framebuffer-fetch/input attachments alone do not implement this sampling pattern. [U01]

If active color is the backbuffer, arrange an intermediate sampleable target through the installed URP version's supported feature/pass mechanism and validate it with Intermediate Texture=Auto. A guard that simply skips rendering on a backbuffer is a diagnostic safeguard, not a successful integration. Start with a supported single-sample source/destination resolve path; validate MSAA 2x/4x, HDR on/off and the cost of any resolve. Do not apply `CopyTexture` blindly across incompatible sample counts/formats. The exact 17.3 patch/API shape remains to be compiled in phase P1.

Do not substitute `_CameraOpaqueTexture` when transparent objects must refract: it represents a different point in the scene. Do not request depth, normals or motion vectors for an effect that does not use them. Support Screen Space Overlay UI remaining sharp; camera-space/world-space UI rendered before the rain is part of the source and may distort. Document that distinction. Defer an extra user-selectable injection point unless a real integration requires it.

### Batched geometry and layer semantics

Reuse quad and ribbon mesh buffers. Group consecutive compatible primitives by texture set, shader mode and blend/order requirements; carry per-drop intensity, tint and other varying values through vertex attributes. Share materials per actual render configuration. Stable order matters: never globally sort transparent layers for fewer draw calls if doing so changes the picture. Atlas packing is an optional measured improvement with padding/normal-import checks; texture arrays, indirect draws and instancing are not baseline requirements.

No one-MonoBehaviour/Transform/Material/MeshRenderer per drop. Keep meaningful Simple/Static/Flow/Friction algorithms, represented by bounded data under one effect owner. One static full-screen layer is a quad, not an unnecessarily rebuilt particle list. Preserve overlay-only rendering without capturing scene color when neither distortion nor blur is visible.

All drops in a frame sample C0. Existing Forward unnamed GrabPass can capture already-rendered effects, so stacked refraction changes. Define the new rule and compare edge overlaps, blood tint and frozen layers. Do not promise exact legacy pixels or rebuild recursive copies per drop to hide this difference. Required appearance parity is preset intent and documented tolerances.

Coverage is a required part of that design, not an incidental blend setting. The legacy Forward/Mobile distortion passes have no alpha-blend declaration: a rectangular quad can replace the background with a fresh grab. With a shared C0, blindly copying that behavior can erase earlier rain across the next quad's flat-normal area. Define explicit per-pixel coverage and a consistent premultiplied/straight-alpha blend contract; audit whether each shipped texture has a usable mask and bake/author a dedicated mask where needed. Do not infer coverage only from normal magnitude, which can be zero inside a real drop. P1 must include overlapping quads with visible earlier rain, and P6 must map coverage for every preset. Preserve camera output alpha separately from the local rain coverage.

### Shader contract

Replace CGPROGRAM/UnityCG and the unused legacy deferred/surface path with URP HLSL. Use URP/Core includes, modern texture macros, explicit material constants and integer property IDs. Use `UnityPerMaterial` for material properties and appropriate engine declarations; verify compatibility rather than claiming SRP Batcher merges draws. Explicit batched mesh rendering has a different cost model from ordinary MeshRenderer batching. [U09]

Use float for clip/screen positions, UV offsets, texel-scale calculations and long-running time; evaluate half for bounded colors, masks and normals on target devices. Do not bulk replace everything with half, and do not use HLSL `h` suffixes as a precision guarantee. [U10] Use a documented normalized UV/pixel unit for distortion and test aspect ratio/resolution invariance. Clamp offscreen refraction deliberately and preserve camera alpha where the host requires it.

Remove old register bindings, ARB precision hints, gles/DX9-era restrictions, `ZWrite On` lens geometry and irrelevant fallbacks. Lens passes use no depth writes. Keep variant dimensions small and local to actual features: overlay/refraction plus optional blur where needed. Use serialized shader/material references for build inclusion; remove reliance on `Shader.Find`/Resources strings after migration. Do not indiscriminately strip runtime-selected variants.

### Blur

Low quality disables blur but retains distortion/tint/frozen masks. For blur-enabled profiles, compare the old appearance against a shared low-resolution blur of C0, sampled locally under the effect. Generate it only if a visible layer requests blur. Start with a small fixed separable kernel and half-resolution dimensions; cap radius and sample count. The old blur is horizontal and normal-dependent, so a two-axis blur is a visual change, not a pixel-equivalent optimization. Choose quality presets from A/B captures. Do not build a universal blur pyramid unless differing radii measurably need it.

### Cameras and lifetime

Store mutable simulation/resources per effect owner, not in globally shared profiles or materials. Tick simulation once per game frame; rendering may occur several times for cameras. Base/Overlay stacks must have one explicit owner and one application to the intended final scene color, with no repeated simulation or double intensity. Prototype the installed URP stack/post-processing ordering before fixing ownership rules; passing a single-camera test is insufficient.

Game cameras are opt-in through the effect component. Scene View preview is explicit; Preview, reflection and unrelated cameras are skipped. Handle camera enable/disable, destruction, renderer-feature recreation, scene reload, domain-reload-disabled Play Mode, resize, orientation, render scale, target RenderTexture and pause/resume. Transient TextureHandles do not persist across frames/cameras. Explicitly release owned materials/meshes/any external RTHandles; graph-created transient textures belong to the graph. Per-camera pass data must not read mutable settings later overwritten while another camera is recorded.

## 5. Simulation and API refactor

Proposed public surface: a camera-attached effect component with a reusable settings/profile asset, Play, graceful Stop, immediate Clear/Stop, IsPlaying, intensity, wind/gravity and optional deterministic seed. Names become final in planning after API-compatibility input. Separate emission stopped from visuals finished: graceful Stop drains existing drops/trails, while immediate stop removes visible state and queued delayed emission. Restart cannot awaken an earlier coroutine or emit after a stop.

| Concern | Minimum useful change | Verification |
|---|---|---|
| Data lifecycle | Reuse bounded arrays/lists of value-type particle/trail state; initialize capacity once per quality change | Warmed steady-state 0 B/frame from effect code; stable object counts |
| Spawn logic | Counters and direct loops replace FindAll/Take/ToList; handle zero rates, inverted ranges, zero duration/lifetime and capacity limits | Focused state test covering delay, one-shot, stop, restart and cap |
| Timing | Delta-time-consistent movement and fade; bounded substeps for long frames; explicit scaled-time behavior | Same seeded simulation at 30/60/120 Hz within stated tolerance |
| Trail geometry | Reuse vertex/index/UV buffers and active ranges; bounded ring or compact point storage; deterministic winding and point expiry | No degenerate/NaN ribbons, no unbounded growth, stable 16-bit index splitting |
| Flow fluctuation | Replace per-drop coroutine closures with timestamps/state at the same cadence | Stop/restart and seeded fluctuation regression |
| Friction | Cache/bake a compact scalar grid once; sample with explicit coordinate and edge rules; direct candidate scan instead of dictionary/sort/Transform objects | Equal-value tie, maxima, border, missing map, zero size and deterministic path tests |
| Curves | Preserve authored curves initially; bake lookup tables only if measured hot | Compare curve endpoints, overshoot and representative lifetime values |
| Settings | Shared immutable profiles, per-camera overrides/state; validate edits without rebuilding every frame | Two cameras sharing a profile remain independent |

Friction-map migration must document the old sign/wrap/gamma interpretation before replacing it with normalized viewport sampling. If a readable source texture is needed for import, bake during Editor import/conversion and ship a compact data asset without a permanent extra readable GPU texture copy. Avoid adding compute/readback as a shortcut. Do not call a new probabilistic model a bug fix for the old maximum-selection routine.

Finite user inputs, capacities, null textures/profiles and unsupported formats receive bounded fallbacks or one actionable diagnostic; no per-frame logging. Fixed-step catch-up must have a ceiling so browser tab resume does not simulate minutes of missed rain. The host controls overall resolution, orientation, refresh target and quality; Runtime does not change global Player/Quality settings.

## 6. Platform and optimization policy

“Implement all optimizations” is interpreted as implementing every relevant proven optimization and measuring conditional ones. Enabling all Unity rendering features simultaneously is not an optimization strategy: lighting features, GPU Resident Drawer, occlusion culling, compute and asynchronous compute do not automatically improve a small screen-space effect.

| Platform | Baseline / validation | Limits and follow-up |
|---|---|---|
| Web desktop | WebGL2 player; current stable Chrome/Edge/Firefox on Windows and Safari on macOS, with actual versions recorded | WebGPU is Experimental in the verified 6.3 manual; optional exploratory build, never required [U11, U12] |
| Web mobile | Android Chrome and iOS Safari on named devices | Unity documents mobile browser support, but minimum engine/browser support is not this effect's performance guarantee. Browser memory, tab/background resume and thermal load need tests [U12] |
| iOS native | Metal player, actual iPhone/iPad; IL2CPP | Xcode/device build environment required; Metal precision, format and tiler behavior need hardware evidence |
| Android native | GLES3 portable validation and Vulkan where devices support it; IL2CPP | Do not assume Vulkan is faster. Record Adreno/Mali coverage and device-specific driver results |
| Desktop optional | Windows D3D11 smoke/performance test through the same implementation | No separate PC shader tree. macOS Metal and other APIs only enter support claims after tests |

Target Unity `6000.3.23f1` for the first verified baseline. Declare package minimum `6000.3`; if an API/fix needs a particular patch, state it using package metadata/release notes rather than claiming all 6.3 patches work. Pin exact URP in the host lockfile. Before each release, compile/test against the minimum supported combination and one explicitly chosen newer supported editor/URP combination. Revalidate source-breaking API changes without adding historical fallback branches. [U13]

### Optimization disposition

| Always in the migration | Conditional on measurement | Excluded from the baseline |
|---|---|---|
| Remove GrabPass; stable shared scene source; eliminate redundant copy-back | Reduced-resolution distortion field and single compositor at high density | Built-in/HDRP, legacy Execute/Compatibility backend |
| Reusable simulation/mesh storage; share materials; cache property IDs and friction data | Curve baking, texture atlasing, instanced batches, Jobs/Burst | Compute-only simulation, mandatory WebGPU, ECS/job architecture without evidence |
| Skip inactive effects, unused blur, unused depth/normals/motion inputs | Half precision per value on verified devices; compact supported RT formats | Blanket half conversion, forced format assumptions |
| Explicit asset inclusion and small tested shader variant set | Custom variant stripping if build reports show remaining waste | Force Always Included for all shaders, speculative stripping |
| Texture size/compression/readability audit by role and platform | ASTC/ETC2/browser-specific distribution variants after visual/size tests | Copy all demo/media/tooling into library |

Texture requirements differ: normal maps need correct decoding; masks/friction data need declared color-space semantics; normal/mask mipmaps depend on minification and sample usage; wrap mode and atlas padding affect edge refraction. Choose platform overrides from builds rather than copying old importer settings. Preserve the host scene's HDR/MSAA/lighting choices and optimize only resources owned by the effect. Unity recommends demand-driven intermediate textures and avoiding unused scene inputs. [U08]

## 7. Verification and provisional budgets

No target hardware/FPS was supplied at investigation time. These numbers make the future measurement concrete; they are proposed starting gates to revise once devices are named. They are not current capabilities, final promises or Unity recommendations.

| Profile | Reference internal camera size | Caps: drops / live trails / points per trail | Blur | Proposed incremental p95 cost |
|---|---|---|---|---|
| Low | 1280×720 | 48 / 8 / 32 | Off | GPU ≤2 ms; CPU ≤0.5 ms native, ≤1 ms Web |
| Balanced | 1280×720 | 128 / 16 / 64 | Optional half-resolution | GPU ≤3 ms; CPU ≤0.75 ms native, ≤1.5 ms Web |
| High | 1920×1080 | 256 / 32 / 96 | Bounded shared blur | GPU ≤4 ms; CPU ≤1 ms native, ≤2 ms Web |

The host chooses actual render size and 30/60 FPS. The effect's budget is incremental against the same scene with the feature disabled, not the entire 16.67/33.33 ms frame. Required feature families remain functional at Low with reduced density. Add a no-blur quality setting without forcing the host resolution or disabling its post-processing.

NFR-01: zero managed allocations per warmed steady-state frame from library simulation/render preparation; initialization, editor changes and explicit capacity changes are measured separately. NFR-02: zero rain draws/copies/blur allocations when visually inactive, even if a controller is enabled. NFR-03: bounded memory and stable material/mesh counts across repeated playback and reloads. NFR-04: draw count grows with compatible texture/order groups, not particle count; record batches and native passes without promising a universal one-draw effect.

Memory arithmetic: one RGBA8 full-resolution texture is 3.52 MiB at 720p and 7.91 MiB at 1080p; RGBA16F doubles those values. Two half-width/half-height RGBA8 blur buffers add 1.76 and 3.96 MiB respectively. These are texture payloads only; they exclude existing camera targets, resolves, alignment and allocator overhead. Track peak incremental GPU memory; preliminary ceilings are 16 MiB at 720p LDR and 32 MiB at 1080p LDR. HDR/MSAA gets a separately reported budget, not a silent quality downgrade. Do not add a distortion-field texture without accounting for its format/channel/blur requirements.

### Evidence required for release

| Area | Required cases |
|---|---|
| Installation | Empty URP consumer, no samples imported, normal player build; import Basic/gallery samples separately; no missing references or tool dependencies |
| Rendering | Opaque + transparent scene objects, overlapping drops, frozen/blood combinations, portrait/landscape, UV edges, RenderTexture output, preserved alpha where configured |
| URP configuration | Perspective/orthographic, HDR on/off, MSAA off/2x/4x, render scale 0.5/1, configured dynamic resolution, post-processing on/off; Universal Renderer Forward baseline, Forward+/Deferred integration where supported |
| Camera policy | One camera, independent cameras sharing profile, Base+Overlay stack, Scene View opt-in, unrelated/preview/reflection cameras excluded; simulation once per frame |
| State | Delay, one-shot, looping, zero intensity, graceful/immediate stop, restart during delay, zero/invalid ranges, capacity change, paused/scaled time, tab resume |
| Ownership | 100 Play/Stop/restart cycles, scene reload, component disable/destroy, feature reload, domain reload disabled, resize/orientation cycles; no accumulating resources |
| Visual comparison | Capture original master and new package at matched scene/camera settings; log original low-resolution demo overrides; include every migrated preset and explicit overlap/blur/timing differences |
| Performance | Warm-up; repeated captures of no effect, static/frozen, sparse rain, dense trails/friction, combined high load; p50/p95 CPU/GPU, allocations, passes, texture memory and player size |
| Hardware | Named iOS Metal, Android Adreno/Mali GLES3/Vulkan, desktop/mobile browsers, optional PC; sustained 10–15 minute run and background/resume |

Use small runnable Unity tests for nontrivial deterministic state, geometry and conversion behavior; add image fixtures only where they protect material rendering contracts. Do not add tests that merely assert the implementation's own constants. Unity Test Framework may be a host/test dependency; it must not become a runtime player dependency. Existing code has no such test suite, and none was executed in this research.

On platforms where GPU timing is unavailable, report that limitation and use supported browser/vendor tooling plus controlled frame-time comparisons; never invent GPU milliseconds from total FPS. A Windows Editor result does not validate iOS/Safari. Use Render Graph Viewer/Frame Debugger for resource topology, Unity Profiler for CPU/GC, and suitable device tools (for example Xcode on Metal) for hardware evidence.

## 8. Detailed phase design

These phases specify dependency order, implementation scope, deliverables and decision gates for planning. They are not a task checklist and do not authorize code changes during explore mode. P0/P1 are the most important uncertainty-reduction stages; a failed rendering spike changes downstream design before mass asset conversion.

### P0 — Baseline and compatibility contract

Input: current branch and pinned master revision, 18-prefab inventory, requested platforms. Establish original camera/preset captures and metadata separately from the URP workspace; avoid switching the working tree away from `URP6.3` or altering master. A separate checkout is optional later if Unity comparison requires it. Record supported feature families and known unsupported/legacy features, public fields/curves/shader mappings, and all serialized references.

Output: baseline capture set, migration matrix, explicit scope/API/device assumptions and agreed visual tolerances. Preserve original behavior evidence even when it contains defects. Exit: every original non-VR preset has a destination or explicit documented disposition; baseline revision and settings are reproducible. If the old project cannot render correctly on the current editor, record that limitation and use source/preset evidence; do not manufacture reference images or equate a broken baseline with intended visuals.

### P1 — URP 6.3 integration and rendering spike

Input: P0 scene/visual contract. Resolve editor-compatible URP 17.3 patch, establish host pipeline/renderer assets and quality/build profiles. Prototype one normal-map drop and one frozen/static overlay through Render Graph, including a transparent object behind the rain. Validate sampleable camera color, no framebuffer feedback, MSAA resolve, HDR/LDR, render scale, alpha and final-color propagation.

Compare native Full Screen Pass for the simple overlay with the necessary custom RendererFeature for batch geometry/camera control. Compare direct batch composition against a minimal field+composite experiment only if density/overlap costs justify it. Capture copy count, bandwidth-sensitive passes, shader compile and WebGL2/Android behavior. Probe Base+Overlay stack timing before settling routing. Output: compiled vertical slice, frame captures and a renderer decision record. Exit: portable refraction actually renders in a WebGL2 player and a native target, transparents included, no redundant copy-back, and the selected renderer's visual difference is explicit. Exact mask/compositor channel encoding must be proven before selecting that alternative.

The spike also establishes a coverage/blend contract: overlapping flat-normal quad regions must not erase previously drawn rain, and local coverage must not overwrite the host camera's alpha convention. A missing usable coverage channel in original textures changes the asset-conversion workload; record that before porting every preset.

### P2 — Package extraction and assembly boundaries

Input: P0 inventory; P1 determines final runtime rendering dependencies. Introduce package metadata, runtime/editor boundaries and opt-in sample/test structure. Move retained assets with GUIDs; classify textures/presets versus demo assets; isolate HP/audio/FPS and all tooling. Keep the host as a consumer. Select publisher ID/namespace before serialization conversion.

Output: installable package skeleton containing the working vertical slice, one Basic sample, clean compile without gallery/tooling. Exit: empty-project package install and no-sample player build succeeds; Runtime cannot reference Editor/Samples/host. Do not postpone this gate until the whole feature is rewritten. Native versus manual export workflow is fixed and its sample paths validated.

### P3 — Playback, settings and simple/static effects

Input: accepted P1 renderer and P2 boundaries. Introduce camera owner/profile settings and bounded state, retaining curves/intensity/spawn/delay/one-shot semantics. Port Simple/Static families, frozen masks, tint/relief/darkness and splash primitives to batched quad geometry. Correct frame-dependent motion with documented units and retuned migration defaults. Implement reusable resources and inactive fast path immediately.

Output: usable simple rain and frozen package API, migrated representative profiles, focused state/lifetime checks. Exit: Play/Stop/immediate-stop/restart and two-camera independence pass; no per-drop objects/materials or warmed GC; frozen/overlay-only avoids scene-color work when unneeded. A feature family cannot be replaced by a visually unrelated procedural noise approximation solely to reduce code.

### P4 — Flow, friction and ribbons

Input: P3 lifecycle/units and P0 friction/curve references. Port flow state without per-drop coroutines; implement bounded reusable ribbon geometry; bake/cache friction data and replace candidate allocations with direct selection. Explicitly handle viewport edges, UV conventions and maximum-score/tie semantics. Retain trail width/alpha/distortion curves and make trail lifetime independent of render count.

Output: all four families on one renderer and complete CPU update path. Exit: deterministic motion/geometry tests, comparable 30/60/120 Hz trajectories, cap/no-GC checks and dense trail benchmark. Allocation removal and renderer replacement address shared root causes, not isolated prefab symptoms. Only introduce Jobs/Burst if measured CPU cost still exceeds the named-device budget.

### P5 — Shader quality, textures and GPU optimization

Input: complete effects and measured P1/P4 workloads. Implement bounded shared blur where enabled; tune safe precision, normal decoding, edge sampling and texture imports. Evaluate batch grouping/atlases without breaking layer order. Remove legacy shader variants/resources and verify serialized build inclusion. Benchmark Low/Balanced/High and optional field composition against the same camera size/visual workload.

Output: quality presets, performance captures, memory/pass budget and documented visual tradeoffs. Exit: accepted overlap/blur/frozen/blood appearance, required supported formats/APIs, inactive cost gate and device budgets or an explicit unresolved hardware result. Report rejected optimizations with measurements; do not keep two production backends simply because both were prototyped.

### P6 — Asset migration, examples and legacy removal

Input: final settings/shader semantics from P3–P5. Convert all supported presets, scenes, public setting names and shader/material references. Provide an explicit migration command/report with dry-run, undo/backups, handling for unmapped data and no silent rewriting on import. Conversion can use legacy types during a staged migration, but the final Runtime package contains no old renderer. If an external migration tool is needed to read old types, isolate it as an opt-in Editor utility with instructions on installing alongside the old asset.

Replace demo resolution overrides and HP coupling, assemble Basic/gallery samples and functional smoke scene, remove old shader/Cg/VR/mobile-backend paths per the accepted scope. Retain notices and record every deliberate behavior/API break. Output: finished package assets, migration guide and usable samples. Exit: no broken GUID references/missing scripts, all supported original presets accounted for, no old rendering terms in executable Runtime code, and no examples/tool DLLs in the shipped dependency tree. Tooling may remain in the development host where required.

### P7 — Release qualification and version policy

Input: P6 distributable and P5 budgets. Execute §7's installation, visual, state, ownership, platform and sustained performance matrix; inspect exported package contents and repeat install without the repository caches. Validate minimum supported 6.3 patch plus a chosen newer Unity/URP combination, record browser/OS/API versions, compile stripping-sensitive player configurations and test ordinary non-development builds.

Output: versioned package, support/migration documentation, test/performance report and unresolved limitations if any. Exit: release claims correspond to actual runs; unavailable iOS/browser hardware is a release evidence gap, not a pass. Desktop support is included if the same backend passes its smoke test. Source master remains unchanged. Update project context/map/rules in the later owning workflows to describe the new structure.

Dependency summary: P0 precedes P1 and package inventory; P1 gates renderer architecture; P2+P1 enable P3; P3 precedes P4; P4/P1 feed P5; P0/P2/P3/P4/P5 converge on P6; P6+device evidence gate P7. Detailed coupling is in [DEPENDENCY-GRAPH.md](DEPENDENCY-GRAPH.md). Hardware access can be arranged in parallel with P0; no speculative feature implementation should precede the renderer gate.

## 9. Open questions and decision triggers

| Question | Working assumption | What changes if the answer differs |
|---|---|---|
| XR/VR? | Excluded from first release | Add stereo/eye rendering, descriptors, camera policies, actual headset validation; revisit P1/P6 |
| Actual world-space windows? | Camera image only | Separate surface/refraction/depth/occlusion design; cannot be claimed by this migration |
| Old C# API compatibility? | Major API with one-time conversion | Add thin deprecated facade and compile an old-API consumer; preserve spellings deliberately |
| Devices/browser/FPS? | Proposed §7 profiles | Rebudget textures, density, blur and API matrix using named hardware |
| Strict original overlap/blur? | Accept documented new stable-source semantics | Revisit ordered group composition cost before P3; avoid silently restoring per-drop grabs |
| Publisher/package name? | Unresolved | Lock namespace/assembly identities before conversion |
| Physics-like freezing expected? | Preserve mask-based Frozen | Treat physical growth as a separate feature with its own evidence and budget |

## 10. Verified external references

All pages below were retrieved from official Unity documentation on 2026-09-09. Versioned manuals can change with patch updates; verify precise API signatures against the package installed in P1. Recommendations above distinguish documented capabilities from this project's proposed choices. Several guessed URLs returned 404 and were not used as evidence.

| ID | Official reference | Relevance |
|---|---|---|
| U01 | [Unity 6.3 Render Graph introduction](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/render-graph-introduction.html) | Resource lifetimes, pruning, eligible native-pass merging |
| U02 | [Unity 6.3 blit guidance](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/customize/blit-overview.html) | AddBlitPass/Blitter; avoid legacy blit APIs |
| U03 | [Unity 6.3 Render Graph blit](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/render-graph-blit.html) | Forward destination as cameraColor instead of copy-back |
| U04 | [Unity 6.3 Render Graph texture access](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/render-graph-read-write-texture.html) | Explicit input/output texture dependency contract |
| U05 | [Unity 6.3 Full Screen Pass](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/renderer-features/renderer-feature-full-screen-pass.html) | Injection points and requested inputs; simple-overlay alternative |
| U06 | [Unity 6.3 package layout](https://docs.unity3d.com/6000.3/Documentation/Manual/cus-layout.html) | Runtime/Editor/test/sample boundaries |
| U07 | [Unity 6.3 package samples](https://docs.unity3d.com/6000.3/Documentation/Manual/cus-samples.html) | Native Create/Export Samples naming behavior |
| U08 | [Unity 6.3 URP performance settings](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/configure-for-better-performance.html) | Avoid unnecessary inputs/intermediate buffers; inspect actual passes |
| U09 | [Unity 6.3 shader SRP Batcher compatibility](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/shaders-in-universalrp-srp-batcher.html) | Material/engine CBUFFER layout |
| U10 | [Unity 6.3 16-bit shader precision](https://docs.unity3d.com/6000.3/Documentation/Manual/SL-Use16BitPrecisionInShaders.html) | Explicit precision, accuracy testing and suffix limitations |
| U11 | [Unity 6.3 WebGPU](https://docs.unity3d.com/6000.3/Documentation/Manual/WebGPU.html) | Experimental status; WebGL2 remains the baseline |
| U12 | [Unity 6.3 browser compatibility](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-browsercompatibility.html) | Desktop and mobile browser categories |
| U13 | [Unity 6.3 package manifest](https://docs.unity3d.com/6000.3/Documentation/Manual/upm-manifestPkg.html) | Version/minimum-editor/dependency metadata |
| U14 | [Unity 6.3 Render Graph optimization](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/render-graph-optimize.html) | Avoid redundant blits, inspect resource and pass topology |

## Sessions
<!-- aif:sessions:start -->
### 2026-09-09 19:55 Europe/Berlin — URP migration exploration

What changed: Created this six-file ultra research bundle from the current source/GUID inventory and verified Unity 6.3 documentation. Captured package and renderer proposals, eight migration phases, functional/visual contracts, optimization dispositions and provisional device budgets.

Key notes: No Unity runtime/shader validation or hardware profiling was performed. Scope/API/device questions remain explicit working assumptions. Only this bundle was written; current branch and pinned master reference were preserved.

Validation: Research Coherence Gate passed a fresh-context read-only review after correcting two summary gaps: the overlay-only no-capture path and the P1 prerequisite before mass conversion. Bundle Integrity Gate passed: exact markers, existing relative artifact links, optional-artifact rationale, no orphan files, no implementation task checklists and no writes outside this bundle. UTF-8 text, fenced diagrams and local links passed structural checks.

Links (paths): [INDEX.md](INDEX.md), [ADR-0001-urp-package-boundary.md](ADR-0001-urp-package-boundary.md), [ADR-0002-portable-rain-rendering.md](ADR-0002-portable-rain-rendering.md), [C4-COMPONENT-rendering.md](C4-COMPONENT-rendering.md), [DEPENDENCY-GRAPH.md](DEPENDENCY-GRAPH.md).
<!-- aif:sessions:end -->
