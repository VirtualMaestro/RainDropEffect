# Effects Gallery

Every shipped preset on one camera, through the public API. Nothing here is required by the
package — the sample exists to be read and copied.

## Gallery.unity

One camera with one `RainEffect`. `GallerySample` swaps `RainEffect.Profile`, calls `Rebuild()` and
`Play()`; that is the whole preset mechanism, so a game needs no per-preset camera prefab.

| Button | Profile | Quality |
| --- | --- | --- |
| Rain1 … Rain6 | `Rain1`–`Rain6` | High |
| Frozen | `Frozen` | High |
| WaterSplashIn / WaterSplashOut | `WaterSplashIn`, `WaterSplashOut` | High |
| MobileRain1 … MobileRain3 | `MobileRain1`–`MobileRain3` | Low |

The `Low`/`Balanced`/`High` buttons rebuild the current preset at that quality. The Mobile presets
are not a separate renderer or a separate shader — they are ordinary profiles shown at
`Quality Low`, which is what the 1.x "mobile" prefabs amounted to.

`Stop` ends emission and lets the drops on screen finish; `Clear` removes everything immediately.

Behind the rain is a single textured quad (`Cottage.jpg` on `Backdrop.mat`). Refraction is only
legible against something with detail in it, and one quad carries that detail for a few hundred
kilobytes — the 1.x still life it replaces was an 18 MB FBX with nine textures, which every consumer
downloaded whether or not they ever imported the sample.

## Prefabs/

One camera prefab per converted 1.x preset, for projects that preferred dropping a prefab into a
scene. Each is a camera with a `RainEffect` (BloodRain has three, one per behaviour family) already
pointed at its profile. They reference profiles only — no sample script — so they work on their own.

Blood, splash and frozen have no scene of their own. Drop the matching prefab into a scene, or read
`Gallery.unity` and point a `RainEffect` at `BloodFrame`, `BloodFlow`, `BloodSplatter`,
`WaterSplashIn`, `WaterSplashOut` or `Frozen` yourself.

## Scripts/

No assembly definition: the sample compiles into the consumer's own assembly, so its code can be
edited in place. The scripts set no resolution, orientation or target frame rate — a sample must
not reconfigure the host game the way the 1.x demo scenes did.

- `GallerySample` — the preset switcher and the IMGUI menu around it.
- `FpsDisplaySample` — a frame-time readout, so a quality change is visible as a number.
