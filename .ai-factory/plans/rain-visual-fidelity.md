# Rain Visual Fidelity — restoring the look of the 2.0.0 URP port

Branch: `URP6.3` (existing — no new branch is created for this plan)
Created: 2026-09-10

> **Plan discovery note.** Branch-based consumers (`/aif-implement`, `/aif-improve`,
> `/aif-verify`, `/aif-rules-check`) derive their lookup stem from the current branch, which
> resolves to `.ai-factory/plans/URP6.3/` — the *completed* 2.0.0 migration bundle, not this file.
> Always pass this plan explicitly:
> `/aif-implement` with `Plan artifact: .ai-factory/plans/rain-visual-fidelity.md`.

## Original Request

Восстановить визуальную достоверность эффекта дождя в 2.0.0: оживить меандр friction- и flow-трейлов, сузить ленту к хвосту и добавить голову капли, переписать профиль Basic (fade-in, разброс размера, AutoRotate, drift), подключить натюрморт как фон Basic-сцены, уточнить комментарий над WarnKey, и отдельным гейт-блоком заменить бинарную coverage-маску мягкой alpha-рампой с перепроверкой D9. Контекст и корневые причины уже разобраны в .ai-factory/RESEARCH.md — использовать его как вход.

## Settings

- **Testing:** yes — Unity Test Framework. Four existing tests encode the current (broken)
  behaviour and must be updated in the same task that changes it; they are named per task below.
- **Logging:** verbose — `RainLog.Verbose` (compiled out unless `RAINDROP_VERBOSE_LOG` is defined).
  Nothing new may log per frame: decision D17 forbids it. Log at construction/bake time only.
- **Docs:** yes — mandatory documentation checkpoint at completion, routed through `/aif-docs`.

## Roadmap Linkage

Milestone: "none"
Rationale: no roadmap artifact exists in this project (`.ai-factory/ROADMAP.md` absent).

## Research Context

Source: `.ai-factory/RESEARCH.md` (Active Summary, Updated: 2026-09-10 17:59, SHA256: 85003cdb5b1563972fc1fbeef128c4d1823aaa77cc2ad12070586ec242d0a70c)

The Active Summary is copied verbatim below; it is this plan's authoritative requirements
snapshot. The source file is used only for drift checks.

Topic: Visual fidelity regressions in the 2.0.0 URP port — drop silhouettes, trail meander, sample backdrop — plus one Unity-version compile concern.

Goal: Make the shipped effect read as rain again. Four independent problems, three of them
regressions introduced by the URP port rather than asset problems.

Constraints:
- Branch `URP6.3`, tag `v2.0.0` already cut, nothing pushed.
- D3 (`docs/migration-matrix.md:19`, `docs/support-matrix.md:15`) declares minimum `6000.3` and
  forbids version-conditional code. Decision 6 below resolves to keep D3 intact.
- D9 exists because URP blends into one attachment: every layer samples the same pre-rain
  snapshot `_RainSceneColor`, so a runtime-inferred coverage would erase rain drawn underneath.
  Any coverage change must not reintroduce that.
- Textures are byte-identical to the 1.x package with GUIDs preserved; keep them that way.

Decisions:
1. Drop textures are NOT the problem. `git log --follow` reports `R100` (100% identical) for
   `Assets/RainDropEffect2/Textures/Rain/rain_drop.tga` ->
   `Packages/com.virtualmaestro.raindropeffect/Runtime/Textures/Rain/rain_drop.tga` at commit
   `b8aacf9`. All 10 shipped pairs moved the same way. The teardrop/pear silhouette is the
   original `rain_drop.tga` artwork itself (128x128 RGBA, alpha uniformly 255).

2. The pattern-like look comes from the NEW coverage channel (D9), which 1.x did not have.
   - `Runtime/Shaders/RainLens.shader` uses `Blend SrcAlpha OneMinusSrcAlpha` with
     `alpha = coverage * opacity`. The silhouette is now a visible composited object.
   - Legacy `RainDistortion (Forward).shader` used `ColorMask RGB`, `ZWrite On`, NO Blend, and
     returned `fixed4(col, 1.0)` — the quad overwrote RGB from a per-object GrabPass, so a
     flat-normal region sampled the same pixel and was invisible. No silhouette existed.
   - `Editor/Baking/CoverageMaskBaker.BakeInMemory` produces a BINARY mask
     (`NormalActiveThreshold 0.02` OR `OverlayLuminanceThreshold 0.03` -> 0 or 255), then
     `DilationPixels = 2` max passes, then a single 3x3 box average. On a 128px source that is a
     one-pixel soft edge plus a silhouette grown 2px beyond the actual drop.
   - Confirmed visually: `Runtime/Textures/Coverage/rain_drop_coverage.png` is a hard-edged solid
     white pear; `flow_coverage.png` is solid white edge to edge.
   Direction: coverage should be a continuous alpha ramp (smoothstep over normal magnitude and
   overlay luminance, real gaussian falloff), not threshold + dilation.

