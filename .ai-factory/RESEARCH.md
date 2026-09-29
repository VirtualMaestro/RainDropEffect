# Research

Updated: 2026-09-10 18:26
Status: active

## Active Summary (input for /aif-plan)
<!-- aif:active-summary:start -->
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
   - Audit of all ten shipped masks (share of pixels strictly between 8 and 247, i.e. genuine
     partial coverage):
       flow_coverage.png         128x128    min 255  max 255    0.00%   no silhouette at all
       frozenfill_coverage.png  1024x1024   min 170  max 255    0.00%   never fully transparent
       frozenframe_coverage.png 1024x1024   min   0  max 255    1.76%
       bubble_coverage.png       256x256    min   0  max 255    2.71%
       wash_coverage.png         512x512    min   0  max 255    2.51%
       bloodframe_coverage.png  1024x1024   min   0  max 255    4.01%
       rain_frame_coverage.png  1024x1024   min   0  max 255    4.31%
       rain_drop_coverage.png    128x128    min   0  max 255    5.47%
       bloodsplatter_coverage.png 512x512   min   0  max 255    8.09%
       rain_white_coverage.png     32x32    min   0  max 255   16.80%
     Two are degenerate rather than merely hard: `flow_coverage` carries no shape information
     whatsoever, and `frozenfill_coverage` has a floor of 170 so that fill layer cannot fade out
     at its edges. Both must be checked explicitly after any re-bake.
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
       SUPERSEDED (2026-09-10, second session): an earlier version of this entry concluded that
       "the fix is to derive the fan from `speed` (per second) rather than from `distance` (per
       frame)". That is wrong and was disproved by simulation — see the measured table below.

       The real legacy invariant is visible in `GetNextPositionWithFriction`:
       `downVector += Vector3.left * dv + Vector3.right * dv * ((float)j / widthResolution)` for
       `j = 0..2*widthResolution`, i.e. a span of exactly `±dv` where `dv` is the downward
       advance. Lateral half-span EQUALS the downward step — a 1:1 ratio, with no reference to
       speed at all. The port's `stepLength * 0.375` lands at 0.31 of that ratio, which is the
       same order of magnitude; the fan is narrow enough to fail only because 0.31x of a
       per-frame step happens to fall under one field texel.

       Measured against the real `FrictionField_friction_map.asset` (512x512, 40 seeds, 2 s
       lifetime, Rain5 acceleration, 60 fps), median absolute lateral drift over a trail's life:
         current  (half = 0.9375 * distance / 3)          0.0084   reads as a plumb line
         tie-break restored, fan unchanged                 0.0153   still a plumb line
         half = distance          (legacy invariant)       0.0540   the target
         half = speed / iters * 0.375 (the wrong fix)      0.5508   max 1.805, crosses the frame
       Both changes are required; the tie-break alone reaches only 0.0153.

       Chosen form: `half = distance * s.LateralStepFactor` with `lateralStep = 2*half/lateral`,
       default raised 0.375 -> 1. This keeps `LateralStepFactor` a live knob and gives it a
       concrete meaning — lateral reach as a multiple of the per-frame downward step.

       Frame-rate note: a random walk over a noise field cannot be made path-identical across
       `dt`, because the number of `rng` draws changes with the frame count. Measured drift for
       the legacy form is 0.088 / 0.054 / 0.051 at 30 / 60 / 120 fps. Only a statistical band is
       assertable; a path-equality test would be unsatisfiable.

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

   4c. SUPERSEDED (2026-09-10, second session): the earlier claim "the ribbon itself is a uniform
       rectangle ... no taper toward the tail" is wrong for shipped content. Every shipped trail
       profile already carries an authored taper of `0 -> 1 -> 0`: `Rain1`, `Rain2`, `Rain3`,
       `Rain5`, `Rain6`, `MobileRain1`, `MobileRain3`, `BloodFlow`, `WaterSplashOut`. Only
       `Bench_Combined` and `Bench_Trails` are flat, deliberately — their constant width is part
       of the load measured by `Tests/Runtime/DenseTrailBenchmarkTests.cs`. What is flat is the
       CLASS DEFAULT, `AnimationCurve.Constant(0,1,1)` in `FlowLayerSettings` and
       `FrictionLayerSettings`, so only newly created layers start uniform.

       The actual defect is that the ribbon has no silhouette at all:
       `Runtime/Textures/Coverage/flow_coverage.png` is 128x128 with min = max = 255 — uniformly
       opaque. `alpha = coverage * opacity` therefore degenerates to `alpha = opacity`, and the
       ribbon's only shape is its mesh geometry, whose edges are raw triangle edges with no
       softening. Combined with 4a's dead-straight motion that reads as a hard-edged bar. The
       taper is present but invisible at the ends, where the curve reaches 0 and the width floors
       at `MinHalfWidth = 0.001` (about 1 px at 1080p), leaving only the fat middle on screen.
       No head blob quad is emitted either — that part of the earlier entry stands.

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
- How `flow_normal.tga` actually unpacks in the lens shader. Both `flow.tga` and
  `flow_normal.tga` vary only along v (across the ribbon) and are constant along u: the normal
  reads 18 at v=0, 235 at v=0.5, 11 at v=1, and its alpha channel is uniformly 255. The meta is
  `textureType: 1` (NormalMap), `convertToNormalMap: 0`. If Unity compresses to DXT5nm on this
  platform, `UnpackNormal` takes x from that constant alpha, giving `n.x` near +1 across the whole
  ribbon, and `col *= (1 - n.x * Relief)` at `Relief 1.5` would go negative and clamp to black —
  which is not what is observed on screen. So either the format is not DXT5nm, or the
  `UNITY_NO_DXT5nm` branch applies, or this reading of the unpack is wrong. Not resolvable by
  reading source; needs a Frame Debugger capture or a probe shader. Relevant because it decides
  whether trail shading is merely unshaped (4c) or actively wrong.
