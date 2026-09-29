# Rain Drop Effect URP

Camera-space rain, frozen, blood and splash lens effects for Unity 6.3+ with the Universal Render
Pipeline. The effect runs as a single Render Graph pass injected before post-processing, driven by a
`RainEffect` component on the camera and one immutable `RainProfile` asset per look.

```csharp
var rain = camera.GetComponent<RainEffect>();
rain.Play();
```

- Four layer families — static overlays, droplets, flowing trails and friction-guided trails.
- One shared half-resolution blur per camera, allocated only when a visible layer asks for it.
- No managed allocation per frame after the first, at any supported layer count — measured on Mono
  and on IL2CPP/WebGL.
- Deterministic under a seed, so a run reproduces exactly.
- Several effects per camera, one profile shared by any number of cameras.
- Nothing is enqueued at all when nothing is visible.

## Install

Pick one:

- **Git URL** — Package Manager → *Install package from git URL…*, then paste:

  ```
  https://github.com/VirtualMaestro/RainDropEffect.git?path=Packages/com.virtualmaestro.raindropeffect#v2.0.2
  ```

  The package is not at the repository root, so the `?path=` part is required; the `#v2.0.2` pins a
  tag — drop it to track the default branch, or change it to move between versions.
- **Tarball** — Package Manager → *Install package from tarball…* → the `.tgz` attached to the
  matching [GitHub release](https://github.com/VirtualMaestro/RainDropEffect/releases).
- **Embedded** — copy `Packages/com.virtualmaestro.raindropeffect/` into your project's `Packages/`.

Then add the renderer feature once per URP renderer asset. Select any camera with a `RainEffect`: the
inspector notices the missing feature and offers **Add Rain Renderer Feature**. You can also add
*Rain Renderer Feature* by hand under *Renderer Features* on the renderer asset.

The package depends on `com.unity.render-pipelines.universal` and nothing else.

## Quick start

1. Add `Rain Effect` to your camera (*Add Component → Rendering → Rain Effect*).
2. Assign a profile — the shipped ones live in `Runtime/Profiles` (`Rain1`–`Rain6`, `Frozen`,
   `BloodFrame`/`BloodFlow`/`BloodSplatter`, `WaterSplashIn`/`Out`, `MobileRain1`–`3`).
3. Pick a `Quality`: `Low`, `Balanced` or `High`. It caps drop, trail and point counts and switches
   the blur off at `Low`.
4. `Play()`, `Stop()`, `Clear()`, `Rebuild()` from your own code. `Intensity` fades the whole effect;
   `Wind` and `Gravity` push it.

## Supported versions

| | |
| --- | --- |
| Minimum editor | 6000.3 |
| Verified | 6000.3.23f1 (Windows D3D11 editor and player, WebGL2/Chrome) and 6000.5.10f1 with URP 17.5 (Windows player) |
| Render pipeline | URP only — no Built-in, no HDRP |
| Not supported | XR/VR, world-space glass |
| Untested | Android, iOS — see the gaps below |

Full detail, including every measured platform and every named gap, is in the repository's
`docs/support-matrix.md` and `docs/release-report.md`.

## Performance

Measured on a Development player, ten scenarios × 600 frames (Windows 11, i9-14900HX, RTX 4080
Laptop, D3D11, 1280 × 720). The effect's own cost against a no-rain frame:

| Tier | GPU (Δ p50) | CPU (Δ p95) | GC |
| --- | --- | --- | --- |
| Low | +0.057 ms | +0.113 ms | 0 |
| Balanced | +0.060 ms | +0.135 ms | 0 |
| High | +0.062 ms | +0.144 ms | 0 |

In Chrome (WebGL2) every scenario held the 60 Hz refresh with no dropped frame, and GC delta stayed
0. Numbers, method and the render-target budget are in `docs/performance.md`.

## Camera policy

- Put the `RainEffect` on the **Base** camera. Overlay cameras render into the same attachment and
  the rain is drawn there; an effect on an Overlay camera is not served.
- Preview cameras and reflection probes never render rain.
- The Scene View renders it only when a component opts in with `PreviewInSceneView`.
- Each camera keeps its own simulation state; one profile can back any number of cameras.

## Verbose logging

The runtime logs quietly by default. Add the scripting define `RAINDROP_VERBOSE_LOG` to see every
rebuild, lifecycle transition and clamped delta time. Warnings and errors are always on.

## Documentation

- `CHANGELOG.md` — what changed per version. 2.0.1 is a visual-fidelity release: trail meander,
  ribbon taper and head, softer drop silhouettes, and a Basic preset that varies its drops.
- `Documentation~/index.md` — installation, setup, the API and the quality tiers.
- In the repository: `docs/support-matrix.md`, `docs/performance.md`, `docs/release-report.md`.

## Samples

- **Basic Setup** — one camera, one profile, a still-life backdrop, a checkered ground plane and a
  transparent cube, so the lens distortion and the blur are both easy to see.
- **Effects Gallery** — every preset switchable at runtime against a textured backdrop, plus a
  camera prefab per preset (blood, splash and frozen included) for dropping straight into a scene.

## License

MIT. See `LICENSE.md`. Original Rain Drop Effect 2 by VirtualMaestro; this is its URP rewrite.