3. "Drops pop in and vanish" is the sample profile, not the runtime.
   `Samples~/Basic/BasicRainProfile.asset` (generated, not hand-authored — see below) has:
   `AlphaOverLifetime` 1 -> 0 with no fade-in; `SizeMin == SizeMax == 0.15` (every drop identical);
   `AutoRotate: 0` (every drop points the same way); `SizeOverLifetime` constant 1;
   `DriftOverLifetime` constant 0 (drops never move). Identical size + identical rotation +
   one texture + instant appearance reads as a stamped pattern.
   By contrast `Runtime/Profiles/Rain1.asset`, converted from the original preset, has
   `AutoRotate: 1` and `SizeMin 0.054 / SizeMax 0.073`.
   The values are authored in CODE, not only in the asset:
   `Editor/Inspectors/RainProfileEditor.CreateBasicRainProfile` (from line 132) sets
   `SizeMin = SizeMax = (0.15, 0.15)` and `AlphaOverLifetime = AnimationCurve.EaseInOut(0,1,1,0)`
   and leaves `AutoRotate` / `DriftOverLifetime` at their defaults. Three callers reproduce it:
   the `Assets/Create/Rain Drop Effect/Rain Profile (Basic Rain)` menu item
   (`RainProfileEditor.cs:116`, the path the sample README tells users to follow),
   `Editor/BuildValidation/RainBuildValidation.cs:310`, and
   `Tests/Editor/RainEffectEditorTests.cs:47`. Fixing only the sample `.asset` leaves all three
   still emitting the defect.

4. Straight trails have three independent causes, and the shipped profiles
   (`Rain1,3,5,6`, `MobileRain*`) use the Friction family, not Flow.

   4a. Friction field scanning is arithmetically dead —
       `Runtime/Simulation/FrictionLayerRuntime.FrictionStep`.
       With Rain5 values (Accel 0.138..0.244, ScanRate 150, LateralSamples 5,
       LateralStepFactor 0.375) at 60 fps and t = 1 s:
         speed 0.2 -> step 0.00333 -> iterations 3 -> stepLength 0.00111
         lateralStep 0.000417 -> half-span 0.00104 -> total candidate fan width 0.00208
       (iterations = 3, not 2: `Tests/Editor/FrictionLayerRuntimeTests.IterClampMatchesLegacyRange`
       asserts `IterFor(150f, 1f/60f) == 3`, because `150f * (1f/60f)` evaluates to 2.5000002 in
       float and rounds up. A smaller stepLength makes the fan narrower still.)
       One `FrictionField` texel spans `2 * aspect / 512 = 0.00694` in x at 16:9.
       The whole fan fits inside a third of a texel, so all 6 candidates score equal,
       `bestCount == lateral + 1`, and the code takes its explicit "carry straight on" branch.
       Compounding it: legacy `FrictionFlowRainController.PickRandomWeightedElement` did the
       OPPOSITE on an all-equal set — `Shuffle(kvList); return kvList[0]`, i.e. a random lateral
       pick, which is exactly the meander. The port removed that on the stated grounds that a
       fieldless layer "would random-walk sideways for no visible reason".
       Also relevant: `friction_map.tga` is 512x512 per-pixel noise sampled nearest, so it only
       yields signal to a fan several texels wide.
       Root cause of the shrink: the migration converted the DOWNWARD motion from a per-frame
       displacement to a per-second speed, but left the lateral fan proportional to the per-frame
       step. Everything the fan is derived from therefore shrank by roughly the frame time while
       the trajectory stayed correct. The fix is to derive the fan from `speed` (per second) rather
       than from `distance` (per frame), which both restores the legacy magnitude relationship and
       makes the fan frame-rate independent — the current form widens the fan as the frame rate
       drops, which is its own defect.

   4b. Flow lateral wobble is bounded where it should accumulate —
       `Runtime/Simulation/FlowLayerRuntime.Tick`.
       Legacy: `posXDt += 0.01f * Smooth * dt` then
       `Slerp(pos, pos + right*rnd1, posXDt)` — lerped from the CURRENT position, so each frame
       adds `posXDt * rnd1` and the offset COMPOUNDS. Over one re-roll interval
       (tau = 1/rate = 0.2 s at rate 5) the drift is `0.01 * Smooth * rnd1 * tau^2 / (2*dt)`,
       about 0.006 at Smooth 5, Amplitude 0.1, 60 fps — and successive intervals random-walk,
       reaching roughly 0.016 over a 1.4 s lifetime.
       Port: `LateralT += 0.01f * LateralSmooth * dt` then `Lerp(0f, LateralTarget, LateralT)`
       applied as an ABSOLUTE offset from `t.Start`, with `LateralT` reset to 0 on every re-roll.
       `LateralT` reaches only ~0.01 per interval, so lateral is bounded at ~0.001 and returns
       toward zero on each re-roll instead of drifting.
       Net: about 6x too small per interval, and — the qualitative defect — bounded rather than
       cumulative, which is why the path is a straight vertical line rather than a wander.
       Note the legacy form is itself frame-rate dependent (the `1/dt` above), so a faithful port
       must pick a reference rate; 60 fps is the rate every legacy constant in the migration
       matrix was converted against.

   4c. The ribbon itself is a uniform rectangle. `TrailBuffer.EmitRibbon` uses
       `WidthRange 0.15..0.15` with `TrailWidth = AnimationCurve.Constant(0,1,1)` and
       `alpha = coverage(=1) * opacity`. No taper toward the tail, and no head blob quad is
       emitted at all. `flow_normal.tga` is a gradient that varies along v only (dark, bright
       centre, dark) — a correct cylindrical lens across the ribbon width — so the normal map is
       not at fault.

