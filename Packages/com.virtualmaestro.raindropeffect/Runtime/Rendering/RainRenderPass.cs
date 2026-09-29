using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace RainDropEffect
{
    /// <summary>
    /// The single URP integration pass (decisions D6-D9).
    ///
    /// Topology, fixed for 2.0.0 (revised by Q8): when any lens layer is visible the camera color is
    /// copied once into a read-only C0, an optional half-resolution blur of the same source is
    /// produced, and all batches are drawn into the camera's own active color attachment while
    /// sampling C0. Overlay-only frames draw straight into the active color and copy nothing.
    /// No pass ever samples its own attachment and nothing is copied back.
    ///
    /// The attachment is never swapped: assigning <c>resourceData.cameraColor</c> is ignored when the
    /// Base camera has a camera stack, because the Overlay cameras keep rendering into the original
    /// attachment and the copy carrying the rain is dropped. Drawing into the attachment and reading
    /// from the copy costs the same single copy and needs no special case for stacks.
    /// </summary>
    internal sealed class RainRenderPass : ScriptableRenderPass
    {
        class DrawData
        {
            public List<RainBatch> Batches;
            public TextureHandle Source;
            public TextureHandle Blur;
            public MaterialPropertyBlock Mpb;
        }

        readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        readonly List<RainBatch> frameBatches = new List<RainBatch>(16);
        readonly Material blurMaterial;

        public RainRenderPass(Material blur)
        {
            blurMaterial = blur;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            requiresIntermediateTexture = true;
            profilingSampler = new ProfilingSampler("Rain Drop Effect");
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var resources = frameData.Get<UniversalResourceData>();

            if (!Collect(cameraData, frameBatches, out var anyLens, out var anyBlur, out var blurRadius))
            {
                return;
            }

            if (resources.isActiveTargetBackBuffer)
            {
                RainLog.WarnOnce("backbuffer:" + cameraData.camera.name,
                    $"Camera '{cameraData.camera.name}' renders to the backbuffer; rain skipped. " +
                    "Enable an intermediate texture (post-processing, HDR, MSAA or Intermediate Texture=Always).",
                    cameraData.camera);
                return;
            }

            // The attachment we draw into. Never replaced; see the topology note above.
            var target = resources.activeColorTexture;

            // What the lens samples: a copy taken before anything is drawn.
            var source = TextureHandle.nullHandle;
            var blur = TextureHandle.nullHandle;

            if (anyLens)
            {
                var desc = renderGraph.GetTextureDesc(target);
                desc.name = "_RainSceneColorCopy";
                desc.msaaSamples = MSAASamples.None;
                desc.depthBufferBits = DepthBits.None;
                desc.clearBuffer = false;
                desc.bindTextureMS = false;
                source = renderGraph.CreateTexture(desc);
                renderGraph.AddBlitPass(target, source, Vector2.one, Vector2.zero, passName: "Rain Copy Scene Color");

                if (anyBlur && blurMaterial != null)
                {
                    var blurDesc = desc;
                    blurDesc.name = "_RainSceneBlur";
                    if (blurDesc.sizeMode == TextureSizeMode.Scale)
                    {
                        blurDesc.scale *= 0.5f;
                    }
                    else
                    {
                        blurDesc.width = Mathf.Max(1, blurDesc.width / 2);
                        blurDesc.height = Mathf.Max(1, blurDesc.height / 2);
                    }

                    blurDesc.filterMode = FilterMode.Bilinear;
                    blur = renderGraph.CreateTexture(blurDesc);

                    // Texel size of the blur source, which is the full-resolution camera color. A
                    // scale-mode descriptor carries no pixel size of its own, so the camera's
                    // scaled size is the only place to get it; a fixed-size one already knows.
                    int width;
                    int height;
                    if (desc.sizeMode == TextureSizeMode.Scale)
                    {
                        width = Mathf.Max(1, cameraData.scaledWidth);
                        height = Mathf.Max(1, cameraData.scaledHeight);
                    }
                    else
                    {
                        width = Mathf.Max(1, desc.width);
                        height = Mathf.Max(1, desc.height);
                    }
                    blurMaterial.SetFloat(RainShaderIds.BlurRadius, blurRadius);
                    blurMaterial.SetVector(RainShaderIds.BlurTexel,
                        new Vector4(1f / width, 1f / height, width, height));

                    renderGraph.AddBlitPass(
                        new RenderGraphUtils.BlitMaterialParameters(target, blur, blurMaterial, 0),
                        "Rain Blur Scene Color");
                }
            }

            using (var builder = renderGraph.AddRasterRenderPass<DrawData>("Rain Draw", out var data, profilingSampler))
            {
                data.Batches = frameBatches;
                data.Source = source;
                data.Blur = blur;
                data.Mpb = mpb;

                builder.SetRenderAttachment(target, 0, AccessFlags.ReadWrite);
                if (anyLens)
                {
                    builder.UseTexture(source, AccessFlags.Read);
                    if (blur.IsValid())
                    {
                        builder.UseTexture(blur, AccessFlags.Read);
                    }
                }

                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (DrawData d, RasterGraphContext ctx) =>
                {
                    d.Mpb.Clear();

                    // TextureHandle -> RTHandle -> Texture needs two explicit steps; C# applies
                    // only one implicit conversion.
                    if (d.Source.IsValid())
                    {
                        RTHandle source = d.Source;
                        d.Mpb.SetTexture(RainShaderIds.SceneColor, source);
                    }

                    if (d.Blur.IsValid())
                    {
                        RTHandle blur = d.Blur;
                        d.Mpb.SetTexture(RainShaderIds.SceneBlur, blur);
                    }
                    else if (d.Source.IsValid())
                    {
                        // A material may carry the blur keyword while no blur texture exists this
                        // frame (the feature has no shader set, or quality turned it off after the
                        // material was built). Point it at the unblurred copy: sampling an unbound
                        // texture would paint the drop black instead.
                        RTHandle source = d.Source;
                        d.Mpb.SetTexture(RainShaderIds.SceneBlur, source);
                    }

                    for (var i = 0; i < d.Batches.Count; i++)
                    {
                        var batch = d.Batches[i];
                        if (batch.Mesh == null || batch.Material == null)
                        {
                            continue;
                        }

                        ctx.cmd.DrawMesh(batch.Mesh, Matrix4x4.identity, batch.Material, 0, batch.ShaderPass, d.Mpb);
                    }
                });
            }
        }

        /// <summary>
        /// Gathers the batches that apply to this camera, in effect order. Returns false when
        /// nothing is visible, which is the fast path for an inactive effect.
        /// </summary>
        internal static bool Collect(UniversalCameraData cameraData, List<RainBatch> into,
            out bool anyLens, out bool anyBlur, out float blurRadius)
        {
            into.Clear();
            anyLens = false;
            anyBlur = false;
            blurRadius = 0f;

            var type = cameraData.cameraType;
            if (type == CameraType.Preview || type == CameraType.Reflection)
            {
                return false;
            }

            List<RainEffect> effects;
            if (type == CameraType.SceneView)
            {
                var source = RainEffect.SceneViewPreviewSource;
                if (source == null)
                {
                    return false;
                }

                // Allocation only in the Scene View, never in a player.
                effects = new List<RainEffect>(1) { source };
            }
            else if (!RainEffect.TryGet(cameraData.camera, out effects))
            {
                return false;
            }

            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect == null || !effect.isActiveAndEnabled)
                {
                    continue;
                }

                var renderData = effect.RenderData;
                if (renderData == null || !renderData.AnyVisible)
                {
                    continue;
                }

                into.AddRange(renderData.Batches);
                anyLens |= renderData.AnyLens;
                anyBlur |= renderData.AnyBlur;
                blurRadius = Mathf.Max(blurRadius, renderData.BlurRadius);
            }

            return into.Count > 0;
        }
    }
}
