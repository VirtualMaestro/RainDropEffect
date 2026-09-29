using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace RainDropEffect
{
    /// <summary>
    /// One reusable dynamic mesh plus its persistent CPU arrays. Written once per frame between
    /// <see cref="Begin"/> and <see cref="End"/>; steady state allocates nothing.
    ///
    /// Managed arrays (not NativeArray) keep <c>ref</c> element access available without unsafe
    /// code, and <c>Mesh.SetVertexBufferData</c>/<c>SetIndexBufferData</c> accept them directly.
    /// </summary>
    public sealed class RainBatchMesh : IDisposable
    {
        public const int MaxVerticesLimit = 65535;

        readonly RainVertex[] vertices;
        readonly ushort[] indices;
        readonly string meshName;

        RainVertex scratch;
        Mesh mesh;

        public RainBatchMesh(int maxVertices, int maxIndices, string name)
        {
            if (maxVertices <= 0 || maxVertices > MaxVerticesLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(maxVertices), maxVertices,
                    $"maxVertices must be in 1..{MaxVerticesLimit} (UInt16 index buffer).");
            }

            if (maxIndices <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxIndices), maxIndices, "maxIndices must be positive.");
            }

            meshName = name;
            vertices = new RainVertex[maxVertices];
            indices = new ushort[maxIndices];

            mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave, name = name };
            mesh.SetVertexBufferParams(maxVertices, RainVertex.Layout);
            mesh.SetIndexBufferParams(maxIndices, IndexFormat.UInt16);
            mesh.MarkDynamic();
            // Geometry is already in clip space; a fixed generous bounds avoids per-frame recalculation.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(100f, 100f, 100f));
        }

        public Mesh Mesh => mesh;

        public int VertexCount { get; private set; }

        public int IndexCount { get; private set; }

        public int VertexCapacity => vertices != null ? vertices.Length : 0;

        public int IndexCapacity => indices != null ? indices.Length : 0;

        /// <summary>True when the last fill ran out of capacity. Never throws inside the frame loop.</summary>
        public bool Overflowed { get; private set; }

        public bool IsEmpty => IndexCount == 0;

        /// <summary>Reads back a written vertex. For tests; the GPU buffer itself is write-only.</summary>
        internal RainVertex GetVertex(int index) => vertices[index];

        public void Begin()
        {
            VertexCount = 0;
            IndexCount = 0;
            Overflowed = false;
        }

        public ref RainVertex AddVertex()
        {
            if (VertexCount >= vertices.Length)
            {
                Overflowed = true;
                return ref scratch;
            }

            return ref vertices[VertexCount++];
        }

        public void AddIndex(ushort index)
        {
            if (IndexCount >= indices.Length)
            {
                Overflowed = true;
                return;
            }

            indices[IndexCount++] = index;
        }

        /// <summary>Two triangles over the four vertices starting at <paramref name="baseVertex"/>.</summary>
        public void AddQuadIndices(ushort baseVertex)
        {
            AddIndex(baseVertex);
            AddIndex((ushort)(baseVertex + 1));
            AddIndex((ushort)(baseVertex + 2));
            AddIndex((ushort)(baseVertex + 2));
            AddIndex((ushort)(baseVertex + 1));
            AddIndex((ushort)(baseVertex + 3));
        }

        public void End()
        {
            const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds
                                          | MeshUpdateFlags.DontValidateIndices
                                          | MeshUpdateFlags.DontNotifyMeshUsers;

            mesh.SetVertexBufferData(vertices, 0, 0, VertexCount, 0, flags);
            mesh.SetIndexBufferData(indices, 0, 0, IndexCount, flags);
            mesh.SetSubMesh(0,
                new SubMeshDescriptor(0, IndexCount, MeshTopology.Triangles)
                {
                    bounds = mesh.bounds,
                    vertexCount = VertexCount
                },
                flags);
        }

        public void Dispose()
        {
            CoreUtils.Destroy(mesh);
            mesh = null;
        }

        /// <summary>
        /// Writes one axis-aligned (optionally rotated) quad. Corner order matches the legacy
        /// <c>RainDropTools.CreateQuadMesh</c>: (-1,-1), (1,-1), (-1,1), (1,1).
        /// </summary>
        public static void AddQuad(RainBatchMesh target, Vector2 center, Vector2 halfSize, float rotationRad,
            float opacity, Color32 tint, Vector4 parameters)
        {
            if (target.VertexCount + 4 > target.VertexCapacity)
            {
                target.Overflowed = true;
                return;
            }

            var baseVertex = (ushort)target.VertexCount;
            var cos = Mathf.Cos(rotationRad);
            var sin = Mathf.Sin(rotationRad);

            WriteCorner(target, center, halfSize, cos, sin, -1f, -1f, new Vector2(0f, 0f), opacity, tint, parameters);
            WriteCorner(target, center, halfSize, cos, sin, 1f, -1f, new Vector2(1f, 0f), opacity, tint, parameters);
            WriteCorner(target, center, halfSize, cos, sin, -1f, 1f, new Vector2(0f, 1f), opacity, tint, parameters);
            WriteCorner(target, center, halfSize, cos, sin, 1f, 1f, new Vector2(1f, 1f), opacity, tint, parameters);

            target.AddQuadIndices(baseVertex);
        }

        static void WriteCorner(RainBatchMesh target, Vector2 center, Vector2 halfSize, float cos, float sin,
            float signX, float signY, Vector2 uv, float opacity, Color32 tint, Vector4 parameters)
        {
            var localX = signX * halfSize.x;
            var localY = signY * halfSize.y;

            ref var vertex = ref target.AddVertex();
            vertex.Position = new Vector3(
                center.x + localX * cos - localY * sin,
                center.y + localX * sin + localY * cos,
                opacity);
            vertex.UV = uv;
            vertex.Tint = tint;
            vertex.Params = parameters;
        }
    }
}