5. Backdrop: `Samples~/Basic` uses `Checker.png`, 4x4 with exactly two colours —
   `(71,71,77)` and `(209,209,209)` — plus the default skybox. The still life the user wants is
   already in the repository at `Samples~/EffectsGallery/Objects/Fruits/` (Fruits.fbx, apple,
   grape, watermelon, basket textures); it is simply not wired into the Basic scene.

6. `GetInstanceID` / `GetEntityId` — RESOLVED, no code change. Exactly one helper,
   `Runtime/RainEffect.cs:281` `WarnKey`, with two call sites (`RainEffect.cs:157` `noprofile:`,
   `RainEffect.cs:172` `nolayers:`), both only keys for `RainLog.WarnOnce`. Current body is
   `System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(target)` — a plain .NET API, so
   there is no version branch at all and nothing to guard.
   The version-conditional form was considered and declined after review: for a `WarnOnce` key the
   only requirement is that distinct objects get distinct keys, which all three candidates satisfy.
   The one behavioural difference — a Unity instance id survives a domain reload while a managed
   identity hash does not — is inert here, because `RainLog.once` is a static `HashSet` that is
   itself recreated empty on domain reload.
   Plan scope for this item is therefore comment-only: make the doc comment above `WarnKey` state
   plainly that a non-Unity API is used precisely so no version branch is needed. D3 stays intact.

Open questions:
- Which scene/profile was actually on screen when the visuals were judged. Basic is assumed from
  the described grey-blue-white backdrop, but this was not confirmed.
- Whether softening the coverage ramp reintroduces the D9 erase problem that made the mask binary
  in the first place. Untested; needs a real frame, not reasoning.
- The 4a and 4b numbers are computed from source constants, not measured in a running player.
  A profiled or logged trail path would confirm them cheaply.
- Whether the fix for 4a belongs in the scan (widen the fan to a settable normalized span) or in
  the field (bake a low-frequency, blurred field instead of per-pixel noise), or both.

Success signals:
- A friction trail visibly wanders instead of falling on a plumb line.
- Drop edges read as refraction, not as a composited hard-edged blob.
- Drops differ from each other in size and orientation and fade in rather than pop.
- Refraction is legible against the sample backdrop.
- `6000.3` and `6000.5` both compile.

Agreed scope for the plan (chosen 2026-09-10): everything above — items 2, 3, 4a, 4b, 4c, 5 and
the comment-only 6. The coverage baker (item 2) is in scope but stays a separate, independently
verifiable unit of work, because softening the mask must be re-validated against D9 on a real
frame before it can be trusted.

Next step: `/aif-plan`. Cheapest-first ordering by effect: 4a (two lines in `FrictionStep`),
then the Basic profile data, then 4b, then 4c, 5 and 6, with item 2 last and gated on its own
D9 check.

## Tasks

### Phase 1 — Trail motion (the core regression)

