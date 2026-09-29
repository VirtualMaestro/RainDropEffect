# ADR-0002: Portable CPU simulation and batched raster refraction

Status: proposed
Date: 2026-09-09
Research: [INDEX.md](INDEX.md)
Decision ID: DEC-02

## Context

The source has four effect families with useful authored curves/textures, but rendering uses per-drop geometry/material objects and legacy GrabPass. Flow/Friction trails allocate mesh arrays; friction selection builds collections and samples a readable texture on the CPU. These source-level findings are E02–E08/E15 in [RESEARCH.md](RESEARCH.md); no measured CPU/GPU bottleneck ordering is asserted.

Web is a required target. Unity 6.3's verified manual marks WebGPU Experimental and identifies WebGL2 as the default Web API. Therefore a compute-required design would narrow the requested product before hardware/browser requirements have even been established. Exact old pixels are also not a safe assumption: Forward unnamed grabs can include earlier effects, while Mobile already uses a shared named grab.

## Decision

Prefer bounded CPU simulation and reusable batched quad/ribbon geometry, submitted by a custom URP RendererFeature through Render Graph. Preserve family behavior and authored settings while replacing the per-drop GameObject/material/controller graph. A component on the consumer camera owns playback and per-camera state; profiles remain immutable shared data.

Default injection is after transparents and before post-processing. Use a sampleable current scene color C0 and a distinct destination C1. Copy C0 to C1 once, draw ordered compatible batches into C1 sampling C0, then supply C1 as URP camera color. Prototype a combined raster copy+draw pass when supported; otherwise use one copy and one draw pass. Never read a render attachment as the refraction source, assume the backbuffer is sampleable, or add an unnecessary copy-back. Resolve/format handling and Base+Overlay camera ownership must be proved in P1.

Use shared scene blur only when a visible profile requests it; disable it at Low quality. Overlay-only work should avoid a scene snapshot if distortion and blur are absent. A visually inactive effect performs no rain rendering/resource work. Stable draw order and per-camera pass-data ownership matter more than blindly minimizing batch count.

The preferred overlap rule is explicit: all rain layers refract the same undistorted C0 during that effect application. They may blend tint/coverage into C1, but do not recursively sample previous rain. This is a visual change requiring acceptance. Blur shape is also a comparison gate because the source uses a horizontal normal-dependent kernel.

Explicit coverage is required: copying the old opaque quad output while sampling shared C0 could erase earlier effects in flat-normal rectangular regions. P1 establishes the blend convention and validates overlapping quads; texture audit/conversion must provide a usable coverage mask, without assuming normal magnitude defines the shape. Local coverage and preserved camera alpha are separate contracts. See RESEARCH §4 for the conversion consequence.

Do not require Jobs/Burst, compute, indirect rendering, instancing, an atlas, a distortion-field buffer or a general backend abstraction. Add a measured optimization only when it improves required device budgets without unacceptable visual/correctness cost.

## Alternatives considered

| Option | Benefits | Costs / risks | Disposition |
|---|---|---|---|
| URP shader port retaining per-drop objects | Fastest visual prototype | Preserves object/material costs and complicated ordered copies | Useful only as a bounded comparison prototype, not final architecture |
| Native Full Screen Pass + one shader | Minimal implementation for a static overlay | Does not by itself provide the four simulation families, ordered ribbon batches or camera ownership | Use for a spike/reference or if a truly static-only use case is split out; insufficient alone for this library |
| CPU + batched direct refraction | Portable; preserves arbitrary authored masks, tint and ribbon shape; fewer new GPU resources | Full-resolution overdraw remains; group count depends on texture/order; stable-source overlap differs from legacy | Preferred starting production design |
| Distortion-field raster + fullscreen composite | Potential benefit for dense scenes and low-resolution distortion | Extra RT bandwidth; field encoding needs distortion, coverage, tint/relief/darkness/blur semantics; overlap changes; one RGBA buffer cannot be assumed sufficient | P1/P5 comparison only when high-density measurements justify it; select instead of maintaining two default backends |
| Procedural fullscreen rain | Very small CPU/object footprint | Loses authored emitter/friction/trail behavior unless substantially rebuilt | Not equivalent to the requested migration |
| Compute/GPU-driven simulation | Potential high particle counts and low CPU update cost | WebGL2 portability loss, additional buffers/synchronization and device variance | Not baseline; separate justified extension if requirements change |

## Consequences and validation gates

The new runtime has a single renderer and clear transient-resource ownership. Batching reduces object/material overhead, but it is not a claim of one draw for every possible profile or automatic SRP Batcher acceleration. Refraction still consumes framebuffer bandwidth; Render Graph merging is conditional, not a zero-cost promise.

P1 must show real WebGL2 and native rendering with transparent content, sampleable color under Intermediate Texture=Auto, correct MSAA/HDR/viewport behavior and a usable camera-stack policy. P3/P4 must prove no warmed library GC, bounded trail state and correct playback. P5 must compare overlap/blur, device timings and RT memory before accepting quality presets. P7 must validate the release matrix on real devices.

The field/composite alternative replaces this decision only after its channel representation, blend semantics and actual bandwidth win are demonstrated. Strict recursive-overlap parity or XR requirements are explicit revisit triggers. No renderer implementation has been created by this research.

## Evidence

- [RESEARCH.md](RESEARCH.md), E02–E08/E15, §4–§8, REQ-02/04/05 and RISK-01/02/04/05.
- [Unity 6.3 Render Graph introduction](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/render-graph-introduction.html).
- [Unity 6.3 blit guidance](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/customize/blit-overview.html).
- [Unity 6.3 avoid copy-back](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/render-graph-blit.html).
- [Unity 6.3 WebGPU status](https://docs.unity3d.com/6000.3/Documentation/Manual/WebGPU.html).
