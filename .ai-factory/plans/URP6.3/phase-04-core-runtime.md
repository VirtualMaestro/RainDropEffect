# Phase 4: Playback, Settings, Simple and Static Layers

Plan: [index.md](index.md)
Tasks: 12-18
Depends on: Phase 2 / Tasks 5-7 (pass, shaders, mesh, accepted renderer decision), Phase 3 / Task 9 (textures in package)

`<pkg>` means `Packages/com.virtualmaestro.raindropeffect`.

## Objective

Replace the legacy camera/behaviour/controller/drawer object graph with one camera component, immutable profile assets, per-camera bounded state, and batched quad geometry for the Static and Simple families (frozen, blood frame, splash droplets, static rain frame all use these two). Playback semantics (delay, one-shot, graceful stop, immediate clear, restart) and the inactive fast path are implemented here and reused unchanged by Phase 5.

## Current-Code Evidence

| Path | Symbols / lines | Why it matters |
|------|-----------------|----------------|
| `Assets/RainDropEffect2/Scripts/RainBehaviours/SimpleRain/SimpleRainController.cs` | `PlayDelay` L85-114 (delay accumulates `Time.deltaTime`; re-entry guard; `PlayOnce` sets `oneShotTimeleft = Duration`); `CheckSpawnTime` L188-206 (`interval = Duration / Random(EmissionRateMin, EmissionRateMax)`, catch-up `spawnNum = min(timeElapsed/interval, free)`); `Spawn` L209-220 (first `Disabled` slot; silent when full); `InitializeDrawer` L229-245; `UpdateShader` L248-274; `UpdateInstance` L281-296 | Emission, one-shot, delay and per-drop value formulas to preserve. |
| `Assets/RainDropEffect2/Scripts/RainBehaviours/SimpleRain/SimpleRainBehaviour.cs` | `InitParams` L217-232 (swap inverted min/max; clamp count ≥ 0); `Start` L171-177 (`AutoStart` only in Play Mode); `StopRain` sets `NoMoreRain`; `StopRainImmidiate` destroys controller | Input sanitization and stop semantics. |
| `Assets/RainDropEffect2/Scripts/RainBehaviours/StaticRain/StaticRainController.cs` | `UpdateInstance` L108-182: fade in/out by `±Time.deltaTime` bounded to `[0, fadeTime]`, hidden at 0; full-screen scale `orthSize/2`; sized quad position formula; `Darkness` not curve-scaled | Static layer semantics. |
| `Assets/RainDropEffect2/Scripts/Camera/RainCameraController.cs` | `Update` L149-192: children sorted by `Depth`; `Alpha`, `GlobalWind`, `GForceVector` pushed to children; `Awake` stops all; `Play/Stop/StopImmidiate` dispatch | Effect-level API and ordering rule replaced by `RainEffect` + profile order. |
| `Assets/RainDropEffect2/Scripts/Common/RainDropTools.cs` | `GetGForcedScreenMovement` L287-298 (gravity projected onto camera axes); `Random` wrappers over `UnityEngine.Random.Range` (int overload exclusive max) | Gravity-to-screen conversion and RNG range semantics. |
| `Assets/RainDropEffect2/Scripts/Common/RainDrawer.cs` | `Show` L81-166: hides when all of distortion/relief/overlay alpha/blur are 0 | Inactive fast path per drawer, now per layer/effect. |
| `<pkg>/Runtime/RainEffect.cs` (Task 5 shell), `Runtime/Rendering/RainRenderPass.cs`, `Runtime/Geometry/RainBatchMesh.cs`, `Runtime/Rendering/RainMaterials.cs`, `Runtime/Shaders/RainLens.shader` | registry, `RainRenderData`, `RainBatch`, `RainVertex` layout, `CreateLayerMaterial` | Integration points this phase fills. |

## Files to Change

| Path | Action | Required change |
|------|--------|-----------------|
| `<pkg>/Runtime/Settings/RainQuality.cs`, `RainLayerMode.cs`, `RainQualityCaps.cs` | create | Enums and quality caps (Task 12). |
| `<pkg>/Runtime/Settings/RainLayerSettings.cs`, `StaticLayerSettings.cs`, `EmitterLayerSettings.cs`, `SimpleLayerSettings.cs`, `FlowLayerSettings.cs`, `FrictionLayerSettings.cs`, `RainProfile.cs` | create | Serializable settings and the profile asset (Task 12). All families declared now so Phase 5 adds behaviour only. |
| `<pkg>/Runtime/RainEffect.cs` | modify | Full component: fields, API, lifecycle, tick, render data build (Task 13). |
| `<pkg>/Runtime/Simulation/RainRuntimeState.cs`, `LayerRuntime.cs`, `RainTickContext.cs`, `RainRandom.cs`, `EmitterState.cs` | create | Per-effect state, layer base, RNG, shared emission logic (Tasks 13, 14). |
| `<pkg>/Runtime/Simulation/SimpleLayerRuntime.cs` | create | Simple family (Task 14). |
| `<pkg>/Runtime/Simulation/StaticLayerRuntime.cs` | create | Static family (Task 15). |
| `<pkg>/Runtime/Simulation/FlowLayerRuntime.cs`, `FrictionLayerRuntime.cs` | create (stubs) | Compile-only stubs that report `Visible = false` and log once "not implemented until Phase 5" (Task 16). |
| `<pkg>/Editor/Inspectors/RainEffectEditor.cs`, `RainProfileEditor.cs`, `RainEditModePreview.cs` | create | Inspector, profile creation, edit-mode preview driver (Task 17). |
| `<pkg>/Samples~/Basic/BasicRainProfile.asset` | create | One static frame + one simple layer using package textures (Task 17). |
| `<pkg>/Tests/Editor/*`, `<pkg>/Tests/Runtime/*` | create | Tests listed in Task 18. |
| `Assets/Spike/RainSpikeDriver.cs` | delete | Replaced by real state (Task 16). `Assets/Spike/Spike.unity` stays until Task 29 as a host smoke scene using `BasicRainProfile`. |

## Task 12: Declare settings types, quality caps, and the profile asset

### Intent

Freeze the serialized data model (names used by the migration matrix, converter, inspector, and all simulations). Every family's fields are declared here so later phases never rename serialized data.

### Implementation Steps