- Whether softening the coverage ramp reintroduces the D9 erase problem that made the mask binary
  in the first place. Untested; needs a real frame, not reasoning.
- Whether `frozenfill_coverage.png`'s floor of 170 is deliberate (a fill meant to stay partly
  opaque) or a bake artefact. It predates this investigation and is not part of the reported
  complaints, but a re-bake will change it either way.
- The 4b numbers are computed from source constants, not measured in a running player. 4a is no
  longer in this category — it was simulated against the real field asset (see the table in 4a) —
  but neither has been confirmed in the engine.

Resolved since the first session:
- "Which scene/profile was on screen" — partly answered. `Samples~/Basic` declares
  `FlowLayers: []` and `FrictionLayers: []`, so it contains no trails at all; the trail complaints
  cannot come from it. The stamped-drops complaint does match Basic. At least two different
  scenes/profiles were therefore being judged, and the two complaint groups belong to different
  content. Which profile produced the trails is still unconfirmed.
- "Whether the fix for 4a belongs in the scan or in the field" — answered: in the scan. The
  measured table in 4a shows the scan change alone reaches the legacy figure; the noise field
  needs no re-bake.
- "Mathf.RoundToInt(2.5f)" — answered: 3, pinned by
  `Tests/Editor/FrictionLayerRuntimeTests.IterClampMatchesLegacyRange`.

Success signals:
- A friction trail visibly wanders instead of falling on a plumb line.
- A trail's long edges no longer read as straight mesh edges: `flow_coverage.png` carries an
  actual silhouette instead of a uniform 255.
- Drop edges read as refraction, not as a composited hard-edged blob.
- Drops differ from each other in size and orientation and fade in rather than pop.
- Refraction is legible against the sample backdrop.
- `6000.3` and `6000.5` both compile.

Agreed scope for the plan (chosen 2026-09-10): everything above — items 2, 3, 4a, 4b, 4c, 5 and
the comment-only 6. The coverage baker (item 2) is in scope but stays a separate, independently
verifiable unit of work, because softening the mask must be re-validated against D9 on a real
frame before it can be trusted.

Next step: the plan already exists at `.ai-factory/plans/rain-visual-fidelity.md` (10 tasks) and
its task bodies for T1 and T3 already carry the corrected 4a and 4c conclusions above. Its
`## Research Context` still pins the SHA256 of the previous revision of this summary, so the plan
is deliberately AHEAD of this file rather than behind it; re-pin via `/aif-improve` when
convenient. No task needs rewriting because of this session.
<!-- aif:active-summary:end -->

## Sessions
<!-- aif:sessions:start -->
### 2026-09-10 17:28 — Why the ported effect stopped looking like rain
What changed:
Traced four user-reported visual complaints to root causes. Established that the drop textures are
byte-identical to 1.x (so the artwork is not the regression) and that the new D9 coverage channel,
a binary-thresholded mask composited through an alpha blend, replaced 1.x's invisible
GrabPass-overwrite lens. Found the friction trail's lateral candidate fan is narrower than one
field texel, so the scan always ties and the port's tie-break carries the drop straight down —
where legacy shuffled and picked at random, which was the meander. Found the flow lateral wobble
was ported as an absolute Lerp factor where legacy used it as a recurrence rate, losing ~30x of
amplitude and all accumulation. Confirmed the Basic sample profile has zero size, rotation,
fade-in or drift variation. Located the still-life assets already present in EffectsGallery.
Scoped the GetEntityId change to one helper with two call sites, and noted it conflicts with D3.

