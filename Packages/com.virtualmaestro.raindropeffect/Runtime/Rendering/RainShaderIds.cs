using UnityEngine;
using UnityEngine.Rendering;

namespace RainDropEffect
{
    /// <summary>Cached shader property ids used by the render pass and the layer materials.</summary>
    public static class RainShaderIds
    {
        public static readonly int SceneColor = Shader.PropertyToID("_RainSceneColor");
        public static readonly int SceneBlur = Shader.PropertyToID("_RainSceneBlur");
        public static readonly int NormalMap = Shader.PropertyToID("_NormalMap");
        public static readonly int OverlayTex = Shader.PropertyToID("_OverlayTex");
        public static readonly int CoverageTex = Shader.PropertyToID("_CoverageTex");
        public static readonly int BlurTexel = Shader.PropertyToID("_RainBlurTexel");
        public static readonly int BlurRadius = Shader.PropertyToID("_RainBlurRadius");
    }

    /// <summary>Pass indices of <c>Hidden/RainDropEffect/Lens</c>. Frozen by decision D10.</summary>
    public static class RainShaderPass
    {
        public const int Lens = 0;
        public const int Overlay = 1;
    }

    /// <summary>Local keywords, created lazily per shader (a keyword is bound to its shader).</summary>
    public static class RainKeywords
    {
        public const string BlurName = "_RAIN_BLUR";

        public static LocalKeyword Blur(Shader shader)
        {
            return new LocalKeyword(shader, BlurName);
        }
    }
}
