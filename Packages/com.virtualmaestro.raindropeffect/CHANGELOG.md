# Changelog

All notable changes to this package are documented in this file.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this package
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.2] - 2026-09-29

Distribution weight. The package is delivered from GitHub, so every consumer downloads the whole
package folder — including sample media they may never import. The tarball was 56 MB, of which 43 MB
was the Effects Gallery sample and 19 MB of that was referenced by no scene at all. No runtime code,
no shader, no profile and no runtime texture changed; the package API is untouched.

### Removed

- **`BloodAndSplash.unity` and its scripts** (`BloodRainSample`, `SplashSample`, `GalleryMenuSample`,
  `AxisRotatorSample`). Blood, splash and frozen remain as profiles and as camera prefabs under
  `Samples~/EffectsGallery/Prefabs/`, which reference profiles only and need no sample script. The
  1.x HP-curve mapping went with them; damage feedback is game logic, not library code.
- **`Samples~/EffectsGallery/Sounds/`** — seven WAV files, 25 MB compressed. Four of them
  (`rain`, `shower`, `in_water`, and `player_killed_1`) were referenced by nothing; the rest went with
  the scene that used them.
- **`Samples~/EffectsGallery/Objects/`** — the Fruits still life (a 10 MB FBX, 17 materials and nine
  textures, 18 MB compressed) and the Prototyping grid props, which no scene referenced.
- **The whole migration layer.** `Documentation~/legacy-converter/` (the 1.x prefab converter shipped
  as text), `Editor/Migration/AssetMover.cs` (one-time texture moves and import roles) and the
  `docs/migration-*.md` guides with their before/after captures. The port they served finished two
  releases ago; a package that still ships a converter for an asset it replaced is carrying someone
  else's history. `LegacyTermsTests` is renamed `ForbiddenTermsTests` and now guards only what still
  matters — no Built-in-pipeline construct and no `Shader.Find` — instead of 1.x type names.

### Changed

- **The gallery backdrop is one textured quad** (`Cottage.jpg` on `Backdrop.mat`, 180 KB) instead of
  the rotating still life. Refraction still reads against it — that is the only thing the backdrop
  was ever for — and the scene no longer spins, so nothing turns edge-on.
- `Samples~` is 47 MB → 487 KB on disk; the packed tarball drops from about 56 MB to about 13 MB,
  the remainder being `Runtime/Textures` (12.7 MB compressed), which is the product itself.

## [2.0.1] - 2026-09-10

Visual fidelity: 2.0.0 rendered correctly but did not read as rain. Four independent regressions
introduced by the URP port, plus the sample presentation around them. No API changes; no serialized
field was renamed or retyped.

### Fixed

- **Friction trails fell on a plumb line.** The lateral scan derived its candidate fan from one scan
  iteration instead of from the frame's downward advance, so the whole fan fitted inside a third of a
  `FrictionField` texel, every candidate scored the same, and the drop carried straight on. The fan is
  now `distance * LateralStepFactor` — legacy's "lateral half-span == downward advance" — and an
  all-equal candidate set is answered by a random pick when a field is assigned, as 1.x did. A layer
  with no field still falls straight.
- **Flow trails ran vertically.** The sideways wobble was applied as an absolute offset from the spawn
  point and reset on every re-roll, so it was bounded near `0.01 * LateralAmplitude`. It now
  accumulates into a persistent per-trail offset and random-walks across re-rolls, matching 1.x at the
  60 fps reference the migration constants were converted against, without 1.x's frame-rate dependence.
- **Drops read as composited cut-outs rather than refraction.** The baked coverage masks were binary —
  two hard thresholds, a 2 px dilation and one 3x3 box pass — which on a 128 px source is a one-pixel
  edge around a silhouette grown past the artwork. Coverage is now a continuous `smoothstep` ramp over
  normal magnitude and overlay luminance, softened by a gaussian proportional to the mask size, with no
  dilation. All ten shipped masks were re-baked. The D9 stacked-layer behaviour was re-validated on a
  captured frame: layers still do not erase the rain drawn underneath them.

### Changed

