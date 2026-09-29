# Rain Drop Effect URP

Camera-space rain, frozen, blood and splash lens effects for Unity 6.3+ with the Universal Render
Pipeline.

This repository is the development home of the package. The thing you install is
[`Packages/com.virtualmaestro.raindropeffect`](Packages/com.virtualmaestro.raindropeffect); the rest
of the repository is the host project that builds, measures and validates it.

```csharp
var rain = camera.GetComponent<RainEffect>();
rain.Play();
```

## Start here

| | |
| --- | --- |
| Package overview, install, quick start | [`Packages/com.virtualmaestro.raindropeffect/README.md`](Packages/com.virtualmaestro.raindropeffect/README.md) |
| API and setup reference | [`Documentation~/index.md`](Packages/com.virtualmaestro.raindropeffect/Documentation~/index.md) |
| Verified editors, pipelines and platforms | [`docs/support-matrix.md`](docs/support-matrix.md) |
| Measured budgets | [`docs/performance.md`](docs/performance.md) |
| Executed release evidence | [`docs/release-report.md`](docs/release-report.md) |
| What changed per version | [`CHANGELOG.md`](Packages/com.virtualmaestro.raindropeffect/CHANGELOG.md) |
| Project map for contributors and agents | [`AGENTS.md`](AGENTS.md) |

## What this is

- Four layer families — static overlays, droplets, flowing trails and friction-guided trails.
- A single Render Graph pass per camera, injected before post-processing, with one scene-colour copy
  and an optional shared half-resolution blur.
- No managed allocation per frame after the first, measured on Mono and on IL2CPP/WebGL.
- Deterministic under a seed; several effects per camera; one profile shared by any number of cameras.

## What this is for

- **Weather seen through glass** — a windscreen, a visor, a porthole, a security camera in the rain.
  The effect is camera-space, so it costs the same whether the world behind it is a corridor or an
  open valley.
- **Damage and status feedback** — blood on the lens when the player is hit, frost creeping in on a
  cold level, a splash when they surface. Every one of those is a profile plus one `Play()` call, and
  several can sit on the same camera and play independently.
- **Mood on a budget** — under a tenth of a millisecond of GPU time per frame at every quality tier,
  nothing allocated per frame after the first, and nothing enqueued at all when nothing is visible.
  That is what makes it usable on mobile and in WebGL, not only on a desktop GPU.
- **Look development you can iterate on** — a profile is a `ScriptableObject` with animation curves,
  editable live in the Game view without entering Play Mode. Twelve presets ship as starting points.

## Credits

This package began life as a rewrite of **Rain Drop Effect 2**, the Built-in-pipeline asset of the
same name. None of its code survives — the pipeline integration, the simulation and the shaders were
written from scratch for URP's Render Graph — but the artwork, the feel of the presets and the idea
of layered rain families all come from there. Thanks to the original for the starting point.

## License

MIT. See [`LICENSE`](LICENSE) and the package's `LICENSE.md`.
