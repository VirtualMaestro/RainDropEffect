# AGENTS.md

> Maintain this map when project structure changes. Detailed context lives in `.ai-factory/`.

## Project Overview

Rain Drop Effect is a Unity package that draws camera-space rain, frozen, blood and splash lens
effects through a URP Render Graph pass. `Packages/com.virtualmaestro.raindropeffect/` is the product
and the only thing that ships; everything under `Assets/` is host scaffolding that exists to compile,
benchmark and validate it. The package began as a rewrite of the Built-in-pipeline asset
Rain Drop Effect 2; none of that code is left here, and the migration tooling and guides that carried
the port were removed once it was done.

See `.ai-factory/DESCRIPTION.md` for the stack and constraints, `.ai-factory/ARCHITECTURE.md` for the
boundaries, and `.ai-factory/plans/` for the completed plans (`URP6.3/` — the migration to 2.0.0;
`rain-visual-fidelity.md` — the 2.0.1 fidelity pass).

## Tech Stack

- Unity `6000.3.23f1` with URP `17.3.0` (Render Graph). Package minimum is `6000.3`; decision D3
  forbids version-conditional code, so one source tree must compile on `6000.3` and `6000.5` alike.
- C#: one MonoBehaviour (`RainEffect`), plain classes and structs elsewhere, `ScriptableObject` data.
  HLSL shaders bound through a `RainShaderSet` asset, never by `Shader.Find`.
- Unity Test Framework `1.6.0`: 103 EditMode + 49 PlayMode tests. PlayMode needs a real graphics
  device — under `-nographics` the rendering tests fail on `RenderTexture.Create failed`.
- Rider integration, NuGetForUnity and Unity MCP tooling. No application database or ORM.

## Project Structure

```text
Packages/
  com.virtualmaestro.raindropeffect/   # THE PRODUCT. Embedded URP rain package, 2.0.1
    Runtime/                # asmdef RainDropEffect.Runtime
      RainEffect.cs         #   camera component, public API, render data
      Rendering/            #   renderer feature, Render Graph pass, materials, shader ids
      Geometry/             #   vertex layout and the reusable batch mesh
      Shaders/              #   RainLens, RainBlur, RainCommon.hlsl, RainShaderSet asset
      Settings/             #   layer settings types, quality tiers and caps
      Profiles/             #   RainProfile plus 15 converted presets and 3 Bench_* profiles
      Simulation/           #   emitter clock, RNG, trail ring, one runtime per layer family
      Data/                 #   baked FrictionField asset and its type
      Textures/             #   21 shipped textures + 10 baked coverage masks
    Editor/                 # asmdef RainDropEffect.Editor
      Inspectors/           #   RainEffect and RainProfile inspectors, edit-mode preview
      Baking/               #   coverage mask and friction field bakers
      BuildValidation/      #   batch-mode builds, packing, reference and missing-script audits,
                            #   shader variant report
      Migration/            #   AssetMover: texture moves, roles, platform overrides
    Tests/                  # asmdefs RainDropEffect.Tests.{Editor,Runtime}
                            #   including the Release* PlayMode suites that execute the release matrix
    Samples~/               # Basic and EffectsGallery (hidden from the Asset Database)
    Documentation~/         # package documentation and the archived 1.x converter
  manifest.json             # Unity package dependencies and lock

Assets/                     # the host project: everything here is test and measurement scaffolding
  Host/                     # asmdefs RainDropEffect.Host{,.Editor}
    Benchmark.unity         #   generated benchmark scene
    Smoke.unity             #   one camera, one profile: the host's own smoke scene
    Demo.unity              #   generated: rain presets, frost and blood behind an IMGUI panel —
                            #   the only scene in this repository that shows the effects, because
                            #   Samples~ is invisible to the Asset Database
    Cottage.jpg             #   backdrop texture for Demo.unity (the sample keeps its own copy)
    BenchmarkDriver.cs      #   scenario sweep and sustained run, writes Logs/benchmark-*.json
    DemoSample.cs           #   the demo's IMGUI driver
    Editor/BenchmarkSetup.cs #  regenerates the profiles, the scenes and the players
    Editor/DemoSetup.cs     #   regenerates Demo.unity
  Settings/                 # URP pipeline asset and renderer (Rain-URP)
  Plugins/, Packages/       # NuGet tooling

tools/                      # clean-install.ps1, pack-and-install.ps1, run-benchmark.ps1
docs/                       # support matrix, migration matrix/report/guide, performance, release report
ProjectSettings/            # Unity editor and build settings
.ai-factory/                # project context, config, rules, plans, baseline captures
.codex/ .claude/            # agent skills and configuration
```

The package assemblies are ordinary auto-referenced asmdefs, so a consumer's `Assembly-CSharp` sees
`RainDropEffect.Runtime` without any setup — which is what lets the samples compile with no asmdef of
their own. `Assets/Host` still declares one, because it is a separate assembly by choice rather than
by necessity.

## Key Entry Points