- **Trail ribbons taper and carry a head.** `EmitRibbon` now appends one quad at the leading end of
  every ribbon, and the default `TrailWidth` for new Flow and Friction layers falls off from head to
  tail instead of being a constant. The shipped profiles already authored their own width curve and are
  unchanged. Mesh capacity grew by four vertices and six indices per trail to match.
- **`LateralStepFactor` default `0.375` → `1`**, and the same value in every shipped friction profile
  (`Rain1`, `Rain3`, `Rain5`, `Rain6`, `MobileRain1`, `MobileRain3`, `BloodFlow`, `WaterSplashOut`,
  `Bench_Combined`, `Bench_Trails`). The field now means "sideways reach as a multiple of the per-frame
  fall", so it is the knob for tuning how far a trail wanders.
- **The Basic Rain preset generates plausible drops.** `Assets → Create → Rain Drop Effect → Rain
  Profile (Basic Rain)` previously produced identically sized, identically oriented, motionless drops
  at full opacity from their first frame — a stamped pattern rather than rain. It now authors a size
  spread, `AutoRotate`, a fade-in, a grow-then-hold size curve and a non-zero drift.
- **The Basic sample has a backdrop.** `Samples~/Basic` gained a still-life picture on a full-frame
  quad, because refraction is only legible against something with detail in it. The checkered ground
  and transparent cube are unchanged.

## [2.0.0] - 2026-09-10

The URP rewrite of Rain Drop Effect 2, and the first release of this package. Nothing of the original
Built-in-pipeline code survives; the prefab converter and the migration guides that carried the port
were removed in 2.0.2, once nobody needed them any more.

### Added

- **A URP Render Graph renderer.** One `RainRendererFeature` and one `RainRenderPass` at
  `BeforeRenderingPostProcessing`: a single scene-colour copy per camera, an optional shared
  half-resolution blur, and the drops drawn into the camera attachment. Nothing is enqueued when
  nothing is visible.
- **Profiles instead of prefabs.** `RainProfile` is an immutable asset holding four arrays of layer
  settings — static overlays, droplets, flowing trails and friction-guided trails. One profile can
  back any number of cameras; several `RainEffect` components can share one camera.
- **Quality tiers.** `Low`, `Balanced` and `High` cap drop, trail and point counts and switch the
  blur off at `Low`. Measured budgets are in `docs/performance.md`.
- **A bounded, allocation-free simulation.** Seeded RNG, a fixed trail ring, batched quad and ribbon
  geometry into one reusable mesh per layer. GC delta is 0 per frame on Mono and on IL2CPP/WebGL.
- **Baked data instead of runtime reads.** Coverage masks per texture pair and a `FrictionField`
  asset, both baked in the editor, so no texture is read back at runtime.
- **Editor tooling.** Inspectors for the component and the profile, an edit-mode preview, the
  coverage and friction bakers, texture policy menus, a reference audit, a missing-script audit and
  a shader variant report.
- **Two samples.** *Basic Setup* and *Effects Gallery* — every preset switchable at runtime, the
  blood HP demo, the splash and frozen controls, and a camera prefab per converted preset.
- **Fifteen converted presets** plus three `Bench_*` stress profiles.

### Changed

- `Alpha` → `RainEffect.Intensity`; `StopImmidiate()` → `Clear()`; `Refresh()` → `Clear()` or
  `Rebuild()` depending on what you meant.
- Sizes, offsets and speeds are screen-normalized (`1.0` = half the viewport height) rather than
  world units at a fixed camera distance.
- Drift, fall and friction advance as per-second rates, so playback no longer depends on frame rate.
  A frame longer than 0.1 s is clamped, so a backgrounded tab does not deliver a burst on resume.
- `Blur` is the weight the blurred sample is mixed in with, not a kernel width.
- Overlapping drops refract the same undistorted scene; the recursive `GrabPass` overlap is gone.
- The blood HP logic moved out of the runtime into the Effects Gallery sample, where game logic
  belongs.

### Removed

- The Built-in Render Pipeline path, `GrabPass`, and every 1.x shader.
- `RainCameraController` and the behaviour components; prefabs per preset.
- VR prefabs and any XR support.
- Shader lookup by name and any `Resources.Load` from the runtime.

## [2.0.0-pre.1] - Unreleased

### Added

- Initial URP package skeleton: Runtime, Editor and Tests assemblies, opt-in samples.
