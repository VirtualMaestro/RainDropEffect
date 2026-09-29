# URP6.3 — Implementation Progress and Session Handoff

Updated by `/aif-implement` on 2026-09-10. Read this together with `index.md` (the plan and its task
checkboxes) before continuing. `index.md` stays the source of truth for progress; this file carries
the context that is not in the plan.

## State

All eight phases are complete: **32 of 32 tasks** are checked in `index.md`, `v2.0.0` is tagged, and
everything is committed on branch `URP6.3` (nothing is pushed).

| Task | Deliverable | Where |
| --- | --- | --- |
| 1-23 | See the git history; the summary table lived here until the tasks were all green. | — |
| 24 | Texture platform overrides, reference audit, shader variant report, **and the player run** | `Editor/Migration/AssetMover.cs`, `Editor/BuildValidation/`, `Logs/shader-variants.json` |
| 25 | Three `Bench_*` profiles, generated benchmark scene, measured Windows and WebGL2 budgets | `Runtime/Profiles/Bench_*`, `Assets/Host/`, `docs/performance.md` |
| 26 | Legacy prefab converter, window and dry-run tests | archived at `Documentation~/legacy-converter/` |
| 27 | Materials upgraded, 15 profiles, 13 camera prefabs, 39 captures, report | `Runtime/Profiles/`, `Samples~/EffectsGallery/Prefabs/`, `docs/migration-report.md` |
| 28 | Basic and Effects Gallery samples: three scenes, six sample scripts, demo media moved | `Samples~/Basic`, `Samples~/EffectsGallery` |
| 29 | Legacy runtime, demos, baseline harness and spike deleted; host smoke scene; migration guide | `Assets/` now holds only `Host`, `Settings`, `Plugins`, `Packages` |
| 30 | 25 matrix rows executed as PlayMode tests; installation and audit rows | `Tests/Runtime/Release*Tests.cs`, `docs/release-report.md` |
| 31 | Web rows measured; Android and iOS recorded as gaps | `docs/release-report.md` §Web, §Android, §iOS |
| 32 | 2.0.0 metadata, tarball verified in a clean project, 6000.5.10f1 validated, docs rewritten | `package.json`, `CHANGELOG.md`, `tools/pack-and-install.ps1`, tag `v2.0.0` |

Tests: **EditMode 98/98, PlayMode 49/49** (`Logs/editmode.xml`, `Logs/playmode.xml`, copied to
`docs/evidence/`). EditMode dropped from 107 when Task 29 deleted the nine legacy-converter tests.

## Decisions and findings from this session

1. **`Start-Process -Wait` is unusable for driving Unity.** It waits for every process Unity spawned,
   and some of them (shader compilers, the licensing client) outlive the editor, so the call never
   returns. `-PassThru` without `-Wait` returns a `Process` whose `ExitCode` is never populated.
   Every script here starts the editor through `System.Diagnostics.Process` instead. This is what
   had blocked Task 24 step 3 — it was never a memory problem.
2. **A hand-written consumer manifest gets no built-in modules.** The clean-install project needed
   `com.unity.modules.screencapture` (and `imgui`) declared explicitly, or the generated validation
   scene could not compile. The rain package itself still declares nothing but URP.
3. **`RAINDROP_VERBOSE_LOG` is no longer defined in the host's Standalone defines.** With it on, the
   test runner fails any test whose playback writes an unexpected log line, and `LogAssert.
   ignoreFailingMessages` logs a line of its own when set. The define stays documented as opt-in.
4. **A package folder without an asmdef is never compiled**, so the sample scenes cannot be authored
   inside `Samples~`. They are authored in the imported copy under `Assets/Samples/` and copied back
   with their `.meta` files, which keeps every GUID.
5. **WebGL frame timing is bounded by the display refresh.** Every scenario reads 17.0 ms p50 and
   p95; that means no frame ran long, not that the measurement failed. GPU milliseconds are simply
   unavailable in a browser. A background browser tab is `requestAnimationFrame`-throttled, so the
   measurement needs a foreground window.
6. **Finding F2 is implemented but not executed.** `BaselineDriver.DisablePrefabCameras` and the menu
   `RainDrop/Baseline/Capture BloodRain F2 Reference` re-capture the 1.x BloodRain reference through
   the scene camera. It needs an interactive editor (ScreenCapture at a 1280×720 Game view) and the
   legacy content, which Task 29 deleted. `docs/migration-report.md` §F2 has the restore command.

Earlier decisions (Q8 pass topology, the blur being a kernel width in 1.x, friction tie-breaking,
`SerializedProperty.objectReferenceValue` not sticking) are recorded in `docs/renderer-decision.md`,
`docs/migration-report.md` and the code comments that implement them.

## Remaining work

Nothing in the plan. All 32 tasks are checked, `v2.0.0` is tagged, and `master` still points at
`901824a2`. What is *not* done is named rather than implied:

- **The gaps in `docs/release-report.md` §Gaps** — Frame Debugger captures, Forward+/Deferred, the
  Scene View, editor-only lifecycle cases, Android and iOS hardware, Edge and Firefox. Each says why.
- **Finding F2** — implemented, not run; see the decision above.
- **The docs checkpoint** was answered "not needed": the documentation was rewritten by hand in this
  session rather than generated (`/aif-docs` would impose its own structure over it).
- **Nothing is pushed.** Two commits and the tag are local on `URP6.3`.

## Environment notes

- **Batch mode is the way to drive this project.** Every step in this session ran through
  `Unity -batchmode -executeMethod`; nothing needed an open editor except the F2 capture. Start the
  editor process through `System.Diagnostics.Process` (see decision 1).
- **Reading compile errors.** `console-get-logs` is extremely token-expensive. Grep the `-logFile`
  the batch run wrote for `error CS` instead.
- **Editing `Samples~`.** Prefer the import/author/copy-back flow above over renaming the folder.
  Keep every `.meta` *inside* `Samples~` — Package Manager copies the folder verbatim and the sample
  scenes resolve their scripts, materials and clips by GUID.
- **Long calls.** `script-execute` is aborted by the MCP client after 300 s of silence; run long
  builds through the scripts in `tools/` in a background shell instead.
- **Bash heredocs mangle content.** Writing C#/PowerShell through `cat > file <<'EOF'` collapses
  doubled backslashes. Use the `Write` tool or a Python heredoc for anything with escapes.
- **Tests write into the project.** A PlayMode run leaves `Assets/InitTestScene*.unity` behind;
  delete it before committing.

## Verification commands

```bash
ls Packages/com.virtualmaestro.raindropeffect/Runtime/Profiles/*.asset | wc -l   # 18
ls "Packages/com.virtualmaestro.raindropeffect/Samples~/EffectsGallery"          # 2 scenes + 5 folders
ls Assets                                                                        # Host Settings Plugins Packages ...
ls Logs/benchmark-*.json | wc -l                                                 # 3
grep -c FAIL docs/release-report.md                                              # 0
git rev-parse master                                                             # 901824a2...
```

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\clean-install.ps1 -Target Windows -Run
powershell -NoProfile -ExecutionPolicy Bypass -File tools\pack-and-install.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools\run-benchmark.ps1
```

Unity tests: `Unity -batchmode -runTests -testPlatform EditMode|PlayMode -testResults Logs/<name>.xml`.