- [x] **T1. Make the friction lateral scan wide enough to see the field, and restore the random
      tie-break when a field exists**

  File: `Packages/com.virtualmaestro.raindropeffect/Runtime/Simulation/FrictionLayerRuntime.cs`

  Two changes inside `FrictionStep`, plus its call site in `Tick`.

  1. Restore the legacy relationship between sideways reach and downward travel. Today:
     ```csharp
     var stepLength  = distance / iterations;
     var lateralStep = stepLength * s.LateralStepFactor;   // ~1/3 of one field texel
     var half        = lateral * 0.5f * lateralStep;
     ```
     Legacy did **not** tie the fan to the per-iteration step. In
     `FrictionFlowRainController.GetNextPositionWithFriction` the offsets run
     `left*dv + right*dv*(j/widthResolution)` for `j = 0..2*widthResolution`, i.e. a span of
     exactly `±dv` where `dv` is the downward advance. The invariant is
     **lateral half-span == downward advance**, a 1:1 ratio.

     Change to `half = distance * s.LateralStepFactor`, with
     `lateralStep = 2f * half / lateral`. `LateralStepFactor` then means something concrete —
     lateral reach as a multiple of the per-frame downward step — instead of being an opaque
     scale on an already-wrong quantity. Keep `stepLength` derived from `distance` as it is now.

     Raise the default in `Runtime/Settings/FrictionLayerSettings.cs` from `0.375f` to `1f`
     (legacy parity), and update `LateralStepFactor` to `1` in the shipped friction profiles
     (`Rain1`, `Rain3`, `Rain5`, `Rain6`, `MobileRain1`, `MobileRain3`, `BloodFlow`,
     `WaterSplashOut`, `Bench_Combined`, `Bench_Trails`). Field *name* and type stay unchanged —
     the serialization contract is about identifiers, not values.

     Measured against the real `FrictionField_friction_map.asset` (512², 40 seeds, 2 s lifetime,
     Rain5 acceleration, 60 fps), median lateral drift:
     ```
     current  (half = 0.9375 * distance/3)        0.0084   reads as a plumb line
     factor 0.375 with the new formula            0.0187
     factor 1.0  with the new formula             0.0540   == legacy
     the previously proposed speed-based fan      0.5508   max 1.80, crosses the screen
     ```
     Do not use a speed-derived fan: at `speed / iterations * LateralStepFactor` the half-span is
     `0.0625` — about 19x the legacy relation — and drops slide sideways across the whole frame.

  2. Restore the legacy random pick on an all-equal candidate set, but only when
     `s.Field != null`. The existing "carry straight on" branch stays as the fieldless path.
     Legacy reference: `FrictionFlowRainController.PickRandomWeightedElement` did
     `Shuffle(kvList); return kvList[0]` when every score matched. Use the existing `rng` so the
     result stays reproducible under a seed.

  **Both changes are required; neither is sufficient alone.** With the tie-break restored but the
  fan left as it is, measured drift only reaches `0.0153` — still visually a straight line. Do not
  land one half and treat the task as done.

  Logging: one `RainLog.Verbose` at construction reporting the resolved fan half-span in
  normalized units and its ratio to one field texel, so a too-narrow fan is diagnosable without a
  debugger. Construction only — never per frame (D17).

  Tests (`Packages/com.virtualmaestro.raindropeffect/Tests/Editor/FrictionLayerRuntimeTests.cs`):
  - `StraightFallWithoutField` — must stay green unchanged. It is the guard proving change (2)
    is correctly gated on `Field != null`.
  - `EqualScoresTieBreakDeterministic` — must stay green; determinism under a seed is preserved.
  - `IterClampMatchesLegacyRange` — unaffected, do not touch.
  - Add: with a field whose left half scores high and right half low, a trail seeded anywhere
    must end measurably left of where it started — the assertion that steering actually happens.
    (`FrictionField.Sample` returns higher = more attractive, and `FrictionFieldBaker` line 123
    already inverts with `1f - gray` to match legacy's `1.0f - pixel`, so darker source pixels
    score higher. Build the fixture accordingly.)
  - Add: a **statistical** frame-rate check, not a path-equality one. Median absolute lateral
    drift across N seeds must stay inside a stated band at `dt = 1/30`, `1/60` and `1/120`.
    A path-equality assertion is unsatisfiable here: the scan consumes `rng` draws per iteration
    per frame, so a different `dt` means a different draw count and the trajectories cannot
    coincide at any tolerance. Reference measurements for the legacy relation are
    `0.088 / 0.054 / 0.051` at 30 / 60 / 120 fps — the residual spread is inherent to a random
    walk over a noise field and is the band to encode, not a defect to engineer away.

