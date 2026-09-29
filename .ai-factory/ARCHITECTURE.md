# Architecture: Structured Modules (Technical Layer)

## Overview

The product is a single Unity package with one feature: draw rain into a camera. There is no second
feature module to isolate it from, so the module boundary that matters here is the **assembly**
(`asmdef`), and the structure inside the runtime assembly is organised by technical concern —
rendering, geometry, simulation, settings, data — rather than by rain family. That is Structured
Modules in its Technical Layer variant, expressed in Unity package conventions.

The four rain families (Static, Simple, Flow, Friction) are not folders. Each is exactly one pair:
a settings type in `Settings/` and a runtime in `Simulation/`, joined by `LayerRuntimeFactory`. That
pairing is the extension point; adding a family means adding one file on each side and one case to
the factory, and touching nothing else.

There is no domain layer in the DDD sense and none is wanted. The "domain" is a screen full of
water, and its rules live in the simulation code and the shaders.

## Decision Rationale

- **Project type:** a Unity package (a library), not an application. No HTTP, no database, no ORM.
- **Tech stack:** Unity `6000.3` with URP `17.3.0` Render Graph; C# with four assemblies.
- **Key factor:** the assembly graph already enforces the only boundary that has consequences —
  editor code must not reach the player. Everything else is a naming convention, and a technical
  split reads better than four near-empty family folders would.
- **Superseded:** the previous document described the 1.x asset (`RainCameraController`,
  `RainBehaviourBase`, one folder per rain family under `Assets/RainDropEffect2/`). That tree was
  deleted in the URP rewrite.

## Folder Structure

```text
Packages/com.virtualmaestro.raindropeffect/
├── Runtime/                          # ── asmdef RainDropEffect.Runtime (auto-referenced) ──
│   ├── RainEffect.cs                 #    The only MonoBehaviour: public API, tick cadence,
│   │                                 #    the camera registry, and RainRenderData
│   ├── RainLog.cs                    #    Logging. Verbose compiles out without RAINDROP_VERBOSE_LOG
│   ├── Rendering/                    #    URP adapter + GPU resource helpers
│   │   ├── RainRendererFeature.cs    #      ScriptableRendererFeature; enqueues the pass
│   │   ├── RainRenderPass.cs         #      Pass topology: copy, optional half-res blur, draw
│   │   ├── RainMaterials.cs          #      Material factory (used at construction, not per frame)
│   │   └── RainShaderIds.cs          #      Cached Shader.PropertyToID handles
│   ├── Geometry/                     #    The single CPU-side vertex buffer
│   │   ├── RainBatchMesh.cs          #      Reusable dynamic mesh; never allocates in steady state
│   │   └── RainVertex.cs             #      Vertex layout
│   ├── Simulation/                   #    One runtime per family + the shared machinery
│   │   ├── RainRuntimeState.cs       #      Owns the ordered layer runtimes for one effect
│   │   ├── LayerRuntime.cs           #      Base: mesh, material, lifecycle
│   │   ├── LayerRuntimeFactory.cs    #      Where a settings type becomes a running simulation
│   │   ├── StaticLayerRuntime.cs     #      ┐
│   │   ├── SimpleLayerRuntime.cs     #      │ the four families
│   │   ├── FlowLayerRuntime.cs       #      │
│   │   ├── FrictionLayerRuntime.cs   #      ┘
│   │   ├── TrailBuffer.cs            #      Shared ring buffer + ribbon emit (Flow and Friction)
│   │   ├── EmitterState.cs           #      Shared emission clock
│   │   ├── RainRandom.cs             #      Xorshift32; deterministic under a seed
│   │   └── RainTickContext.cs        #      Per-frame inputs handed to every runtime
│   ├── Settings/                     #    The authored surface: one type per family + tiers
│   │   ├── RainLayerSettings.cs      #      Base, plus EmitterLayerSettings for emitting families
│   │   ├── {Static,Simple,Flow,Friction}LayerSettings.cs
│   │   └── RainQuality.cs / RainQualityCaps.cs
│   ├── Profiles/                     #    RainProfile.cs + 15 presets + 3 Bench_* profiles
│   ├── Data/                         #    FrictionField (baked scalar grid) and its asset
│   ├── Shaders/                      #    RainLens, RainBlur, RainCommon.hlsl, RainShaderSet
│   └── Textures/                     #    21 shipped textures + 10 baked coverage masks
├── Editor/                           # ── asmdef RainDropEffect.Editor ──
│   ├── Inspectors/                   #    RainEffect and RainProfile inspectors, edit-mode preview
│   ├── Baking/                       #    CoverageMaskBaker, FrictionFieldBaker
│   ├── BuildValidation/              #    Batch builds, packing, reference and shader-variant audits
│   └── Migration/                    #    AssetMover
├── Tests/                            # ── asmdefs RainDropEffect.Tests.{Editor,Runtime} ──
├── Samples~/                         #    Basic, EffectsGallery — tilde hides them from the AssetDB
└── Documentation~/                   #    Package docs + the archived 1.x converter

Assets/                               # Host scaffolding — NOT shipped
├── Host/                             #    asmdefs RainDropEffect.Host{,.Editor}
└── Settings/                         #    Rain-URP pipeline and renderer assets
```

