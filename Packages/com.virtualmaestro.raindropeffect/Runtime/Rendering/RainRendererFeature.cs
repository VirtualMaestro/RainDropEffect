using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RainDropEffect
{
    /// <summary>
    /// Stateless renderer feature: it owns the blur material and enqueues the single rain pass for
    /// cameras that actually have a registered <see cref="RainEffect"/> (decision D6).
    /// </summary>
    [DisallowMultipleRendererFeature("Rain Drop Effect")]
    public sealed class RainRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] RainShaderSet shaders;

        RainRenderPass pass;
        Material blurMaterial;

        public override void Create()
        {
            if (blurMaterial != null)
            {
                CoreUtils.Destroy(blurMaterial);
                blurMaterial = null;
            }

            if (shaders == null)
            {
                RainLog.Error("RainRendererFeature has no RainShaderSet; blur disabled", this);
            }
            else if (shaders.Blur == null)
            {
                RainLog.Error("RainShaderSet has no Blur shader; blur disabled", this);
            }
            else
            {
                blurMaterial = CoreUtils.CreateEngineMaterial(shaders.Blur);
            }

            pass = new RainRenderPass(blurMaterial);
            RainLog.Verbose("feature created; blur material " + (blurMaterial != null), this);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
            {
                return;
            }

            if (cameraType == CameraType.SceneView)
            {
                var source = RainEffect.SceneViewPreviewSource;
                if (source == null || source.RenderData == null || !source.RenderData.AnyVisible)
                {
                    return;
                }
            }
            else if (!RainEffect.AnyVisibleFor(renderingData.cameraData.camera))
            {
                return;
            }

            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(blurMaterial);
            blurMaterial = null;
        }

#if UNITY_EDITOR
        void Reset()
        {
            shaders = UnityEditor.AssetDatabase.LoadAssetAtPath<RainShaderSet>(
                "Packages/com.virtualmaestro.raindropeffect/Runtime/Shaders/RainShaderSet.asset");
        }

        void OnValidate()
        {
            if (shaders == null)
            {
                Reset();
            }
        }
#endif
    }
}