Key notes:
- Legacy shader returned alpha 1 under `ColorMask RGB` with no Blend; the port alpha-blends a
  baked silhouette. That single topology change is what turned drops into visible stamps.
- The friction fan/texel arithmetic is the highest-value finding: two lines of behaviour explain
  a complaint that looks like it needs a simulation rewrite.
- Coherence gate: passed. Claims above are sourced from file contents, git rename metadata and
  image inspection performed this session; the numeric fan-vs-texel and 30x figures are derived
  from source constants and are labelled as such in Open questions.

Links (paths):
- Packages/com.virtualmaestro.raindropeffect/Runtime/Shaders/RainLens.shader
- Packages/com.virtualmaestro.raindropeffect/Editor/Baking/CoverageMaskBaker.cs
- Packages/com.virtualmaestro.raindropeffect/Runtime/Simulation/FrictionLayerRuntime.cs
- Packages/com.virtualmaestro.raindropeffect/Runtime/Simulation/FlowLayerRuntime.cs
- Packages/com.virtualmaestro.raindropeffect/Runtime/Simulation/TrailBuffer.cs
- Packages/com.virtualmaestro.raindropeffect/Runtime/RainEffect.cs (WarnKey, line 281)
- Packages/com.virtualmaestro.raindropeffect/Samples~/Basic/BasicRainProfile.asset
- Packages/com.virtualmaestro.raindropeffect/Runtime/Profiles/Rain1.asset
- Packages/com.virtualmaestro.raindropeffect/Runtime/Textures/Coverage/rain_drop_coverage.png
- Packages/com.virtualmaestro.raindropeffect/Samples~/EffectsGallery/Objects/Fruits/
- docs/migration-matrix.md (D3, D9), docs/support-matrix.md
- git: b8aacf9 (R100 texture move), 5a52b95 (original 1.x sources)
### 2026-09-10 18:26 — Correcting 4a and 4c against measurement

What changed:
Two conclusions from the first session were disproved and are now marked SUPERSEDED in place
rather than deleted. 4a's proposed speed-derived fan was simulated against the real
`FrictionField_friction_map.asset` and produces 0.5508 median lateral drift (max 1.805, a drop
crossing the whole frame) versus the legacy invariant's 0.0540; the legacy relation is
lateral-half-span == downward-step, read directly off `GetNextPositionWithFriction`, with no
reference to speed. 4c's "no taper toward the tail" was checked against every shipped profile and
is false — nine of them already carry an authored `0 -> 1 -> 0` curve; the real defect is that
`flow_coverage.png` is uniformly 255, so the ribbon has no silhouette and shows raw mesh edges.
Added an audit of all ten coverage masks to decision 2, which also surfaced
`frozenfill_coverage.png`'s floor of 170. Closed three open questions and opened two.

Key notes:
- The tie-break restoration alone reaches only 0.0153 drift; both halves of the 4a fix are
  required. The first session presented them as two changes without saying either was
  insufficient alone.
- A random walk over a noise field cannot be path-identical across `dt`, so only a statistical
  frame-rate band is assertable. Measured 0.088 / 0.054 / 0.051 at 30 / 60 / 120 fps.
- `Samples~/Basic` has no trail layers at all, so the trail complaints and the stamped-drop
  complaint came from different scenes.
- New unknown recorded rather than guessed: how `flow_normal.tga` unpacks, given its uniformly
  255 alpha under `textureType: 1`. Needs a Frame Debugger capture.
- Coherence gate: passed. Superseded conclusions carry explicit SUPERSEDED markers with dates;
  the 4a figures are labelled as simulation against the real asset, not engine measurement.

Links (paths):
- Packages/com.virtualmaestro.raindropeffect/Runtime/Simulation/FrictionLayerRuntime.cs
- Packages/com.virtualmaestro.raindropeffect/Runtime/Data/FrictionField_friction_map.asset
- Packages/com.virtualmaestro.raindropeffect/Runtime/Textures/Coverage/ (all ten masks)
- Packages/com.virtualmaestro.raindropeffect/Runtime/Textures/Rain/flow_normal.tga(.meta)
- Packages/com.virtualmaestro.raindropeffect/Runtime/Profiles/ (TrailWidth curves)
- .ai-factory/plans/rain-visual-fidelity.md (T1, T3 already carry the corrected conclusions)

<!-- aif:sessions:end -->