## Dependency Rules

The assembly graph is the enforced part; the folder rules below are conventions the reviewer
enforces.

- ✅ `Editor` → `Runtime`. The editor assembly may use anything in the runtime, including `internal`
  members (`InternalsVisibleTo` in `Runtime/AssemblyInfo.cs`).
- ❌ `Runtime` → `Editor`, and `Runtime` → `UnityEditor`. A runtime file that needs an editor-only
  API is a bug the clean-install gate will catch, not a thing to `#if UNITY_EDITOR` around.
- ❌ `Runtime` or `Editor` → `Samples~`. Samples are consumers.
- ✅ `Tests.Editor` and `Tests.Runtime` → `Runtime` (+ `Editor`), including `internal` members.
- ❌ Anything → `Assets/Host`. The host depends on the package, never the reverse.

Inside `Runtime/`:

- ✅ `Rendering/RainRenderPass` → `RainEffect` (it looks effects up in the registry and reads their
  `RainRenderData`). The arrow points that way and only that way: `RainEffect` must not know the
  pass exists.
- ✅ `Simulation/` → `Geometry/`, `Settings/`, `Data/`, `Shaders/`, and — at construction only —
  `Rendering/RainMaterials`. `Rendering/` therefore holds two different things: the URP adapter,
  which is outer, and the material/shader-id helpers, which are shared services. Keep it that way
  rather than duplicating material creation per family.
- ❌ `Settings/`, `Profiles/`, `Data/` → `Simulation/` or `Rendering/`. Authored data knows nothing
  about what runs it. A profile is immutable at runtime; several cameras share one, and a runtime
  that wrote into its settings would leak state between them.
- ❌ Sibling family runtimes → each other. `FlowLayerRuntime` and `FrictionLayerRuntime` share
  `TrailBuffer`, `EmitterState` and `RainRandom`; they do not call into one another.
- ✅ Anything → `RainLog`.

## Layer Communication

- **The camera to the simulation:** `RainEffect.Update` builds a `RainTickContext` (dt, gravity,
  wind, aspect, intensity, quality) and hands it to `RainRuntimeState.Tick`. That is the only place
  the context is built.
- **The simulation to the renderer:** `LateUpdate` calls `Emit`, which fills each layer's
  `RainBatchMesh` and publishes a `RainRenderData` — a flat list of (mesh, material, property block)
  entries plus flags. The render pass reads that list and nothing else.
- **The renderer to the GPU:** `RainRenderPass` copies scene colour once per camera, optionally
  blurs it at half resolution, and draws the layers into the camera attachment. Textures are bound
  through `RainShaderIds`, never by string.
- **Ordering:** `RainProfile.CollectOrdered` sorts layers by `(Depth asc, FamilyOrder asc, array
  index asc)`, stably. That order is the draw order.
- **Adding a family:** one `*LayerSettings` in `Settings/`, one `*LayerRuntime` in `Simulation/`, one
  case in `LayerRuntimeFactory`, one `FamilyOrder` value. Nothing in `Rendering/` changes.

## Key Principles