1. `<pkg>/Runtime/Settings/RainQuality.cs`: `public enum RainQuality { Low = 0, Balanced = 1, High = 2 }`. `RainLayerMode.cs`: `public enum RainLayerMode { Lens = 0, Overlay = 1 }`.
2. `<pkg>/Runtime/Settings/RainQualityCaps.cs`:
   ```csharp
   public static class RainQualityCaps {
     public static int MaxDrops(RainQuality q)  => q == RainQuality.Low ? 48 : q == RainQuality.Balanced ? 128 : 256;
     public static int MaxTrails(RainQuality q) => q == RainQuality.Low ? 8  : q == RainQuality.Balanced ? 16  : 32;
     public static int MaxPoints(RainQuality q) => q == RainQuality.Low ? 32 : q == RainQuality.Balanced ? 64  : 96;
     public static bool BlurEnabled(RainQuality q) => q != RainQuality.Low;
     public static float BlurRadius(RainQuality q) => q == RainQuality.High ? 3f : 2f;   // half-resolution pixels
   }
   ```
3. `<pkg>/Runtime/Settings/RainLayerSettings.cs`:
   ```csharp
   [Serializable] public abstract class RainLayerSettings {
     public string Name = "Layer"; public bool Enabled = true; public int Depth = 0;
     public RainLayerMode Mode = RainLayerMode.Lens;
     public Texture2D NormalMap; public Texture2D OverlayTexture; public Texture2D CoverageMask;
     public Color OverlayColor = Color.gray;
     [Range(0, 200)] public float Distortion = 50f; [Range(0, 2)] public float Relief = 1.5f; [Range(0, 1)] public float Blur = 0f; [Range(0, 5)] public float Darkness = 0f;
     public bool AutoStart = true;
     public abstract int FamilyOrder { get; }   // Static 0, Simple 1, Flow 2, Friction 3 (tie-break after Depth)
   }
   ```
4. `StaticLayerSettings.cs`: `FullScreen = true`, `Vector2 Size = new(0.5f, 0.5f)`, `Vector2 Offset = Vector2.zero` (fractions, see matrix), `[Range(0,15)] float FadeTime = 2f`, `AnimationCurve FadeCurve = AnimationCurve.Linear(0,0,1,1)`; `FamilyOrder => 0`.
5. `EmitterLayerSettings.cs` (abstract): `bool PlayOnce = false; float Duration = 1f; float Delay = 0f; [Range(1,256)] int MaxCount = 30; Vector2 LifetimeRange = new(0.6f,1.4f); Vector2Int EmissionRateRange = new(2,5); [Range(-2,2)] float SpawnOffsetY = 0f; AnimationCurve AlphaOverLifetime, DistortionOverLifetime, ReliefOverLifetime, BlurOverLifetime` each defaulting to `AnimationCurve.Constant(0,1,1)`.
6. `SimpleLayerSettings.cs`: `bool AutoRotate; Vector2 SizeMin = new(0.15f,0.15f); Vector2 SizeMax = new(0.15f,0.15f); AnimationCurve SizeOverLifetime = Constant(0,1,1); AnimationCurve DriftOverLifetime = Constant(0,1,0); float DriftSpeed = 0.1252f;` `FamilyOrder => 1`.
7. `FlowLayerSettings.cs`: `Vector2 WidthRange = new(0.15f,0.15f); AnimationCurve TrailWidth = Constant(0,1,1); [Range(0.004f,0.2f)] float PointSpacing = 0.01f; [Range(4,96)] int MaxPoints = 64; float LateralAmplitude = 0.1f; [Range(0,10)] float LateralSmooth = 5f; Vector2 FluctuationRateRange = new(5f,5f); float InitialVelocity = 0f; Vector2 AccelerationRange = new(0.0125f, 0.0417f);` `FamilyOrder => 2`.
8. `FrictionLayerSettings.cs`: `Vector2 WidthRange; AnimationCurve TrailWidth; [Range(0.004f,0.2f)] float PointSpacing = 0.01f; [Range(4,96)] int MaxPoints = 64; FrictionField Field; float InitialVelocity = 0f; Vector2 AccelerationRange = new(0.0376f, 0.125f); [Range(30,600)] float ScanRate = 150f; [Range(2,8)] int LateralSamples = 5; float LateralStepFactor = 0.375f;` `FamilyOrder => 3`. Semantics: fall speed `= Accel·t² + InitialVelocity·t` (normalized units/s, `t` = trail age); defaults are legacy `0.06/0.2 × 3 × k` (matrix friction row). `FrictionField` is declared in this task as an empty `ScriptableObject` subclass (`<pkg>/Runtime/Data/FrictionField.cs` with `public int Width, Height; public byte[] Values;`) so the field serializes; Task 21 fills behaviour.
9. `RainProfile.cs`:
   ```csharp
   [CreateAssetMenu(menuName = "Rain Drop Effect/Rain Profile", fileName = "RainProfile")]
   public sealed class RainProfile : ScriptableObject {
     public int Version = 1;
     public StaticLayerSettings[] StaticLayers = System.Array.Empty<StaticLayerSettings>();
     public SimpleLayerSettings[] SimpleLayers = System.Array.Empty<SimpleLayerSettings>();
     public FlowLayerSettings[] FlowLayers = System.Array.Empty<FlowLayerSettings>();
     public FrictionLayerSettings[] FrictionLayers = System.Array.Empty<FrictionLayerSettings>();
     public int LayerCount => StaticLayers.Length + SimpleLayers.Length + FlowLayers.Length + FrictionLayers.Length;
     /// Fills 'into' with enabled layers sorted by (Depth asc, FamilyOrder asc, array index asc). Stable; no allocation beyond 'into' growth.
     public void CollectOrdered(List<RainLayerSettings> into) { into.Clear(); AddAll(into, StaticLayers); AddAll(into, SimpleLayers); AddAll(into, FlowLayers); AddAll(into, FrictionLayers); InsertionSort(into); }
     // InsertionSort by (Depth, FamilyOrder, original index) using a parallel int list of original indices; List.Sort is not stable.
     public void Sanitize() { /* per layer: swap inverted ranges, clamp MaxCount ≥ 1, Duration ≥ 0, Delay ≥ 0, LifetimeRange ≥ 0, EmissionRateRange ≥ 0, FadeTime ≥ 0, MaxPoints ≥ 4 */ }
     void OnValidate() => Sanitize();
   }
   ```
10. Add `[assembly: InternalsVisibleTo("RainDropEffect.Tests.Editor")]` and `("RainDropEffect.Tests.Runtime")` in `<pkg>/Runtime/AssemblyInfo.cs` (if not already added in Task 7).

### Required Interfaces and Contracts

- Field names above are final and identical to `docs/migration-matrix.md`; update the matrix if any name here differs.
- Profiles are immutable at runtime: no runtime code writes to settings objects (enforced by review; `Sanitize` runs only in `OnValidate`/editor).
- Ordering rule: `(Depth, FamilyOrder, index)` ascending; the first layer draws first (bottom).

