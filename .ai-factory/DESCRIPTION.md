# Rain Drop Effect URP

## Overview

`com.virtualmaestro.raindropeffect` is a Unity package that draws camera-space rain, frozen, blood
and splash lens effects through a URP Render Graph pass. It began as a rewrite of the Built-in
pipeline asset Rain Drop Effect 2; that tree, and the migration tooling and guides that carried the
port, were removed once it was done. Nothing of the original code is left here.

The repository is a **package repository**, not an application. The product is
`Packages/com.virtualmaestro.raindropeffect/`; everything under `Assets/` is host-project scaffolding
that exists to compile, benchmark and validate that package.

## Detected Stack

- **Editor:** Unity `6000.3.23f1` (`ProjectSettings/ProjectVersion.txt`). The package declares
  `"unity": "6000.3"` as its minimum; decision D3 forbids version-conditional code, so a single
  source tree must compile on `6000.3` and `6000.5` alike.
- **Rendering:** Universal Render Pipeline `17.3.0` with Render Graph. One
  `ScriptableRendererFeature` enqueues one pass at `BeforeRenderingPostProcessing`. Pipeline assets
  live at `Assets/Settings/Rain-URP.asset` and `Rain-URP-Renderer.asset`.
- **Runtime language:** C#. `RainEffect` is the only MonoBehaviour; everything else is plain classes,
  structs and `ScriptableObject` data.
- **Shaders:** HLSL for URP — `RainLens.shader`, `RainBlur.shader`, shared `RainCommon.hlsl`, bound
  through a `RainShaderSet` asset rather than by `Shader.Find`.
- **Assemblies:** four asmdefs in the package (`RainDropEffect.Runtime`, `.Editor`, `.Tests.Editor`,
  `.Tests.Runtime`) and two in the host (`RainDropEffect.Host`, `.Host.Editor`). The package
  assemblies are auto-referenced, so a consumer's `Assembly-CSharp` sees the runtime with no setup —
  which is what lets the samples compile without an asmdef of their own.
- **Tests:** Unity Test Framework `1.6.0`. 103 EditMode + 49 PlayMode tests.
- **Tooling:** Rider integration `3.1.0`, NuGetForUnity, `com.ivanmurzak.unity.mcp` `0.90.0`.
- **Storage:** Unity assets and project settings. No application database or ORM.
- **Git:** GitHub remote; `origin/HEAD` identifies `master` as the base branch. Work happens on
  branch `URP6.3`.

## Core Features

- **Profiles instead of prefabs.** A `RainProfile` asset holds four arrays of layer settings — static
  overlays, droplets, flowing trails and friction-guided trails — ordered by
  `(Depth asc, FamilyOrder asc, index asc)`. A profile is immutable at runtime, so several cameras can
  share one.
- **Four simulation families.** Static (a full-screen overlay), Simple (drops that appear, drift and
  fade), Flow (drops that leave a ribbon and wander sideways) and Friction (trails that steer by a
  baked `FrictionField`).
- **One draw path.** Every layer writes into a single reusable `RainBatchMesh` with UInt16 indices;
  steady state allocates nothing per frame.
- **Explicit coverage.** Every drop texture pair ships with a baked coverage mask (decision D9):
  URP blends all layers into one attachment against the same pre-rain `_RainSceneColor` snapshot, so
  a runtime-inferred coverage would erase rain drawn underneath.
- **Quality tiers.** `RainQualityCaps` is the only place a tier becomes a number — trail counts,
  point counts, blur radius and whether blur runs at all.
- **15 converted presets** plus three `Bench_*` profiles, and an archived converter that turns a 1.x
  prefab into a profile.

## Repository Layout

```text
Packages/com.virtualmaestro.raindropeffect/   THE PRODUCT
  Runtime/     RainEffect, Rendering, Geometry, Simulation, Settings, Profiles, Data, Shaders, Textures
  Editor/      Inspectors, Baking, BuildValidation, Migration
  Tests/       Editor and Runtime suites
  Samples~/    Basic and EffectsGallery (hidden from the Asset Database by the tilde)
  Documentation~/  package docs plus the archived 1.x converter
Assets/        host scaffolding: benchmark, smoke and demo scenes, the measurement driver, URP assets
tools/         clean-install, pack-and-install, run-benchmark (PowerShell)
docs/          support matrix, migration matrix/report/guide, performance, release report
```

See `AGENTS.md` for the annotated tree and the entry-point table.

## Maintenance Constraints

- **Preserve `.meta` GUIDs and serialized field names.** Consumers have profiles and scenes bound to
  them. Existing spellings stay as they are; a rename needs an explicit compatibility strategy.
- **No version-conditional code (D3).** The minimum is `6000.3`; if an API is obsolete-as-error in a
  later editor, pick an API that needs no branch rather than adding `#if`.
- **Shader lookup names are a contract.** Shaders are bound through `RainShaderSet`, and its entries
  must keep resolving.
- **Editor code stays in `Editor/`.** Runtime code must not reference `UnityEditor` or the samples.
- **Nothing logs per frame (D17).** `RainLog.Verbose` compiles out unless `RAINDROP_VERBOSE_LOG` is
  defined, and is called at construction or bake time only.
- **Coverage is baked, never inferred at runtime (D9).** Any change to the coverage bake has to be
  re-validated on a real frame with stacked layers.
- **`Samples~` must keep its tilde**, and a sample must not reference an asset in another sample —
  Package Manager copies each one independently.
- **Textures are byte-identical to 1.x with GUIDs preserved.** Keep them that way.
- **Generated output is not source:** `Library/`, `Temp/`, `Logs/`, `Build*/`, `obj/` and the
  generated `.csproj`/`.sln` files.

## Verification

- EditMode and PlayMode suites both run from batch mode. PlayMode needs a real graphics device —
  under `-nographics` the rendering tests fail on `RenderTexture.Create failed`.
- `tools/clean-install.ps1` is the release gate: it builds the package in a throwaway consumer
  project whose only dependencies are URP and this package, then checks that nothing editor-only,
  MCP or NuGet leaked into the player.
- Start the editor through `System.Diagnostics.Process`, never PowerShell's `Start-Process -Wait` —
  it also waits on processes Unity spawned that outlive the editor, so the call never returns.
- Read compile errors by grepping the run's `-logFile` for `error CS`.

## Agent Tooling

Skills are installed under `.claude/skills/` (the AI Factory command set plus the generated Unity MCP
skill wrappers). `.mcp.json` configures `ai-game-developer` (the Unity editor bridge), `github`,
`filesystem` and `chromeDevtools`. `.codex/config.toml` carries the same Unity connection for Codex.
No additional external skills or MCP servers were needed.

## Architecture

Pattern: **Structured Modules (Technical Layer)**.
See `.ai-factory/ARCHITECTURE.md` for the folder structure, dependency rules and boundaries.
