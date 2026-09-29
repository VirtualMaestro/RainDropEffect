# Renderer Decision Record

Evidence and decisions from the Phase 2 rendering spike (Tasks 5-8) of the URP 6.3 migration.
Editor `6000.3.23f1`, URP `17.3.0`, Windows D3D11, Gamma color space, game view 1280x720.

## Summary

**Decision: keep direct batched refraction.** Every visible rain layer is drawn as batched geometry
that samples one stable scene-color copy per camera per frame. The distortion-field alternative
(accumulate per-drop normals into an offset buffer, then one full-screen resolve) is **not** built:
the spike renders three overlapping lens quads plus a full-screen frozen layer and an overlay layer
in one copy, one optional half-resolution blur and one raster pass, and nothing in the measured
behaviour suggests the batched path is the bottleneck. Per decision D16 the field variant stays a
conditional optimisation that only Task 25's measurements can trigger.

The pass topology of D8 holds for single cameras and is validated by M1-M6, M9 and M10. It does
**not** hold for camera stacks: see the Matrix results and open question **Q8** in
`.ai-factory/plans/URP6.3/index.md`.

Three defects in the plan's code sketch were found and fixed during the spike (see API notes): the
vertex attribute order, the render-target vertical flip, and a duplicate sampler declaration. The
first two would have silently produced wrong output rather than a compile error, which is exactly
what this spike existed to catch.

## API notes

Deviations from the plan's sketches required by the locked URP patch (17.3.0), by Unity's mesh API,
or by the actual asset content. Pass topology (D8) is unchanged.

| Where | Planned | Actual | Why |
| --- | --- | --- | --- |
| `RainVertex` field and descriptor order | `Position, UV, Tint, Params` | `Position, Tint, UV, Params` | Unity lays vertex attributes out in `VertexAttribute` enum order (Color = 3 before TexCoord0 = 4) whatever order the descriptors are given in. With the planned order every fragment read `uv.x` from the UV's y component and `uv.y` from the tint bytes: all layer textures sampled as if constant, so drops rendered as flat rectangles instead of drop silhouettes. Semantics and shader inputs are unchanged. |
| `RainCommon.hlsl` clip position | `float4(x / aspect, y, 0, 1)`, "no flip" | `float4(x / aspect, y * _ProjectionParams.x, 0, 1)` | Orientation check: rendering into an intermediate texture flips the target vertically, so `y = +0.5` appeared below the centre. See Orientation. |
| `RainLens.shader` | declares `SAMPLER(sampler_LinearClamp)` | declaration removed | `sampler_LinearClamp` is one of URP's predefined global samplers (`Core.hlsl`); redeclaring it fails with `redefinition of 'sampler_LinearClamp'` (D3D). |
| `RainRenderPass` blur texel | `1 / desc.width`, with a note to use `cameraData.scaledWidth/Height` when `sizeMode == Scale` | always `cameraData.scaledWidth/Height` | The blur source is the full-resolution camera color; the scaled camera size is correct for both size modes and needs no branch. |
| `CoverageMaskBaker` | `TextureImporter.singleChannelComponent` | `TextureImporterSettings.singleChannelComponent` via `ReadTextureSettings`/`SetTextureSettings` | The property does not exist on `TextureImporter` in 6000.3. |
| `CoverageMaskBaker` | coverage from normal deviation off flat blue (`> 0.06`) | coverage from "normal carries data" (`max(r,g,b) > 0.02`) | The shipped normal maps have a pure black background, not flat blue: flat-blue pixels measured 0.0% across all ten pairs. The planned test marks every pixel as covered (100% for all ten masks). |
| Spike scene | copy of `Assets/Baseline/Baseline.unity` | new URP/Lit backdrop in `Assets/Spike/Spike.unity` | The baseline scene's Built-in Standard and Legacy materials render magenta under URP and Task 3 step 7 forbids converting them. A magenta backdrop proves nothing about refraction or transparents. |
| `RainEffect` internals | `InternalsVisibleTo` for the two test assemblies | plus `Assembly-CSharp` | The spike driver lives in `Assets/Spike` and writes `RainRenderData` directly. Removed with the host scaffolding in Task 29. |