### Error Handling and Logging

- `Sanitize` fixes silently (mirrors legacy `InitParams`), except `MaxCount > 256` which logs `RainLog.Warn($"{name}: MaxCount clamped to 256")` once per validate.

### Tests

- `<pkg>/Tests/Editor/RainProfileTests.cs`: `CollectOrderedSortsByDepthThenFamilyThenIndex` (two static with Depth 5 and 1, one simple Depth 1, one flow Depth 1 → expected order: static(1), simple(1), flow(1), static(5)); `CollectOrderedSkipsDisabled`; `SanitizeSwapsInvertedRanges` (`LifetimeRange = (2,1)` → `(1,2)`, `EmissionRateRange = (5,2)` → `(2,5)`, `SizeMin > SizeMax` swapped per component); `SanitizeClampsMaxCount` (`300` → `256`).

### Acceptance Criteria

- All settings classes compile and appear in the default inspector of a `RainProfile` asset with the declared defaults.
- Tests pass.

### Verification

- Create a profile via Assets → Create → Rain Drop Effect → Rain Profile; inspector shows four arrays; add one `SimpleLayers` element and confirm defaults (`MaxCount 30`, `LifetimeRange (0.6,1.4)`).
- EditMode test run → `RainProfileTests` passed.

## Task 13: Implement the RainEffect component, runtime state, and lifecycle

### Intent

One owner per camera for settings snapshot, seeded RNG, layer runtimes, meshes, materials, playback API, tick cadence, and render-data publication (D11–D13). This is the public API of the package.

### Implementation Steps

1. `<pkg>/Runtime/Simulation/RainRandom.cs`: `public struct RainRandom { uint s; public RainRandom(int seed) { s = seed == 0 ? 0x9E3779B9u : (uint)seed; if (s == 0) s = 1; } public uint NextUInt() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s; } public float NextFloat() => (NextUInt() & 0xFFFFFF) / 16777216f; public float Range(float min, float max) => min + (max - min) * NextFloat(); public int Range(int minInclusive, int maxExclusive) => maxExclusive <= minInclusive ? minInclusive : minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive)); }`.
2. `<pkg>/Runtime/Simulation/RainTickContext.cs`: `public struct RainTickContext { public float Dt; public Vector2 Down; public Vector2 Wind; public float Intensity; public float Aspect; public RainQuality Quality; }` (RNG is stored in `RainRuntimeState` and passed by `ref`).
3. `<pkg>/Runtime/Simulation/LayerRuntime.cs`:
   ```csharp
   internal abstract class LayerRuntime : IDisposable {
     public RainLayerSettings Settings { get; }
     public RainBatchMesh Mesh { get; private set; }
     public Material Material { get; private set; }
     public bool IsLens => Settings.Mode == RainLayerMode.Lens;
     public bool UsesBlur { get; private set; }
     public bool Visible { get; protected set; }         // has anything to draw this frame
     public abstract bool IsPlaying { get; }              // has active drops or is emitting/fading
     public abstract bool IsEmitting { get; }
     protected LayerRuntime(RainLayerSettings s, RainShaderSet shaders, RainQuality q, int maxVertices, int maxIndices) { Settings = s; Mesh = new RainBatchMesh(maxVertices, maxIndices, s.Name); UsesBlur = IsLens && s.Blur > 0f && RainQualityCaps.BlurEnabled(q); Material = RainMaterials.CreateLayerMaterial(shaders, s.NormalMap, s.OverlayTexture, s.CoverageMask, UsesBlur); }
     public abstract void Play(ref RainRandom rng); public abstract void Stop(); public abstract void Clear();
     public abstract void Tick(in RainTickContext ctx, ref RainRandom rng);
     public abstract void Emit(in RainTickContext ctx);    // writes Mesh via Begin/Add*/End; sets Visible
     public void Dispose() { Mesh?.Dispose(); Mesh = null; CoreUtils.Destroy(Material); Material = null; }
   }
   ```
4. `<pkg>/Runtime/Simulation/RainRuntimeState.cs`:
   ```csharp
   internal sealed class RainRuntimeState : IDisposable {
     public readonly List<LayerRuntime> Layers = new(8);
     public RainRandom Rng;
     readonly List<RainLayerSettings> ordered = new(8);
     public RainRuntimeState(RainProfile profile, RainShaderSet shaders, RainQuality quality, int seed) {
       Rng = new RainRandom(seed == 0 ? Environment.TickCount : seed);
       profile.CollectOrdered(ordered);
       foreach (var s in ordered) Layers.Add(LayerRuntimeFactory.Create(s, shaders, quality));   // switch on concrete type
     }
     public bool IsPlaying { get { foreach (var l in Layers) if (l.IsPlaying) return true; return false; } }
     public bool IsEmitting { ... }
     public void Play() { foreach (var l in Layers) l.Play(ref Rng); }
     public void Stop() { foreach (var l in Layers) l.Stop(); }
     public void Clear() { foreach (var l in Layers) l.Clear(); }
     public void PlayAutoStart() { foreach (var l in Layers) if (l.Settings.AutoStart) l.Play(ref Rng); }
     public void Tick(in RainTickContext ctx) { foreach (var l in Layers) l.Tick(ctx, ref Rng); }
     public void Emit(in RainTickContext ctx, RainRenderData into, RainQuality q) {
       into.Clear();
       foreach (var l in Layers) { l.Emit(ctx); if (!l.Visible || l.Mesh.IsEmpty) continue; into.Batches.Add(new RainBatch { Mesh = l.Mesh.Mesh, Material = l.Material, ShaderPass = l.IsLens ? RainShaderPass.Lens : RainShaderPass.Overlay, IsLens = l.IsLens }); into.AnyVisible = true; into.AnyLens |= l.IsLens; into.AnyBlur |= l.UsesBlur; }
       into.BlurRadius = RainQualityCaps.BlurRadius(q);
     }
     public void Dispose() { foreach (var l in Layers) l.Dispose(); Layers.Clear(); }
   }
   ```
   `LayerRuntimeFactory.Create` computes capacities: Static → (4, 6); Simple → `n = min(MaxCount, MaxDrops(q))` → (4n, 6n); Flow/Friction → `t = min(MaxCount, MaxTrails(q))`, `p = min(MaxPoints, MaxPoints(q))` → (2·t·p, 6·t·(p−1)).
