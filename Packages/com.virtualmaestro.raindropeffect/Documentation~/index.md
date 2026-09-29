# Rain Drop Effect URP

Camera-space rain, frozen, blood and splash lens effects for Unity 6.3+ URP (Render Graph).

## Installation

Package Manager → **Install package from git URL**:

```
https://github.com/VirtualMaestro/RainDropEffect.git?path=Packages/com.virtualmaestro.raindropeffect#v2.0.2
```

The package lives in a subfolder of the repository, so `?path=` is required; `#v2.0.2` pins a tag.
The same string works as a dependency value in `Packages/manifest.json`.

The package depends on `com.unity.render-pipelines.universal` 17.3.0 and pulls in nothing else — no
test framework, no editor-only assemblies, nothing from the host project. `tools/clean-install.ps1`
in the repository proves that on every change by building the package in a throwaway consumer project.

## Renderer feature setup

Select the URP renderer asset your camera uses and **Add Renderer Feature → Rain Renderer Feature**.
The feature owns the render pass; the `RainEffect` component only feeds it data.

A camera with the component but no feature on its renderer draws nothing. The inspector detects this
and offers a button that adds the feature for you.

The pass needs an intermediate colour texture. It requests one, but if URP still renders straight to
the backbuffer the effect logs a warning once and skips; enabling post-processing on the camera, or
setting **Intermediate Texture = Always** on the renderer, resolves it.

## Usage

Add **Rain Effect** to a camera, assign a `RainProfile`, and drive it from script:

```csharp
var rain = camera.GetComponent<RainEffect>();
rain.Play();    // start every layer
rain.Stop();    // stop emitting; drops already on screen finish
rain.Clear();   // remove everything at once
rain.Rebuild(); // re-read the profile after changing textures or layer counts
```

| Member | Meaning |
| --- | --- |
| `Profile` | The authored look. One asset can back any number of cameras. |
| `Intensity` | 0–1 master fade. At 0 the effect costs nothing: no copy, no blur, no draw. |
| `Wind` | Screen-space push in normalized units per second. |
| `Gravity` | World-space direction; projected into screen space every frame. |
| `Quality` | `Low`, `Balanced` or `High`. Caps drop and trail counts and gates the blur. |
| `Seed` | 0 reseeds every run; anything else reproduces a run exactly. |
| `IsPlaying`, `IsEmitting` | Whether anything is on screen, and whether new drops are still spawning. |

Several components may sit on one camera — the blood sample stacks three. Each keeps its own state, so
they play and stop independently.

### Profiles and layers

A `RainProfile` holds four arrays, one per layer family:

| Family | What it does |
| --- | --- |
| **Static** | One quad that fades in and out. Rain frames, frozen fill, blood frames. |
| **Simple** | Transient droplets that appear, drift and fade. |
| **Flow** | Drops that run down the glass leaving a ribbon trail, wobbling sideways. |
| **Friction** | Trails that steer along a baked friction field, so they follow the same channels each run. |

Layers draw in `(Depth, family, array index)` order, the first one at the bottom. Each layer picks
`Lens` mode (refracts the scene behind it) or `Overlay` mode (blends on top without sampling the scene).

Create a profile with **Assets → Create → Rain Drop Effect → Rain Profile**, or start from
**Rain Profile (Basic Rain)**, which gives you a rain frame plus falling droplets already wired up.

Add layers with the **Add** button in the profile inspector rather than Unity's array `+`: the built-in
one zero-initializes the element and you get a layer with no lifetime, no size and no emission.

## Quality

| Quality | Max drops | Max trails | Max trail points | Blur |
| --- | --- | --- | --- | --- |
| Low | 48 | 8 | 32 | off |
| Balanced | 128 | 16 | 64 | radius 4 |
| High | 256 | 32 | 96 | radius 6 |

Caps apply when a layer allocates, never per frame, so lowering the tier on a shipped profile costs
nothing and changes no authored data.

The blur is shared: one half-resolution pass per camera per frame, however many layers ask for it, and
only when a visible lens layer has `Blur > 0`. Render-target budgets and measured CPU cost are in
`docs/performance.md` in the repository.

## Conventions worth knowing

- Shaders are referenced through a serialized `RainShaderSet` asset, never by `Shader.Find` name.
- Sizes, speeds and offsets are fractions of the view, not world units at a fixed camera distance,
  so a profile looks the same at any resolution and aspect.
- Drift, fall and friction steps are per-second rates: playback does not depend on frame rate.
- `Clear()` wipes what is on screen; `Rebuild()` re-applies a changed profile, quality or seed.
- HP curves, damage pulses and similar are game logic and live in your own code, not in the runtime.

## Samples

Import from Package Manager → *Samples*.

- **Basic Setup** — one camera, one profile, a play/stop button. The smallest thing that runs.
  The scene puts a still-life picture, a checkered ground plane and a transparent cube behind the
  rain, because refraction is only legible against something with detail in it.
- **Effects Gallery** — `Gallery.unity` switches all twelve presets on one camera, against a single
  textured quad. Camera prefabs for every converted preset, blood and splash included, are in
  `Prefabs/`: each is a camera with a `RainEffect` already pointed at its profile.

Sample scripts carry no assembly definition, so they compile into your own assembly and can be edited
in place. They set no resolution, orientation or target frame rate.

## Performance

Measured budgets, the render-target table and the blur kernel are in `docs/performance.md`. The short
version: the effect costs well under a tenth of a millisecond of GPU time per frame at every quality
tier on a desktop GPU, allocates nothing per frame, and holds 60 fps in WebGL2.

## Support matrix

Verified editor, URP and platform combinations, clean-install build results and the remaining gaps are
tracked in `docs/support-matrix.md`; the executed release evidence is `docs/release-report.md`.