Everything else in Task 5's sketch compiled unchanged: `TextureDesc.msaaSamples` / `depthBufferBits`,
`RenderGraph.AddBlitPass` (both overloads), `RenderGraphUtils.BlitMaterialParameters`,
`RasterCommandBuffer.DrawMesh`, `UniversalResourceData.isActiveTargetBackBuffer` and
`resourceData.cameraColor` assignment.

## Orientation

Evidence: `docs/images/spike-orientation.png` (batch A only: two overlapping drops at the centre
plus one quad at `(-0.5, +0.5)` rotated 0.7 rad).

Before the fix the rotated quad appeared **lower**-left: the image was mirrored vertically because
the camera renders into an intermediate texture. The fix is the one the plan lists first — multiply
clip-space `y` by `_ProjectionParams.x` in `RainVert` and keep `GetNormalizedScreenSpaceUV` for the
scene lookup. Refraction is not mirrored: `n.y` needs no flip.

After the fix the rotated quad is upper-left and rotated counter-clockwise, and the refracted
content inside each drop follows the normal map. **Recorded decision: apply `_ProjectionParams.x` in
the vertex shader; do not flip `n.y`.**

## Coverage

Baked by `Rain Drop Effect/Bake Coverage Masks` (Task 8) into
`Packages/com.virtualmaestro.raindropeffect/Runtime/Textures/Coverage/`.
"Covered" is the mean mask value; "any" is the fraction of non-zero pixels, which shows how far the
2 px dilation and the 3x3 softening reach.

| Mask | Size | Covered | Any | Note |
| --- | --- | --- | --- | --- |
| `rain_drop_coverage` | 128 | 67.4% | 70.1% | above the plan's expected 5-60% band; the source is a dense cluster of drops (raw silhouette 61.7%) and 2 px dilation is ~3% of a 128 px texture |
| `rain_frame_coverage` | 1024 | 18.9% | 21.1% | |
| `flow_coverage` | 128 | 100.0% | 100.0% | flagged (>99%): the trail texture fills its quad; the ribbon geometry itself is thin, so this is expected |
| `bubble_coverage` | 256 | 59.5% | 60.9% | |
| `wash_coverage` | 512 | 79.3% | 80.6% | |
| `rain_white_coverage` | 32 | 35.7% | 44.5% | |
| `frozenfill_coverage` | 1024 | 100.0% | 100.0% | expected: full-frame overlay (plan requires > 80%) |
| `frozenframe_coverage` | 1024 | 61.4% | 62.2% | |
| `bloodframe_coverage` | 1024 | 37.1% | 39.0% | |
| `bloodsplatter_coverage` | 512 | 29.9% | 33.9% | |

Bake constants: normal-active threshold `0.02`, overlay-luminance threshold `0.03`, dilation 2 px
(two 3x3 max passes), one 3x3 box blur, target size `min(1024, max(width, height))`.

**Overlap check** (`docs/images/spike-overlap.png`, same frame as the orientation capture): the two
centre quads overlap by roughly half their width. Outside the upper quad's coverage mask the lower
drop's refraction is still visible, and inside the upper drop the refraction is computed from the
same undistorted scene copy — the stable-source rule of D9/ADR-0002. There is no recursive
refraction of already-refracted pixels; that is accepted behaviour, not a defect.

Legacy note that motivates the masks: the legacy lens shader has no `Blend` and writes `ZWrite On`
with alpha 1, so a legacy drop quad is opaque over its whole area. Reproducing that literally would
let each new drop erase the drops beneath it, which is exactly what D9's coverage channel prevents.

## Matrix results

Automated: each case captures one frame with the spike driver disabled and one with it enabled, and
reports the mean absolute RGB difference over the whole frame. A case passes when the difference is
above 0.5/255, which cannot happen unless rain actually rendered. Raw results:
`docs/images/spike-matrix.txt`; per-case captures: `docs/images/spike-m*.png`.