| File | Purpose |
| --- | --- |
| `Packages/com.virtualmaestro.raindropeffect/Runtime/RainEffect.cs` | The public API: profile, playback, tick cadence, render data |
| `Packages/com.virtualmaestro.raindropeffect/Runtime/Rendering/RainRendererFeature.cs` | URP integration; enqueues the single rain pass |
| `Packages/com.virtualmaestro.raindropeffect/Runtime/Rendering/RainRenderPass.cs` | Pass topology: one copy, optional half-res blur, draw into the attachment |
| `Packages/com.virtualmaestro.raindropeffect/Runtime/Profiles/RainProfile.cs` | Authored data and the layer ordering rule |
| `Packages/com.virtualmaestro.raindropeffect/Runtime/Simulation/LayerRuntimeFactory.cs` | Where a settings type becomes a running simulation |
| `Packages/com.virtualmaestro.raindropeffect/Runtime/Shaders/RainLens.shader` | Lens and overlay passes; the refraction, relief and blur formulas |
| `Packages/com.virtualmaestro.raindropeffect/Tests/Runtime/ReleaseRenderingTests.cs` | The release matrix: rendering, URP configuration and camera policy |
| `Assets/Host/Editor/BenchmarkSetup.cs` | Regenerates the benchmark profiles, the host scenes and the measurement players |
| `Assets/Host/BenchmarkDriver.cs` | Scenario sweep and sustained run; writes `Logs/benchmark-*.json` |
| `Assets/Host/Editor/DemoSetup.cs` | Regenerates `Demo.unity`: the scene to open when you want to look at the effects |
| `tools/clean-install.ps1` | Builds the package in a throwaway consumer project; the REQ-01 gate |
| `tools/pack-and-install.ps1` | Exports the tarball, checks its contents, installs it in a clean project |
| `tools/run-benchmark.ps1` | Builds and runs the benchmark player, or the sustained run |
| `Packages/manifest.json` | Unity package dependencies |
| `ProjectSettings/ProjectVersion.txt` | Required editor version |

## Documentation and AI Context Files

| File | Purpose |
| --- | --- |
| `README.md` | Repository landing page: what this is, what it is for, where to start |
| `AGENTS.md` | Project map and agent routing rules |
| `.ai-factory/config.yaml` | Language, paths, workflow, and Git defaults |
| `.ai-factory/DESCRIPTION.md` | Stack, features, and maintenance constraints |
| `.ai-factory/ARCHITECTURE.md` | Actual module boundaries and dependency rules |
| `.ai-factory/rules/base.md` | Naming, serialization, error handling, and verification conventions |
| `.codex/config.toml` | Codex agent settings and existing Unity MCP connection |
| `.mcp.json` | MCP configuration for runtimes consuming this format |
| `docs/support-matrix.md` | Verified editor/URP/platform combinations, clean-install results, named gaps |
| `docs/performance.md` | Render-target budget, blur kernel and tuning, measured CPU/GPU budgets |
| `docs/release-report.md` | The executed release matrix: installation, rendering, state, ownership, devices |
| `docs/renderer-decision.md` | Rendering spike evidence: pass topology, injection point, copy strategy |
| `Packages/com.virtualmaestro.raindropeffect/README.md` | Package landing page: install, quick start, support, performance |
| `Packages/com.virtualmaestro.raindropeffect/CHANGELOG.md` | Per-version changes; 2.0.2 is the distribution-weight release |
| `Packages/com.virtualmaestro.raindropeffect/Documentation~/index.md` | Consumer documentation: setup, API, profiles, quality tiers |
| `.ai-factory/plans/URP6.3/index.md` | The completed migration plan to 2.0.0 and its task checkboxes |
| `.ai-factory/plans/rain-visual-fidelity.md` | The completed 2.0.1 visual-fidelity plan |
| `.ai-factory/plans/URP6.3/PROGRESS.md` | Session handoff: state, decisions taken, blockers, environment notes |

## Agent Rules

- Read `.ai-factory/rules/base.md` before implementation. Preserve existing staged and working-tree changes.
- Run independent Git operations separately: first `git checkout master`, then `git pull origin master`, when those operations are requested. Do not combine them into one shell command.
- Preserve Unity `.meta` GUIDs, serialized names, and shader lookup names. Keep UnityEditor code in `Editor/`.
- Prefer codebase-memory-mcp for code discovery: `search_graph`, `trace_path`, `get_code_snippet`, `query_graph`, and `get_architecture`. Index the repository first if missing. Fall back to targeted text search for literals/configuration or insufficient graph results.
- Route gathering through `ctx_batch_execute`, follow-up questions through batched `ctx_search`, and analysis through `ctx_execute` or `ctx_execute_file`. Program filtering, counting, parsing, and comparisons; print only derived answers. Use robust JavaScript with Node built-ins, try/catch, and null checks.
- Read files directly when editing requires their exact content. Keep large search results and exploratory file contents in context-mode. Shell output outside context-mode must stay short.
- Never use curl/wget, inline HTTP shell calls, or direct URL-fetch tools. Use `ctx_fetch_and_index` followed by `ctx_search`, or HTTP inside `ctx_execute` with bounded output.
- Sandbox CWD differs from the repository. Use absolute, quoted paths; Git Bash paths use `/c/...`, never `/mnt/c/...`. Wrap PowerShell cmdlets in `pwsh -NoProfile -Command` when called through Bash.
- Keep responses under 500 words and write artifacts to files. Label indexed sources descriptively.
- On resume, use `ctx_search` with timeline sorting for prior decisions. Context-mode knowledge persists after compaction.
- For `ctx stats`, show the stats tool output verbatim. For `ctx doctor` or `ctx upgrade`, run the returned command and report a checklist. For `ctx purge`, warn that deletion is irreversible before calling purge with `confirm: true`.
- Writing C# or PowerShell through a Bash heredoc mangles escapes in this environment. Use the file-write tool, or a Python heredoc, for anything containing backslashes.
