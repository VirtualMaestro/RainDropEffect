using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace RainDropEffect
{
    /// <summary>
    /// Vertex layout shared by every rain batch. Frozen by decision D10: no later task changes it
    /// without updating <c>RainBatchMeshTests</c>.
    /// </summary>
    /// <remarks>
    /// Field order is Position, Tint, UV, Params, not the plan's Position, UV, Tint, Params:
    /// Unity lays vertex attributes out in <see cref="VertexAttribute"/> enum order (Color before
    /// TexCoord0) regardless of the descriptor order, so a struct that puts UV first is read 4
    /// bytes out of alignment. The semantic content and the shader inputs are unchanged.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public struct RainVertex
    {
        /// <summary>x, y in normalized units (y in [-1,1], x in [-aspect,aspect]); z = opacity 0..1.</summary>
        public Vector3 Position;

        /// <summary>rgb overlay color, a overlay alpha (curves and intensity already applied).</summary>
        public Color32 Tint;

        public Vector2 UV;

        /// <summary>x distortion (px at 1080p), y relief, z blur 0..1, w darkness.</summary>
        public Vector4 Params;

        public static readonly VertexAttributeDescriptor[] Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4)
        };
    }
}