| Case | Setting | Result | Evidence |
| --- | --- | --- | --- |
| M1 | MSAA off, HDR off, render scale 1 | PASS | mean diff 5.04/255 |
| M2 | MSAA 4x | PASS | mean diff 5.03/255; no MSAA sample-mismatch errors (the copy resolves) |
| M3 | HDR on | PASS | mean diff 5.01/255; the copy inherits the camera color descriptor |
| M4 | Render scale 0.5 | PASS | mean diff 4.98/255; proportions correct (aspect comes from `_ScreenParams`) |
| M5 | Post-processing off on the camera, Intermediate Texture = Auto | PASS | mean diff 5.04/255; no `renders to the backbuffer` warning was logged, so `requiresIntermediateTexture = true` alone is enough on this renderer |
| M6 | Camera `targetTexture` 1280x720 | PASS | mean diff 5.04/255, measured on the RenderTexture itself |
| M7 | Base + Overlay camera stack, rain on the Base camera | **FAIL** | mean diff 0.00/255 — no rain at all. See below |
| M8 | Scene View with `PreviewInSceneView` | MANUAL | the Scene View cannot be captured from Play Mode; the code path is exercised by `RainRenderPass.Collect` and is re-checked by hand in Task 30 |
| M9 | Orthographic camera | PASS | mean diff 4.69/255, identical appearance (the effect is screen-space) |
| M10 | Screen Space Overlay UI | PASS | mean diff 5.03/255; the UI text stays sharp because the overlay canvas is drawn after the pass |
| OVERLAY | Overlay-only frame (no scene copy) | PASS | mean diff 0.82/255 drawing straight into `activeColorTexture` |

**M7 detail.** With a stack, `resourceData.cameraColor = target` is not honoured: the Overlay
cameras continue to render into URP's original camera-color attachment, so the copy that carries
the rain is discarded. This is recorded as open question **Q8** in the plan index with two candidate
fixes; the smaller one inverts the topology (copy into a read-only source, draw into
`activeColorTexture`, never reassign `cameraColor`) at the same cost of one copy. It must be decided
before Task 13.

Two further findings from the overlay case, both faithful to legacy semantics rather than bugs:
overlay alpha is `tint.a * tex.r*tex.g*tex.b * opacity`, so only near-white overlay textures produce
visible alpha (`rain_drop` and `frozenframe` are almost invisible in that pass; `frozenfill` is
clearly visible), and the overlay pass performs no scene copy at all.

## Platform results

| Target | Status |
| --- | --- |
| Windows editor, D3D11 | verified, all captures above |
| WebGL2 (Chrome) | **PASS** — `docs/images/spike-webgl2.jpg`. Built from `Assets/Spike/Spike.unity` (WASM, GLES3/WebGL2 graphics API, non-development), served over HTTP with `Content-Encoding: gzip`, run in Chrome. Drops, refraction, coverage silhouettes, the transparent quad and the overlay batch all render as on D3D11. The build reported exactly one shader error, and it is a legacy Built-in shader (`RainDrop/Internal/RainDistortion (Deffered)`: too many texture interpolators); both package shaders compiled clean. |
| Android GLES3 | GAP: not built in Phase 2 |
| Android Vulkan | GAP: not built in Phase 2 |
| iOS Metal | GAP: build module not installed on this machine (`BuildPipeline.IsBuildTargetSupported(iOS) == false`) |

## Measurements

| Metric | Value |
| --- | --- |
| Passes added per camera with rain visible (lens) | 3: `Rain Copy Scene Color`, optional `Rain Blur Scene Color`, `Rain Draw` |
| Passes added per camera, overlay only | 1: `Rain Draw` |
| Passes added with no `RainEffect` on the camera | 0 (the feature returns before `EnqueuePass`) |
| Draw calls for the spike content | 3 (one per batch: lens drops, frozen frame, overlay) |
| GPU ms per pass, Windows D3D11 1920x1080 | GAP: not measured in Phase 2 |
| WebGL2 frame time with/without rain | GAP |
| Android frame time with/without rain | GAP |

The GPU measurements are owned by Task 25, which defines the quality presets and the measured
budgets; nothing in Phase 2 depends on them. The pass counts above are what the topology
guarantees by construction and are visible in the Render Graph Viewer.

## Gaps

- Frame-time measurement on WebGL2 is still a GAP (Task 31); rendering correctness is verified.
- **GAP:** Android GLES3 and Vulkan device runs. Module installed, no device validated here.
  Task 31.
- **GAP:** iOS Metal. Build module not installed on this machine. Task 31.
- **GAP:** per-pass GPU timings. Task 25.
- **OPEN:** camera stacks (M7) — plan open question Q8, must be answered before Task 13.
- **MANUAL:** Scene View preview (M8) — verified by hand, not by an automated capture.