5. Complete `<pkg>/Runtime/RainEffect.cs` (merge with the Task 5 shell; keep the registry):
   - Serialized: `public RainProfile Profile; [Range(0,1)] public float Intensity = 1f; public Vector2 Wind; public Vector3 Gravity = Vector3.down; public RainQuality Quality = RainQuality.High; public int Seed = 0; public bool PreviewInSceneView; public bool PreviewInEditMode; [SerializeField, HideInInspector] RainShaderSet shaderSet;`
   - Properties: `public bool IsPlaying => state != null && state.IsPlaying; public bool IsEmitting => state != null && state.IsEmitting; internal RainRuntimeState State => state;`
   - API: `public void Play()` (if `state == null` → `Rebuild()`; `state.Play()`; `RainLog.Verbose("Play", this)`), `public void Stop()` (graceful: `state?.Stop()`), `public void Clear()` (immediate: `state?.Clear(); RenderData.Clear();`), `public void Rebuild()` (dispose old state, create new from current `Profile/Quality/Seed`; if `Profile == null` → `state = null`, `RainLog.WarnOnce("noprofile:"+GetInstanceID(), $"RainEffect on '{name}' has no profile", this)`; if `shaderSet == null` → try `Resources`-free fallback: `RainLog.Error("RainEffect has no RainShaderSet (select the component in the editor to auto-assign)")` and `state = null`).
   - Lifecycle: `OnEnable` → cache camera, `Register()`, `Rebuild()`, `if (Application.isPlaying) state?.PlayAutoStart()`; `OnDisable` → `Unregister()`, `state?.Dispose(); state = null; RenderData.Clear();`; `OnDestroy` same as disable; `OnValidate` → `rebuildRequested = true` when `(Profile, Quality, Seed)` differ from the values captured at the last rebuild, `Intensity` clamped; `#if UNITY_EDITOR` auto-assign `shaderSet` from `Packages/com.virtualmaestro.raindropeffect/Runtime/Shaders/RainShaderSet.asset` in `Reset()` and in `OnValidate` when null.
   - Tick: `void Update()`: `if (!Application.isPlaying && !PreviewInEditMode) return; if (rebuildRequested) { rebuildRequested = false; bool wasPlaying = IsPlaying; Rebuild(); if (wasPlaying) state?.Play(); } if (state == null) return; float dt = Application.isPlaying ? Time.deltaTime : Mathf.Clamp(Time.realtimeSinceStartup - lastRealtime, 0f, 0.1f); lastRealtime = Time.realtimeSinceStartup; dt = Mathf.Min(dt, MaxDeltaTime /*0.1f const*/); var ctx = MakeContext(dt); state.Tick(ctx);`
   - `MakeContext(dt)`: `Down = ProjectGravity()`, `Wind = Wind`, `Intensity = Intensity`, `Aspect = cachedCamera.pixelWidth / (float)max(1, cachedCamera.pixelHeight)` (when `targetTexture != null` use its size; URP render scale does not change aspect), `Quality = Quality`.
   - `ProjectGravity()`: `Vector3 local = cachedCamera.transform.InverseTransformDirection(Gravity); Vector2 d = new(local.x, local.y); return d.sqrMagnitude < 1e-8f ? Vector2.down : d.normalized;` (equals legacy `GetGForcedScreenMovement(...).normalized.xy` for the camera transform).
   - `void LateUpdate()`: `if (state == null || (!Application.isPlaying && !PreviewInEditMode)) { RenderData.Clear(); return; } state.Emit(lastContext, RenderData, Quality);` where `lastContext` is the context built in `Update` (aspect may change between frames; recompute aspect here).
   - `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)] ResetStatics()` from Task 5 retained.
6. Multiple `RainEffect` on one camera are allowed (blood sample); document in the class summary.

### Required Interfaces and Contracts

- Public API (final): `Profile`, `Intensity`, `Wind`, `Gravity`, `Quality`, `Seed`, `PreviewInSceneView`, `PreviewInEditMode`, `IsPlaying`, `IsEmitting`, `Play()`, `Stop()`, `Clear()`, `Rebuild()`.
- Stop semantics (D12): `Stop()` stops emission and lets visible drops/trails/fades finish; `Clear()` removes all visible state immediately and cancels pending delays; `Play()` after `Stop()` restarts emission without re-spawning existing drops; `Play()` during a delay does not queue a second delay (`EmitterState` ignores `Play` while in `Delay`/`Emitting`, mirroring the legacy re-entry guard).
- Simulation ticks exactly once per `Update`; rendering may read `RenderData` for several cameras (Scene View preview shares it read-only).
- `MaxDeltaTime = 0.1 s` clamp is the bounded catch-up rule (D13).
- No managed allocation in `Update`/`LateUpdate` after the first frame (verified in Task 18).

### Error Handling and Logging

- Verbose: `enable/disable`, `Rebuild (layers=n, quality=q, seed=s)`, `Play/Stop/Clear`, `dt clamped from X to 0.1` (only when clamped and at most once per 5 s: keep `lastClampLogTime`).
- Warn once: no profile; profile with zero enabled layers (`"profile '{p.name}' has no enabled layers"`).
- Error once: missing shader set.
- Never log inside the per-layer tick.

### Tests

- Covered by Task 18 (`RainEffectLifecycleTests`, `RainEffectAllocationTests`, `TwoCamerasShareProfileTests`).

### Acceptance Criteria

- Adding `RainEffect` + a profile to a camera in Play Mode and calling `Play()` produces batches in `RenderData` (visible in Frame Debugger as `Rain Draw`) once Tasks 14–16 land.
- Disabling the component removes its registry entry and releases meshes/materials (Task 18 checks object counts).

### Verification

- Compile; Play Mode in `Assets/Spike/Spike.unity` after Task 16 shows rain; toggling the component off removes `Rain Draw` from Frame Debugger.

## Task 14: Implement shared emission state and the Simple layer

### Intent

Port the Simple family (transient droplets: rain drops, splash bubbles, blood splatter) to bounded value-type state with delta-time motion, reusing one `EmitterState` that Flow and Friction also use in Phase 5.

### Implementation Steps