1. **The assembly boundary is the real boundary.** Folders are for reading; asmdefs are for
   correctness. When in doubt about where something goes, ask which assembly it must not leak into.
2. **Settings are read-only at runtime.** Profiles are shared across cameras. Every piece of mutable
   state belongs to a runtime instance.
3. **Nothing allocates or logs per frame.** The steady-state tick and emit path allocates nothing;
   `RainLog.Verbose` is compiled out unless the host opts in, and is called at construction or bake
   time only (decision D17).
4. **The GPU contract is data, not strings.** Shaders come from a `RainShaderSet` asset and
   properties from cached ids. Coverage comes from a baked mask, never inferred at runtime
   (decision D9).
5. **One editor version, no branches.** The minimum is `6000.3` and version-conditional compilation
   is forbidden (decision D3); when an API differs across editors, choose one that needs no branch.
6. **Determinism is a feature.** `RainRandom` is seeded, so a profile reproduces frame for frame —
   which is what the migration comparison and the regression tests rest on.

## Code Organization Note

- **New features:** follow the structure above — a technical folder in `Runtime/`, the matching
  settings/runtime pair for a new family, editor-only code in `Editor/`.
- **Existing code:** this document describes the package as it stands. When modifying it, prefer
  these conventions, but do not rewrite unrelated code for structural alignment. Existing global-ish
  API spellings (`StopImmidiate`) and serialized field names are compatibility constraints, not
  cleanup opportunities.

## Code Examples

### Consumer entry point

The whole public surface is the component:

```csharp
var effect = camera.gameObject.AddComponent<RainEffect>();
effect.Profile = rainProfile;   // a RainProfile asset
effect.Intensity = 0.8f;
effect.Play();
// Later: let what is on screen finish its life.
effect.Stop();
```

### The settings-to-runtime pairing

A family exists as a settings type plus a runtime, joined in one place:

```csharp
// Runtime/Simulation/LayerRuntimeFactory.cs
public static LayerRuntime Create(RainLayerSettings settings, RainShaderSet shaders, RainQuality quality)
{
    switch (settings)
    {
        case StaticLayerSettings staticSettings:
            return CreateStatic(staticSettings, shaders, quality);

        case SimpleLayerSettings simple:
            return CreateSimple(simple, shaders, quality);

        case FlowLayerSettings flow:
            return CreateFlow(flow, shaders, quality);

        case FrictionLayerSettings friction:
            return CreateFriction(friction, shaders, quality);

        default:
            // A new settings type without a runtime is a programming error, not user input.
            throw new System.NotSupportedException(settings.GetType().Name);
    }
}
```

The same file owns the mesh budget. Every runtime passes `LayerRuntimeFactory.StaticCapacity()`,
`DropCapacity(...)` or `TrailCapacity(...)` straight into its base constructor, and the tier clamps
(`DropCount`, `TrailCount`, `TrailPoints`) live there too — so the arithmetic that decides how large
a mesh must be exists exactly once. That matters because getting it wrong is silent:
`RainBatchMesh.AddVertex` sets `Overflowed` and returns a scratch vertex rather than throwing.

### The dependency rule that matters most

A runtime reads its settings and writes only its own state — never back into the asset:

```csharp
// ✅ read authored data, keep the mutable copy per trail
t.Lifetime = rng.Range(s.LifetimeRange.x, s.LifetimeRange.y);

// ❌ never: s.LifetimeRange = ...;  // several cameras share this profile
```

## Anti-Patterns

- ❌ **Writing into a settings object from a runtime.** It silently couples every camera that shares
  the profile.
- ❌ **`#if UNITY_EDITOR` in `Runtime/`** to reach an editor API. Move the code to `Editor/` instead.
- ❌ **`Shader.Find` or a literal property name.** Use `RainShaderSet` and `RainShaderIds`.
- ❌ **Allocating or logging in `Tick` / `Emit`.** Both run per layer per frame.
- ❌ **A folder per rain family.** It was the 1.x shape and it duplicated the emission clock, the
  trail ring and the RNG four times. The shared machinery in `Simulation/` exists because of that.
- ❌ **A sample referencing another sample's assets.** Package Manager copies each sample on its own;
  the reference imports broken for anyone who installs only one.