- [x] **T2. Make the flow lateral offset accumulate instead of snapping back**

  File: `Packages/com.virtualmaestro.raindropeffect/Runtime/Simulation/FlowLayerRuntime.cs`

  Today `lateral` is an absolute offset from `t.Start`, recomputed each frame and reset toward
  zero on every re-roll:
  ```csharp
  t.LateralT = Mathf.Min(1f, t.LateralT + 0.01f * s.LateralSmooth * ctx.Dt);
  var lateral = Mathf.Lerp(0f, t.LateralTarget, t.LateralT);
  ```
  Legacy compounded it: `Slerp(pos, pos + right*rnd1, posXDt)` adds `posXDt * rnd1` to the
  *current* position every frame, so the drop random-walks sideways.

  Add a persistent `Lateral` field to the `Trail` struct and integrate it. Repurpose `LateralT`
  as seconds elapsed since the last re-roll (reset to `0f` on re-roll, as now), then advance the
  offset by a term proportional to that elapsed time:
  `Lateral += gain * s.LateralSmooth * t.LateralTarget * t.LateralT * ctx.Dt`.
  Choose `gain` so the result matches legacy at the 60 fps reference every other constant in
  `docs/migration-matrix.md` was converted against — legacy drift over one re-roll interval is
  `0.01 * Smooth * target * tau^2 / (2 * dt)`, which at `dt = 1/60` differentiates to a gain of
  `0.6`. Put that derivation in the doc comment; a bare magic number here is how the original
  defect happened.

  `t.Pos` then uses `side * t.Lateral` instead of `side * lateral`. Reset `Lateral` to `0f` in
  `Spawn` alongside the other per-trail state.

  Do NOT rename or retype `LateralSmooth` or `LateralAmplitude` — serialization contract.

  Logging: none at runtime. This path runs per trail per frame (D17).

  Tests (`Packages/com.virtualmaestro.raindropeffect/Tests/Editor/FlowLayerRuntimeTests.cs`):
  - `DeterministicWithSeed`, `FrameRateIndependence` — must stay green. `FrameRateIndependence`
    is the important one: the new integration must not reintroduce frame-rate dependence.
  - `FluctuationMeanMatchesRate`, `ZeroFluctuationRateNeverReRolls` — unaffected.
  - Add: over a full lifetime the accumulated `|Lateral|` exceeds the current bounded ceiling by
    a stated factor, and successive trails from different seeds land on different sides — the
    assertion that it wanders rather than tracks a line.

**Commit checkpoint 1** — `fix(simulation): restore trail meander in the friction scan and flow wobble`

### Phase 1b — Look at it before building on it

- [x] **T10. Visual check of trail motion**

  No files. This is a gate on T1/T2, not a code task.

  T1's magnitude is derived from a simulation against the real field asset, not measured in the
  engine, and the Risks section says so. A wrong fan is cheap to correct here and expensive to
  correct after three more commits sit on top of it.

  Steps:
  - Open `Samples~/EffectsGallery/Gallery.unity`.
  - `Runtime/Profiles/Rain5.asset` — friction trails. Confirm a trail visibly wanders instead of
    falling on a plumb line, and that it does **not** slide sideways across the frame.
  - `Runtime/Profiles/Rain2.asset` — flow trails. Confirm the same for the T2 path.
  - Expected order of magnitude: roughly 5% of half-screen of lateral drift over a trail's life.
    Obvious wander, nothing like a diagonal.

  If it is off, the tuning knob is `LateralStepFactor` in the profiles — the formula from T1 makes
  it a direct multiple of the downward step, so doubling it doubles the reach. Do not change the
  code for a magnitude problem.

  Once T4/T5 land, re-open `Samples~/Basic/Basic.unity` once and confirm the drops read as varied
  and fade in against the new backdrop. That needs no separate task.

### Phase 2 — Trail appearance

