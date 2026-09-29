using System;
using UnityEngine;

namespace RainDropEffect
{
    /// <summary>
    /// Fields shared by every layer family.
    ///
    /// These names are the serialization contract: they appear verbatim in
    /// consumer profiles by name. Renaming one silently
    /// drops data from every profile authored before the rename.
    /// </summary>
    [Serializable]
    public abstract class RainLayerSettings
    {
        public string Name = "Layer";
        public bool Enabled = true;

        /// <summary>Draw order, ascending; ties broken by <see cref="FamilyOrder"/> then array index.</summary>
        public int Depth = 0;

        public RainLayerMode Mode = RainLayerMode.Lens;

        public Texture2D NormalMap;
        public Texture2D OverlayTexture;

        /// <summary>Baked by the coverage baker; the lens pass needs an explicit silhouette (D9).</summary>
        public Texture2D CoverageMask;

        public Color OverlayColor = Color.gray;

        [Range(0, 200)] public float Distortion = 50f;
        [Range(0, 2)] public float Relief = 1.5f;
        [Range(0, 1)] public float Blur = 0f;
        [Range(0, 5)] public float Darkness = 0f;

        public bool AutoStart = true;

        /// <summary>Static 0, Simple 1, Flow 2, Friction 3. Tie-break after <see cref="Depth"/>.</summary>
        public abstract int FamilyOrder { get; }
    }
}
