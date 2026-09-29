# Project Base Rules

> Derived from the package as it stands after the 2.x rewrite. Follow local conventions; these rules
> do not request a refactor.

## Naming and Serialization

- C# filenames match their type. Types, public methods and public fields use PascalCase; private
  fields use camelCase. Serialized public fields are the authored surface and are PascalCase by
  convention (`MaxCount`, `LifetimeRange`, `TrailWidth`).
- Everything in the package lives in the `RainDropEffect` namespace, editor code in
  `RainDropEffect.Editor`, tests in `RainDropEffect.Tests`. Follow the touched file's namespace
  rather than moving existing types.
- Preserve public serialized field names and existing API spellings, including `StopImmidiate` and
  `StopRainImmidiate`. Any rename needs an explicit compatibility strategy — consumer profiles and
  scenes are bound to them.
- Preserve asset `.meta` files and GUIDs. Use Unity-aware asset operations when moving or renaming
  assets. Textures are byte-identical to 1.x with their GUIDs carried over; keep them that way.
- Shaders are resolved through the `RainShaderSet` asset, not by `Shader.Find`. Preserve the shader
  resource names it points at.
- Do not add version-conditional compilation. Decision D3 sets the minimum at `6000.3` and forbids
  `#if` between editor versions; when an API is obsolete-as-error in a newer editor, pick one that
  needs no branch.

## Module Structure

- The product is `Packages/com.virtualmaestro.raindropeffect/`. Everything under `Assets/` is host
  scaffolding for benchmarking and validation and is not shipped.
- `Runtime/` is split by technical concern: `Rendering/`, `Geometry/`, `Simulation/`, `Settings/`,
  `Profiles/`, `Data/`, `Shaders/`, `Textures/`. A layer family (Static, Simple, Flow, Friction)
  appears as one settings type in `Settings/` and one runtime in `Simulation/`; keep those two in
  step and keep family-specific behaviour inside them.
- Editor-only code lives in `Editor/` and must never be referenced from `Runtime/`. Runtime code must
  not reference `UnityEditor` or the samples.
- Samples live in `Samples~/`, which keeps its tilde so the Asset Database ignores it. A sample must
  not reference an asset belonging to another sample — Package Manager copies each one
  independently, and a cross-sample reference imports broken.
- Reuse `RainBatchMesh`, `TrailBuffer`, `EmitterState`, `RainRandom` and `RainQualityCaps` for shared
  responsibilities instead of duplicating them per family.
- Treat `Library/`, `Temp/`, `Logs/`, `Build/`, `Builds/`, `obj/` and the generated solution/project
  files as generated output, not authoritative source.

## Error Handling and Control Flow

- Guard invalid state explicitly. Use `RainLog.Warn` / `RainLog.Error` for actionable diagnostics and
  `RainLog.WarnOnce` for anything a per-frame condition could otherwise spam.
- Prefer flat, readable control flow. Handle invalid inputs and irrelevant branches early with guard
  clauses and early returns/continues when that makes the main path clearer.
- The per-frame path must not allocate and must not throw. `RainBatchMesh` sets `Overflowed` and
  returns a scratch vertex rather than throwing; keep that shape, and put the throwing argument
  checks in constructors and bake-time code.
- A runtime owns GPU resources, so everything constructed must be disposed. Settings objects are
  read-only at runtime: several cameras can share one profile, and a layer that wrote into its
  settings would leak state between them.

## Logging

- Use `RainLog`, not `Debug` directly, in runtime code. `RainLog.Verbose` is compiled out unless the
  host defines `RAINDROP_VERBOSE_LOG`.
- Nothing may log per frame (decision D17). Log at construction, bake or state-transition time only.
  Editor-time bake output may use `Debug.Log` so it shows up without the define.

## Verification

- Both suites run from batch mode:
  `Unity -batchmode -projectPath <repo> -runTests -testPlatform EditMode|PlayMode -testResults <xml> -logFile <log>`.
  PlayMode needs a real graphics device — under `-nographics` the rendering tests fail with
  `RenderTexture.Create failed`.
- Start the editor through `System.Diagnostics.Process`, never PowerShell's `Start-Process`:
  `-Wait` also waits on processes Unity spawned that outlive it, so the call never returns, and
  `-PassThru` without `-Wait` never populates `ExitCode`. Copy `Invoke-Unity` out of
  `tools/clean-install.ps1`.
- Read compile errors by grepping the run's `-logFile` for `error CS`, not through the MCP console.
- For nontrivial logic changes leave one runnable regression check that fails for the reported bug.
  Prefer a statistical or band assertion over path equality where the code consumes RNG per frame —
  a different `dt` means a different draw count and the paths cannot coincide at any tolerance.
- `tools/clean-install.ps1` is the release gate: it builds the package in a throwaway consumer
  project and checks that nothing editor-only, MCP or NuGet leaked into the player.
- Scope Git operations to the task and preserve unrelated staged and working-tree edits. Run separate
  Git operations as separate commands.