- [x] **T3. Taper the ribbon toward the tail and emit a head quad**

  Files:
  - `Packages/com.virtualmaestro.raindropeffect/Runtime/Simulation/TrailBuffer.cs` (`EmitRibbon`)
  - `Packages/com.virtualmaestro.raindropeffect/Runtime/Simulation/FlowLayerRuntime.cs` (capacity)
  - `Packages/com.virtualmaestro.raindropeffect/Runtime/Simulation/FrictionLayerRuntime.cs` (capacity)
  - `Packages/com.virtualmaestro.raindropeffect/Runtime/Settings/FlowLayerSettings.cs`
  - `Packages/com.virtualmaestro.raindropeffect/Runtime/Settings/FrictionLayerSettings.cs`

  Two deliverables.

  1. **Taper — default only; the shipped content already tapers.** Verify before assuming
     otherwise: `Rain1`, `Rain2`, `Rain3`, `Rain5`, `Rain6`, `MobileRain1`, `MobileRain3`,
     `BloodFlow` and `WaterSplashOut` all carry an authored `TrailWidth` of `0 -> 1 -> 0`.
     `EmitRibbon` already evaluates `widthCurve.Evaluate(1f - progress)` per point, so the taper
     mechanism works and needs no emit-side change.

     What is actually flat is the *class default*, `AnimationCurve.Constant(0, 1, 1)` in
     `FlowLayerSettings` and `FrictionLayerSettings` — so every newly created layer starts as a
     uniform rectangle. Change only that default to a head-to-tail falloff.

     **Leave `Bench_Combined` and `Bench_Trails` flat.** Their constant width is part of the
     measured load in `Tests/Runtime/DenseTrailBenchmarkTests.cs`; tapering them makes the
     recorded budget incomparable with earlier runs.
  2. **Head quad.** Append one quad at the newest point of each ribbon, sized from `widthMul`,
     oriented along `Dir`, sharing the trail's tint/params/opacity. Reuse
     `RainBatchMesh.AddQuad` rather than hand-writing vertices.

  Capacity must grow with the head quad or it will silently overflow. Both runtimes size their
  mesh in the constructor as `2 * Cap * Pts` vertices and `6 * Cap * (Pts - 1)` indices; add
  `4 * Cap` vertices and `6 * Cap` indices. `RainBatchMesh` caps at `MaxVerticesLimit = 65535`
  (UInt16 indices) and throws above it — check the High-quality worst case
  (`MaxTrails 32 * MaxPoints 96`) still fits before changing the formula.

  Logging: one `RainLog.Verbose` at construction with the resolved vertex/index capacity and the
  head-quad allowance, so an overflow is traceable to the formula. Construction only.

  Tests:
  - `Tests/Editor/FlowLayerRuntimeTests.EmitProducesTwoVerticesPerPoint` — **will fail**; it
    asserts `2 * points == Mesh.VertexCount`. Update it to `2 * points + 4 * activeTrails` and
    rename it to say what it now guards.
  - `Tests/Editor/TrailBufferTests.EmitRibbonLayout` — **will fail** for the same reason. Update
    the expected layout.
  - `Tests/Editor/TrailBufferTests.EmitRibbonProducesFiniteVertices` — extend to cover the head
    quad's vertices.
  - `Tests/Runtime/TrailAllocationTests.cs` and `Tests/Runtime/DenseTrailBenchmarkTests.cs` —
    re-run; the capacity change must not make either allocate per frame or breach its budget.
  - Add: a full-capacity layer at High quality emits without setting `Mesh.Overflowed`.

### Phase 3 — Sample presentation

- [x] **T4. Re-author the Basic rain profile so drops vary and fade in**

  Primary file: `Packages/com.virtualmaestro.raindropeffect/Editor/Inspectors/RainProfileEditor.cs`
  (`CreateBasicRainProfile`, from line 132)
  Regenerated artifact: `Packages/com.virtualmaestro.raindropeffect/Samples~/Basic/BasicRainProfile.asset`

  **Fix the generator, not just the asset.** `CreateBasicRainProfile` is where the bad values are
  authored in code — it sets `SizeMin = SizeMax = new Vector2(0.15f, 0.15f)`,
  `AlphaOverLifetime = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f)` (no fade-in), and leaves
  `AutoRotate` and `DriftOverLifetime` at their defaults. The sample asset is just its output.
  Patching only the `.asset` leaves three callers still producing the defect:
  - `Editor/Inspectors/RainProfileEditor.cs:116` — the **Assets → Create → Rain Drop Effect →
    Rain Profile (Basic Rain)** menu item, i.e. what a user gets when following the sample README.
  - `Editor/BuildValidation/RainBuildValidation.cs:310` — creates the preset in a consumer project.
  - `Tests/Editor/RainEffectEditorTests.cs:47` — will exercise whatever the generator produces.

  Use `Packages/com.virtualmaestro.raindropeffect/Runtime/Profiles/Rain1.asset` — converted from
  the original 1.x preset — as the reference for what plausible values look like.

  Change in the generator, then regenerate the sample asset from it (keeping the asset's `.meta`
  GUID and the `m_Script` GUID `5a22c287852e50a418ba071574258e01` intact) on the `Rain Drops`
  simple layer:
  - `AlphaOverLifetime`: add a fade-in so drops arrive rather than appear. Currently `1 -> 0`.
  - `SizeMin` / `SizeMax`: give them a real spread. Currently both `{0.15, 0.15}`; Rain1 uses
    `0.054` / `0.073`. The Basic drops are also simply too large — 0.15 is 15% of half-screen.
  - `AutoRotate`: `1`. Currently `0`, so every drop points the same way, which is the single
    biggest contributor to the "stamped pattern" reading.
  - `DriftOverLifetime`: non-zero, so drops creep instead of hanging motionless.
  - `SizeOverLifetime`: a slight grow-then-hold reads better than a constant.

  Also check the `Rain Frame` static layer's `FadeTime` / fade curve still makes sense against the
  new droplet timing.

  Logging: the generator already ends with `RainLog.Verbose($"created basic rain profile at
  {assetPath}")`. Keep it; no per-frame logging is involved.

  Tests:
  - `Tests/Editor/RainEffectEditorTests.cs` — calls the generator directly; re-run and update any
    assertion that pins the old values.
  - `Tests/Editor/RainProfileTests.cs` — check whether it asserts anything about these values.
  - Add: the generated Basic profile has `SizeMin != SizeMax` and an `AlphaOverLifetime` that
    starts below full opacity — a red test against the exact defect being fixed, so a future
    regeneration cannot quietly restore it.

