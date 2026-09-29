using UnityEngine;
using UnityEngine.Rendering;

namespace RainDropEffect
{
    /// <summary>Creates the one material a layer draws with. One material per layer (decision D11).</summary>
    public static class RainMaterials
    {
        public static Material CreateLayerMaterial(RainShaderSet set, Texture normal, Texture overlay,
            Texture coverage, bool blur)
        {
            if (set == null || set.Lens == null)
            {
                RainLog.Error("Cannot create a layer material: RainShaderSet or its Lens shader is missing.");
                return null;
            }

            var material = CoreUtils.CreateEngineMaterial(set.Lens);
            material.SetTexture(RainShaderIds.NormalMap, normal != null ? normal : Texture2D.normalTexture);
            material.SetTexture(RainShaderIds.OverlayTex, overlay != null ? overlay : Texture2D.blackTexture);
            material.SetTexture(RainShaderIds.CoverageTex, coverage != null ? coverage : Texture2D.whiteTexture);
            material.SetKeyword(RainKeywords.Blur(set.Lens), blur);
            material.name = $"Rain {(normal != null ? normal.name : "layer")}";
            return material;
        }
    }
}