1. `<pkg>/Runtime/Simulation/EmitterState.cs`:
   ```csharp
   internal struct EmitterState {
     public enum Phase { Idle, Delay, Emitting, Draining }
     public Phase Current; float delayLeft, oneShotLeft, elapsed, interval; bool noSpawnCycle;
     public bool IsEmitting => Current == Phase.Emitting;
     public void Play(EmitterLayerSettings s, ref RainRandom rng) { if (Current == Phase.Delay || Current == Phase.Emitting) return; if (s.Delay > 0f) { Current = Phase.Delay; delayLeft = s.Delay; } else BeginEmitting(s, ref rng); }
     public void Stop() { if (Current != Phase.Idle) Current = Phase.Draining; }
     public void Clear() { Current = Phase.Idle; delayLeft = oneShotLeft = elapsed = 0f; interval = 0f; }
     void BeginEmitting(EmitterLayerSettings s, ref RainRandom rng) { Current = Phase.Emitting; elapsed = 0f; oneShotLeft = s.PlayOnce ? Mathf.Max(0f, s.Duration) : float.PositiveInfinity; NextInterval(s, ref rng); }
     void NextInterval(EmitterLayerSettings s, ref RainRandom rng) { int rate = rng.Range(s.EmissionRateRange.x, s.EmissionRateRange.y); noSpawnCycle = rate <= 0 || s.Duration <= 0f; interval = noSpawnCycle ? 0f : s.Duration / rate; }
     /// Returns how many drops to spawn this tick (0 when not emitting). 'freeSlots' caps catch-up spawning.
     public int Tick(float dt, int freeSlots, EmitterLayerSettings s, ref RainRandom rng) {
       switch (Current) {
         case Phase.Delay: delayLeft -= dt; if (delayLeft > 0f) return 0; BeginEmitting(s, ref rng); return 0;
         case Phase.Emitting:
           if (s.PlayOnce) { oneShotLeft -= dt; if (oneShotLeft <= 0f) { Current = Phase.Draining; return 0; } }
           if (noSpawnCycle) return 0;
           elapsed += dt; if (elapsed < interval) return 0;
           int n = Mathf.Min((int)(elapsed / interval), freeSlots); NextInterval(s, ref rng); elapsed = 0f; return Mathf.Max(0, n);
         default: return 0;
       }
     }
   }
   ```
   `Draining` transitions to `Idle` when the owning layer has no active drops (the layer checks this in its `Tick`).
2. `<pkg>/Runtime/Simulation/SimpleLayerRuntime.cs`:
   ```csharp
   internal sealed class SimpleLayerRuntime : LayerRuntime {
     struct Drop { public bool Active; public float Age, Lifetime, Rotation; public Vector2 Pos, Size; }
     readonly Drop[] drops; readonly SimpleLayerSettings s; EmitterState emitter; int activeCount;
     public SimpleLayerRuntime(SimpleLayerSettings s, RainShaderSet sh, RainQuality q) : base(s, sh, q, 4 * Cap(s,q), 6 * Cap(s,q)) { this.s = s; drops = new Drop[Cap(s,q)]; }
     static int Cap(SimpleLayerSettings s, RainQuality q) => Mathf.Clamp(Mathf.Min(s.MaxCount, RainQualityCaps.MaxDrops(q)), 1, 256);
     public override bool IsPlaying => activeCount > 0 || emitter.Current == EmitterState.Phase.Delay || emitter.IsEmitting;
     public override bool IsEmitting => emitter.IsEmitting;
     public override void Play(ref RainRandom rng) => emitter.Play(s, ref rng);
     public override void Stop() => emitter.Stop();
     public override void Clear() { emitter.Clear(); for (int i = 0; i < drops.Length; i++) drops[i].Active = false; activeCount = 0; Visible = false; }
     public override void Tick(in RainTickContext ctx, ref RainRandom rng) {
       int spawn = emitter.Tick(ctx.Dt, drops.Length - activeCount, s, ref rng);
       for (int k = 0; k < spawn; k++) Spawn(ctx, ref rng);
       for (int i = 0; i < drops.Length; i++) { ref var d = ref drops[i]; if (!d.Active) continue; d.Age += ctx.Dt; float p = d.Lifetime > 0f ? d.Age / d.Lifetime : 1f; if (p >= 1f) { d.Active = false; activeCount--; continue; } d.Pos += -ctx.Down * (s.DriftSpeed * s.DriftOverLifetime.Evaluate(p) * ctx.Dt) + ctx.Wind * (p * ctx.Dt); }   // legacy: += (-gforced) * 0.01 * PosYOverLifetime; shipped curves are negative, so drops fall
       if (emitter.Current == EmitterState.Phase.Draining && activeCount == 0) emitter.Clear();
     }
     void Spawn(in RainTickContext ctx, ref RainRandom rng) { for (int i = 0; i < drops.Length; i++) { ref var d = ref drops[i]; if (d.Active) continue; d.Active = true; activeCount++; d.Age = 0f; d.Lifetime = rng.Range(s.LifetimeRange.x, s.LifetimeRange.y); d.Pos = new Vector2(rng.Range(-ctx.Aspect, ctx.Aspect), rng.Range(-1f, 1f) + 2f * s.SpawnOffsetY); d.Size = new Vector2(rng.Range(s.SizeMin.x, s.SizeMax.x), rng.Range(s.SizeMin.y, s.SizeMax.y)); d.Rotation = s.AutoRotate ? rng.Range(0f, 179.9f) * Mathf.Deg2Rad : 0f; return; } }
     public override void Emit(in RainTickContext ctx) {
       Mesh.Begin(); Visible = false;
       if (ctx.Intensity <= 0f || activeCount == 0) { Mesh.End(); return; }
       for (int i = 0; i < drops.Length; i++) { ref var d = ref drops[i]; if (!d.Active) continue; float p = d.Age / d.Lifetime; float a = s.AlphaOverLifetime.Evaluate(p) * ctx.Intensity; if (a <= 0f) continue;
         var tint = s.OverlayColor; tint.a *= a;
         var prm = new Vector4(s.Distortion * s.DistortionOverLifetime.Evaluate(p) * ctx.Intensity, s.Relief * s.ReliefOverLifetime.Evaluate(p) * ctx.Intensity, s.Blur * s.BlurOverLifetime.Evaluate(p) * ctx.Intensity, s.Darkness * ctx.Intensity);   // legacy: Darkness * Alpha
         RainBatchMesh.AddQuad(Mesh, d.Pos, d.Size * s.SizeOverLifetime.Evaluate(p), d.Rotation, a, tint, prm); Visible = true; }
       Mesh.End();
     }
   }
   ```
   Note: `AnimationCurve.Evaluate` does not allocate. `Color → Color32` conversion in `AddQuad` does not allocate.

### Required Interfaces and Contracts

- Drop slot order is draw order (stable; matches legacy render-queue-by-index behaviour closely enough under the stable-source rule).
- Spawn positions are uniform over the full view (`x ∈ [−aspect, aspect]`, `y ∈ [−1, 1] + 2·SpawnOffsetY`), matching `GetSpawnLocalPos`.
- Emission arithmetic reproduces legacy `CheckSpawnTime` including catch-up spawning and the exclusive-max integer rate roll.