- [x] **T5. Give the Basic sample a backdrop the refraction is legible against**

  Files: `Packages/com.virtualmaestro.raindropeffect/Samples~/Basic/Basic.unity` plus one new
  texture and material in the same folder.

  The scene currently holds `Directional Light`, `Ground`, `Main Camera`, `Transparent Cube` and
  a 4x4 two-colour `Checker.png`. Add a full-frame colourful backdrop behind the camera's view —
  a quad with an unlit material carrying a colourful image is enough; the user explicitly said a
  picture suffices and a full still-life scene is not required.

  **Constraint that decides the approach:** Package Manager copies each sample independently, so
  `Samples~/Basic` must not reference an asset under `Samples~/EffectsGallery`. A cross-sample
  reference imports as a broken link for anyone who installs only Basic. Put the backdrop texture
  inside `Samples~/Basic/`, even though a similar image already exists in the Gallery.

  Keep the existing `Transparent Cube` and `Ground` — the sample README promises both lens
  distortion and blur are visible, and that text should stay true.

  Logging: none — scene data.

  Tests: `Tests/Editor/SamplesLayoutTests.cs` must stay green. It requires every declared sample
  folder in `package.json` to exist and contain a `.unity` or `README.md`, and requires
  `Samples~` to keep its tilde. Adding files does not breach either, but re-run it.
  Update `Samples~/Basic/README.md` to describe the new backdrop.

### Phase 4 — Comment accuracy

- [x] **T6. State plainly why `WarnKey` avoids the Unity id APIs**

  File: `Packages/com.virtualmaestro.raindropeffect/Runtime/RainEffect.cs` (around line 277-284)

  No code change — this was decided against after review. The current body,
  `RuntimeHelpers.GetHashCode(target)`, is a plain .NET API, so there is no version branch to
  guard and D3 stays intact.

  The existing comment explains the 6000.5 obsolete-as-error history but reads as if a version
  branch were merely forbidden rather than unnecessary. Rewrite it to say: a `WarnOnce` key needs
  only distinct-objects-give-distinct-keys; the one behavioural difference (a Unity instance id
  survives a domain reload, a managed identity hash does not) is inert because `RainLog.once` is
  a static `HashSet` recreated empty on domain reload; therefore no `#if` is needed and D3 is not
  in tension.

  Logging: none.

  Tests: none. Verify both editors still compile — `6000.3.23f1` and `6000.5.10f1` per D3.

**Commit checkpoint 2** — `feat(samples): taper trails, vary the Basic drops, add a backdrop and correct the WarnKey comment`

### Phase 5 — Coverage silhouette (gated; do not merge on reasoning alone)

- [x] **T7. Replace the binary coverage threshold with a continuous alpha ramp**

  File: `Packages/com.virtualmaestro.raindropeffect/Editor/Baking/CoverageMaskBaker.cs`

  `BakeInMemory` currently emits `(byte)(active || lit ? 255 : 0)` from two hard thresholds, then
  dilates 2 px and applies a single 3x3 box average — a one-pixel edge on a 128px source, plus a
  silhouette grown 2 px past the drop.

  Replace with a continuous ramp: map normal magnitude and overlay luminance through a smoothstep
  band rather than a step, and soften with a real separable gaussian whose radius is proportional
  to the mask size instead of one fixed box pass. Reconsider `DilationPixels = 2` — with a soft
  ramp the dilation is what pushes the silhouette outside the artwork.

  Keep `BakeInMemory` pure and deterministic — it is deliberately separated from asset I/O so it
  can be tested without importer round-trips, and `BakeIsDeterministic` depends on that.

  Logging: the bake already `Debug.Log`s coverage percentage per mask. Keep it and add the ramp's
  parameters, so a regression is visible in the bake output. Editor-time only.

  Tests (`Packages/com.virtualmaestro.raindropeffect/Tests/Editor/CoverageMaskBakerTests.cs`):
  - `BakeProducesMaskWhereNormalCarriesData` — **will need updating**, but preserve its two
    corner assertions exactly: `mask[0] == 0` and `mask[Size*Size-1] == 0`. A gaussian wide
    enough to bleed into the far corners is too wide, and those asserts are the guard.
  - `BakeIsDeterministic` — must stay green unchanged.
  - `ImporterSettingsRestored` — unaffected.
  - Add: the ramp is monotonic across the silhouette edge and spans more than one byte step, i.e.
    it is genuinely soft rather than a threshold with a blur on top.

  Then re-bake all 10 masks via **Rain Drop Effect → Bake Coverage Masks** and commit the
  regenerated PNGs.