### Error Handling and Logging

- Zero/negative rates, zero duration, zero lifetime, inverted ranges: handled arithmetically (`noSpawnCycle`, `p = 1` when lifetime ≤ 0, `Sanitize` swaps ranges); no logging in the tick.
- Capacity exhausted: spawn silently skipped (legacy behaviour).

### Tests

- Covered by Task 18 (`EmitterStateTests`, `SimpleLayerRuntimeTests`).

### Acceptance Criteria

- With `BasicRainProfile` (Task 17) in Play Mode, droplets spawn at the configured rate, fall along gravity, fade by curve, and stop cleanly with `Stop()`/`Clear()`.
- `Seed != 0` makes two runs identical (asserted in Task 18).

### Verification

- Play Mode in `Spike.unity` with the simple layer only: Frame Debugger shows one `Rain Draw` `DrawMesh` with vertex count = 4 × active drops.

## Task 15: Implement the Static layer

### Intent

Port full-screen/sized static overlays (rain frame, frozen fill/frame, blood frame) with the legacy fade-in/out semantics on a single quad.

### Implementation Steps

1. `<pkg>/Runtime/Simulation/StaticLayerRuntime.cs`:
   ```csharp
   internal sealed class StaticLayerRuntime : LayerRuntime {
     readonly StaticLayerSettings s; float fadeElapsed; bool playing;
     public StaticLayerRuntime(StaticLayerSettings s, RainShaderSet sh, RainQuality q) : base(s, sh, q, 4, 6) { this.s = s; }
     public override bool IsPlaying => playing || fadeElapsed > 0f;
     public override bool IsEmitting => playing;
     public override void Play(ref RainRandom rng) => playing = true;
     public override void Stop() => playing = false;
     public override void Clear() { playing = false; fadeElapsed = 0f; Visible = false; }
     public override void Tick(in RainTickContext ctx, ref RainRandom rng) { float ft = Mathf.Max(0f, s.FadeTime); fadeElapsed = playing ? Mathf.Min(ft, fadeElapsed + ctx.Dt) : Mathf.Max(0f, fadeElapsed - ctx.Dt); }
     public override void Emit(in RainTickContext ctx) {
       Mesh.Begin(); Visible = false;
       float ft = Mathf.Max(0f, s.FadeTime); float progress = ft > 0f ? fadeElapsed / ft : (playing ? 1f : 0f);
       float f = s.FadeCurve.Evaluate(progress) * ctx.Intensity;
       if (f <= 0f || (!playing && fadeElapsed <= 0f)) { Mesh.End(); return; }
       Vector2 center = s.FullScreen ? Vector2.zero : new Vector2(-2f * s.Offset.x * ctx.Aspect, -2f * s.Offset.y);
       Vector2 half = s.FullScreen ? new Vector2(ctx.Aspect, 1f) : s.Size;
       var tint = s.OverlayColor; tint.a *= f;
       RainBatchMesh.AddQuad(Mesh, center, half, 0f, f, tint, new Vector4(s.Distortion * f, s.Relief * f, s.Blur * f, s.Darkness));
       Visible = true; Mesh.End();
     }
   }
   ```
2. Legacy quirk preserved: `Darkness` is not scaled by the fade curve (only by tint alpha inside the shader).

### Required Interfaces and Contracts

- Full-screen quad UVs `(0,0)…(1,1)` map the overlay texture to the whole view (stretched, as legacy).
- Fade state survives `Stop()` (fades out) and is reset by `Clear()`.

### Error Handling and Logging

- `FadeTime = 0` means instant on/off; handled by the `progress` expression. No logging.

### Tests

- Covered by Task 18 (`StaticLayerRuntimeTests`).

### Acceptance Criteria

- Frozen-style profile (two static layers) fades in over `FadeTime`, holds, fades out on `Stop()`, disappears immediately on `Clear()`.

### Verification

- Play Mode with a profile containing one static layer (`frozenframe`): Frame Debugger shows one 4-vertex `DrawMesh`; after `Stop()` the draw disappears when the fade reaches 0.

## Task 16: Wire simulations into render data and implement the inactive fast path

### Intent

Replace the spike driver with real state, guarantee that inactive effects cost nothing on the GPU (NFR-02), and make the mesh overflow contract observable.

### Implementation Steps

1. Create compile-only stubs `<pkg>/Runtime/Simulation/FlowLayerRuntime.cs` and `FrictionLayerRuntime.cs`: constructor as per family, `Visible = false`, `IsPlaying = false`, `Tick/Emit` no-ops, and `RainLog.WarnOnce("stub:" + settings.Name, "Flow/Friction layers render nothing until Phase 5")` in the constructor. Add `<pkg>/Runtime/Simulation/LayerRuntimeFactory.cs` (`switch` on `StaticLayerSettings`, `SimpleLayerSettings`, `FlowLayerSettings`, `FrictionLayerSettings`; `default → throw new NotSupportedException(settings.GetType().Name)`).
2. Delete `Assets/Spike/RainSpikeDriver.cs`; in `Assets/Spike/Spike.unity` assign `BasicRainProfile` (Task 17) to the camera's `RainEffect`.
3. Fast path checks in `RainRenderPass.Collect` (already skips `!AnyVisible`) plus in `RainRendererFeature.AddRenderPasses`: enqueue only when at least one registered effect on this camera has `RenderData.AnyVisible` (add `internal static bool AnyVisibleFor(Camera cam)` in `RainEffect`). Result: when all layers are invisible (intensity 0, drained, cleared) no pass is enqueued, so no copy, no blur, no draw.
4. Overflow logging: in `RainRuntimeState.Emit`, after `l.Emit(ctx)`, `if (l.Mesh.Overflowed) RainLog.WarnOnce("overflow:" + l.Settings.Name, $"layer '{l.Settings.Name}' exceeded its mesh capacity; geometry truncated")`.
5. Material property refresh: when `Intensity` or per-frame values change nothing needs a material update (all per-vertex). Texture changes in a profile require `Rebuild()` (documented in the inspector help box, Task 17).

### Required Interfaces and Contracts

- `RainEffect.AnyVisibleFor(Camera)` is the single gate the feature consults; Scene View uses `SceneViewPreviewSource?.RenderData.AnyVisible`.
- No GPU work for an effect with `Intensity == 0` or no active layers: Frame Debugger shows no Rain passes.

### Error Handling and Logging

- Overflow warning once per layer name (above). Factory `NotSupportedException` is a programming error surfaced at `Rebuild`.

### Tests

- Covered by Task 18 (`InactiveEffectEnqueuesNoPass` in `RainRenderPassTests`).

### Acceptance Criteria

- `Spike.unity` renders the Basic profile with real simulation; setting `Intensity = 0` removes all Rain passes from the Frame Debugger; restoring intensity brings them back without a rebuild.

### Verification

- Frame Debugger: with `Intensity = 0` no `Rain Copy Scene Color`/`Rain Draw` entries; with `Intensity = 1` both present.

## Task 17: Editor inspector, profile creation, and edit-mode preview

### Intent

Make the component usable without reading code: playback buttons, renderer-feature diagnostics, shader-set auto-assignment, profile creation, and an optional edit-mode preview loop.

### Implementation Steps

1. `<pkg>/Editor/Inspectors/RainEffectEditor.cs` (`[CustomEditor(typeof(RainEffect))]`):
   - Draw default inspector, then a `HelpBox` when: no profile; the active `UniversalRenderPipelineAsset` (`GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset`) has no `RainRendererFeature` in its default renderer (`rendererDataList[defaultRendererIndex].rendererFeatures.OfType<RainRendererFeature>().Any()`; obtain via `SerializedObject(pipelineAsset).FindProperty("m_RendererDataList")` to avoid internal API) with a button `Add Rain Renderer Feature` that creates the feature via `ScriptableObject.CreateInstance<RainRendererFeature>()`, `AssetDatabase.AddObjectToAsset(feature, rendererData)`, appends it to `m_RendererFeatures` and `m_RendererFeatureMap` (name hash) serialized properties of the renderer data, `ApplyModifiedProperties`, `AssetDatabase.SaveAssets()`; the renderer's Intermediate Texture is `Auto` and post-processing is disabled on the camera (warn: "may render to backbuffer; enable post-processing or set Intermediate Texture = Always" per Task 7 M5 result).
   - Buttons `Play`, `Stop`, `Clear`, `Rebuild` (enabled in Play Mode or when `PreviewInEditMode`); status line `Playing: {IsPlaying}  Emitting: {IsEmitting}  Layers: {State?.Layers.Count ?? 0}`.
   - Help text: "Changing textures or layer counts in the profile requires Rebuild".
2. `<pkg>/Editor/Inspectors/RainEditModePreview.cs` (`[InitializeOnLoad]`): subscribes `EditorApplication.update`; if `!Application.isPlaying` and any enabled `RainEffect` (tracked via a static `HashSet<RainEffect>` registered in `OnEnable`/`OnDisable` under `#if UNITY_EDITOR`) has `PreviewInEditMode`, call `EditorApplication.QueuePlayerLoopUpdate()` and `UnityEditorInternal.InternalEditorUtility.RepaintAllViews()` at most every 16 ms (`EditorApplication.timeSinceStartup` throttle).
3. `<pkg>/Editor/Inspectors/RainProfileEditor.cs`: custom editor drawing the four arrays with a foldout per family and a `Sanitize` button; plus `MenuItem("Assets/Create/Rain Drop Effect/Rain Profile (Basic Rain)")` that creates a profile with one static layer (`rain_frame`/`rain_frame_normal`/`rain_frame_coverage`, `Distortion 20`, `Relief 1`, `FadeTime 2`) and one simple layer (`rain_drop` textures, `MaxCount 20`, `Distortion 50`, `Relief 1.5`, `SizeMin/Max (0.15,0.15)`, `AlphaOverLifetime` = ease-out curve `(0,1)→(1,0)`).
4. Create `<pkg>/Samples~/Basic/BasicRainProfile.asset` with that menu (through the `Samples` rename workflow from Task 10) and assign it in `Basic.unity`; remove the TODO from `Samples~/Basic/README.md`.
5. `RainEffect.Reset()` auto-assigns `shaderSet` (Task 13) and logs verbose `shader set assigned`.

### Required Interfaces and Contracts

- Editor code lives only in `RainDropEffect.Editor`; the runtime keeps the `#if UNITY_EDITOR` auto-assign only.
- The "Add Rain Renderer Feature" button writes to the host's renderer asset only when the user clicks it.

### Error Handling and Logging

- Feature-add failure (serialized property missing because URP changed field names): `RainLog.Error("could not add renderer feature automatically; add 'Rain Renderer Feature' in the Universal Renderer asset")`.

### Tests