- [x] **T8. Verify on a real frame that the soft ramp does not reintroduce the D9 erase problem**

  This is the gate. T7 must not be considered done until this passes.

  D9 exists because every layer samples the same pre-rain snapshot `_RainSceneColor` and blends
  into one attachment, so a layer with soft alpha over a flat-normal region can wash out rain
  drawn underneath it. Reasoning cannot settle this; a frame can.

  Steps:
  - Open `Samples~/EffectsGallery/Gallery.unity` and a profile with stacked layers —
    `Runtime/Profiles/Rain1.asset` or `Bench_Combined.asset` (static + simple + friction).
  - Capture the Game View before and after the re-bake and compare: drops under a fill layer must
    remain visible, and the fill must not gain a visible rectangular edge.
  - Re-run `Tests/Runtime/ReleaseRenderingTests.cs` and `Tests/Runtime/RainBlurPassTests.cs`.
  - Record the outcome in `docs/migration-report.md` as a new finding, next to F1 and F2.

  If the erase problem returns, stop and report rather than tuning blindly: the alternative is a
  shader-side change (coverage as a soft mask multiplied by a normal-magnitude term at sample
  time), which is a different plan.

**Commit checkpoint 3** — `fix(baking): soften the coverage ramp, with the D9 stacked-layer check recorded`

### Phase 6 — Documentation

- [x] **T9. Documentation checkpoint**

  Run `/aif-docs`. At minimum:
  - `docs/migration-matrix.md` — the friction row's lateral-scan description is now wrong; state
    that the fan is per-second, and record the D9 re-validation outcome from T8.
  - `docs/migration-report.md` — add findings for the friction fan, the flow accumulation and the
    coverage ramp, in the same shape as the existing F1/F2.
  - `Samples~/Basic/README.md` — the new backdrop and the re-authored profile.
  - `Packages/com.virtualmaestro.raindropeffect/CHANGELOG.md` — a `2.0.1` entry.

  Separately flag, do not silently fix: `.ai-factory/DESCRIPTION.md` and
  `.ai-factory/ARCHITECTURE.md` still describe the deleted `Assets/RainDropEffect2/` legacy tree
  as the live structure. They are owned by `/aif` and `/aif-architecture`, not by this plan.

**Commit checkpoint 4** — `docs: record the visual-fidelity fixes and refresh the sample docs`

## Commit Plan

| # | After | Message |
|---|-------|---------|
| 1 | T1, T2, T10 | `fix(simulation): restore trail meander in the friction scan and flow wobble` |
| 2 | T3, T4, T5, T6 | `feat(samples): taper trails, vary the Basic drops, add a backdrop and correct the WarnKey comment` |
| 3 | T7, T8 | `fix(baking): soften the coverage ramp, with the D9 stacked-layer check recorded` |
| 4 | T9 | `docs: record the visual-fidelity fixes and refresh the sample docs` |

## Risks

- **T7/T8 is the only task that can fail on its premise.** Everything else is a local behavioural
  fix with a test around it; the coverage ramp trades against the exact problem D9 was introduced
  to solve. It is ordered last and gated so a failure there does not block Phases 1-4.
- **T3 changes mesh capacity.** An arithmetic slip overflows silently at High quality rather than
  throwing — `RainBatchMesh.AddVertex` sets `Overflowed` and returns a scratch vertex. The
  full-capacity emit test in T3 is the guard.
- **T1's magnitude is derived from simulation, not measured in the engine.** T10 is the gate that
  catches a mis-tuned fan before later work is stacked on it. The tuning knob is
  `LateralStepFactor` in the profiles — after T1 it is a direct multiple of the downward step —
  not another code change.
- **T6 is deliberately in checkpoint 2, not 3.** It is an isolated comment fix; grouping it with
  the D9-gated coverage work would block a safe change behind the plan's riskiest one.
- **T4/T5 touch sample data whose GUIDs matter.** Preserve `.meta` GUIDs and serialized field
  names per the project's maintenance constraints.