- `<pkg>/Tests/Editor/RainEffectEditorTests.cs`: `ShaderSetAutoAssignedOnReset` (create a GameObject with camera, `AddComponent<RainEffect>()`, call `Reset` through `Editor.CreateEditor(...)`'s `serializedObject.FindProperty("shaderSet")` after `EditorUtility.SetDirty`; simpler: expose `internal void EditorAutoAssignShaderSet()` and assert non-null). `BasicProfileMenuCreatesTwoLayers` (invoke the creation method with a temp path under `Assets/Temp/`, assert 1 static + 1 simple, delete the asset).

### Acceptance Criteria

- Selecting a fresh `RainEffect` shows the feature-missing warning when the feature is absent; the button adds it; the warning disappears.
- `PreviewInEditMode` animates rain in the Game view without entering Play Mode.
- `BasicRainProfile.asset` exists and renders in `Basic.unity`.

### Verification

- Manual: remove the feature from `Rain-URP-Renderer.asset`, select the camera, click the button, confirm the feature is back.
- EditMode test run → `RainEffectEditorTests` passed.

## Task 18: Tests for state, lifecycle, allocations, and camera independence

### Intent

Lock the playback contract and the no-GC/no-leak guarantees before Phase 5 adds the heavier families.

### Implementation Steps

1. `<pkg>/Tests/Editor/RainRandomTests.cs`: `SameSeedSameSequence`, `RangeIntExclusiveMax` (10 000 draws of `Range(2,5)` never return 5, all of 2,3,4 appear), `RangeIntDegenerateReturnsMin` (`Range(5,5) == 5`), `SeedZeroIsNotZeroState`.
2. `<pkg>/Tests/Editor/EmitterStateTests.cs` (pure struct tests with a fixed `RainRandom(42)` and a settings object):
   - `DelayThenEmitting` (`Delay 0.5`, tick 0.25 → 0 spawns and `Phase.Delay`; tick 0.3 → `Phase.Emitting`).
   - `PlayDuringDelayDoesNotRestart` (second `Play` during delay leaves `delayLeft` unchanged: tick to just before expiry, `Play`, tick small → emitting only once the original delay expires).
   - `OneShotDrainsAfterDuration` (`PlayOnce`, `Duration 1`, ticks totalling 1.05 → `Draining`).
   - `SpawnCountMatchesRate` (`Duration 1`, `EmissionRateRange (4,5)` → interval 0.25; tick 1.0 in 0.1 steps → total spawns 3 or 4 (rounding); tick 0.6 once → catch-up 2).
   - `ZeroRateNeverSpawns` (`EmissionRateRange (0,0)` → 0 spawns over 10 s).
   - `ZeroDurationNeverSpawns`.
   - `FreeSlotsCapSpawns` (`freeSlots 1` with catch-up 3 → 1).
   - `StopThenPlayResumes` (`Stop` → `Draining`; `Play` → `Emitting` immediately when `Delay 0`).
3. `<pkg>/Tests/Editor/SimpleLayerRuntimeTests.cs` (construct `SimpleLayerRuntime` with a `RainShaderSet` loaded from the package asset):
   - `SpawnsUpToCapacityAndNoMore` (`MaxCount 5`, rate high → `activeCount ≤ 5` for 100 ticks; expose `internal int ActiveCount`).
   - `DropsExpireByLifetime` (`LifetimeRange (0.5,0.5)`; after 0.6 s of ticks with emission stopped, `ActiveCount == 0`).
   - `ClearRemovesEverything` and `StopDrainsThenIdle` (after drain, `IsPlaying == false`).
   - `DeterministicWithSeed` (two runtimes with identical settings and `RainRandom(7)`: after 60 ticks all drop positions equal).
   - `MotionFollowsGravityAndDeltaTime` (`DriftSpeed 1`, `DriftOverLifetime = Constant(0,1,-1)` (negative like the shipped curves), `Down = (0,-1)`: after ticks totalling 0.5 s the drop's y decreased by 0.5 ± 1e-4 regardless of step size 1/30 vs 1/120; compare two runtimes). Add `PositiveCurveDriftsAgainstGravity` (`Constant 1` → y increases), pinning the legacy sign.
   - `EmitProducesFourVerticesPerActiveDrop` (`Mesh.VertexCount == 4 * ActiveCount`).
   - `IntensityZeroEmitsNothing`.
4. `<pkg>/Tests/Editor/StaticLayerRuntimeTests.cs`: `FadesInOverFadeTime` (progress reaches 1 after `FadeTime`), `StopFadesOutThenInvisible`, `ClearIsImmediate`, `FadeTimeZeroIsInstant`, `SizedQuadCenterFormula` (`Offset (0.25, −0.5)`, aspect 2 → vertex centroid `(−1.0, 1.0)`).
5. `<pkg>/Tests/Runtime/RainEffectLifecycleTests.cs` (PlayMode, `[UnityTest]`):
   - `PlayStopClearCyclesDoNotLeak`: camera + `RainEffect` with `BasicRainProfile`; record `Resources.FindObjectsOfTypeAll<Mesh>().Length` and `<Material>` counts after first `Rebuild`; run 100 cycles of `Play()`, 3 frames, `Stop()`, 3 frames, `Clear()`; assert counts unchanged (±0).
   - `DisableReleasesResources`: after `enabled = false`, mesh/material counts return to the pre-enable values.
   - `RebuildReplacesResourcesWithoutGrowth`: 20 × `Rebuild()`; counts equal to after the first rebuild.
   - `AutoStartPlaysOnEnableInPlayMode`: profile layer `AutoStart = true` → `IsPlaying` after one frame.
6. `<pkg>/Tests/Runtime/RainEffectAllocationTests.cs` (PlayMode): warm up 10 frames, then `Assert.That(() => effect.SendMessage("Update"), Is.Not.AllocatingGCMemory())` is not reliable through `SendMessage`; instead expose `internal void TickForTest()` (calls the same code as `Update`) and `internal void EmitForTest()`; assert `Is.Not.AllocatingGCMemory()` (`UnityEngine.TestTools.Constraints`) for both with the Basic profile and with a profile of 256 simple drops.
7. `<pkg>/Tests/Runtime/TwoCamerasShareProfileTests.cs`: two cameras with `RainEffect` referencing the same profile asset, seeds 1 and 2; `Play()` on A only → after 10 frames `A.IsPlaying && !B.IsPlaying`; `B.Play()`; `A.Clear()` → `!A.IsPlaying && B.IsPlaying`; profile asset fields unchanged (compare `MaxCount`, `Distortion`).
8. Extend `RainRenderPassTests` (Task 7) with `InactiveEffectEnqueuesNoPass`: `Intensity = 0` → render to a RenderTexture through `RenderPipeline.SubmitRenderRequest` (Task 7's helper) → pixel equals the no-effect render exactly.
9. Run EditMode and PlayMode suites through Unity MCP `tests-run` (or CLI); fix failures before marking the phase done.

### Required Interfaces and Contracts

- Internal test hooks: `RainEffect.TickForTest()`, `EmitForTest()`; `SimpleLayerRuntime.ActiveCount`; `LayerRuntime.Mesh` internal.
- Tests never touch host scenes; they create their own objects and destroy them in `[TearDown]`.

### Error Handling and Logging

- Test logs are not gated; `LogAssert.NoUnexpectedReceived()` is used in lifecycle tests to assert no warnings/errors were logged during 100 cycles.

### Tests

This task is the test set. Commands:
- EditMode: `"C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\workspace\projects\unity\RainDropEffect" -runTests -testPlatform EditMode -testResults Logs\editmode.xml -logFile Logs\editmode.log`
- PlayMode: same with `-testPlatform PlayMode -testResults Logs\playmode.xml`.

### Acceptance Criteria

- All tests in this phase pass on Windows editor.
- Allocation tests pass for Basic and 256-drop profiles.
- Lifecycle test reports zero mesh/material growth across 100 cycles.

### Verification

- `grep -c 'result="Failed"' Logs/editmode.xml Logs/playmode.xml` → `0` for both.
- `grep -o 'total="[0-9]*"' Logs/editmode.xml | head -1` → ≥ 30 tests.

## Phase Risks and Mitigations

- Risk: `AnimationCurve.Evaluate` or `Color`→`Color32` conversions allocate in some Unity versions.
  Mitigation: allocation tests catch it; fallback is caching curve samples in a 64-entry float array per curve at rebuild.
- Risk: legacy per-frame drift/wind conversions look different from the baseline.
  Mitigation: constants are profile fields (`DriftSpeed`, `Wind`) tunable during Task 27 visual acceptance; matrix records the conversion.
- Risk: edit-mode preview loop keeps the editor busy.
  Mitigation: preview is opt-in per component and throttled to 60 Hz.

## Phase Completion Checklist

- Every Task N in this phase satisfies its acceptance criteria.
- Required verification commands pass.
- `index.md` task checkboxes are updated immediately after verified completion.
