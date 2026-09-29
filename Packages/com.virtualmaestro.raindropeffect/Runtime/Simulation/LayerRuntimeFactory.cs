using UnityEngine;

namespace RainDropEffect
{
    /// <summary>
    /// The vertex and index budget one layer allocates, computed once at construction.
    /// </summary>
    internal readonly struct MeshCapacity
    {
        public readonly int Vertices;
        public readonly int Indices;

        public MeshCapacity(int vertices, int indices)
        {
            Vertices = vertices;
            Indices = indices;
        }
    }

    /// <summary>
    /// Turns a settings object into the runtime that simulates it, and is the single source of truth
    /// for how large that runtime's mesh has to be.
    ///
    /// Capacities are fixed at construction so no simulation ever reallocates a mesh — and they live
    /// here rather than in each runtime's base constructor call because a mesh sized too small does
    /// not throw: <see cref="RainBatchMesh.AddVertex"/> sets <c>Overflowed</c> and returns a scratch
    /// vertex, so the geometry is silently dropped. When this arithmetic existed twice it drifted;
    /// the copy here missed both the runtimes' clamps and the per-trail head quad added in 2.0.1.
    /// </summary>
    internal static class LayerRuntimeFactory
    {
        public static LayerRuntime Create(RainLayerSettings settings, RainShaderSet shaders, RainQuality quality)
        {
            switch (settings)
            {
                case StaticLayerSettings staticSettings:
                    return new StaticLayerRuntime(staticSettings, shaders, quality);

                case SimpleLayerSettings simple:
                    return new SimpleLayerRuntime(simple, shaders, quality);

                case FlowLayerSettings flow:
                    return new FlowLayerRuntime(flow, shaders, quality);

                case FrictionLayerSettings friction:
                    return new FrictionLayerRuntime(friction, shaders, quality);

                default:
                    // A new settings type without a runtime is a programming error, not user input.
                    throw new System.NotSupportedException(settings.GetType().Name);
            }
        }

        /// <summary>Drops a Simple layer allocates for: the authored count under the tier's cap.</summary>
        public static int DropCount(int maxCount, RainQuality quality)
        {
            return Mathf.Clamp(Mathf.Min(maxCount, RainQualityCaps.MaxDrops(quality)), 1, 256);
        }

        /// <summary>Trails a Flow or Friction layer allocates for.</summary>
        public static int TrailCount(int maxCount, RainQuality quality)
        {
            return Mathf.Clamp(Mathf.Min(maxCount, RainQualityCaps.MaxTrails(quality)), 1, 32);
        }

        /// <summary>Points one trail's ring holds.</summary>
        public static int TrailPoints(int maxPoints, RainQuality quality)
        {
            return Mathf.Clamp(Mathf.Min(maxPoints, RainQualityCaps.MaxPoints(quality)), 4, 96);
        }

        /// <summary>One quad.</summary>
        public static MeshCapacity StaticCapacity()
        {
            return new MeshCapacity(4, 6);
        }

        /// <summary>One quad per drop, capped by quality.</summary>
        public static MeshCapacity DropCapacity(int maxCount, RainQuality quality)
        {
            var n = DropCount(maxCount, quality);
            return new MeshCapacity(4 * n, 6 * n);
        }

        /// <summary>
        /// Per trail: a ribbon of p points is 2p vertices and 6(p-1) indices, plus the one head quad
        /// <see cref="TrailBuffer.EmitRibbon"/> appends at the leading end.
        /// </summary>
        public static MeshCapacity TrailCapacity(int maxCount, int maxPoints, RainQuality quality)
        {
            var t = TrailCount(maxCount, quality);
            var p = TrailPoints(maxPoints, quality);

            return new MeshCapacity(
                t * (2 * p + TrailBuffer.HeadVertices),
                t * (6 * Mathf.Max(0, p - 1) + TrailBuffer.HeadIndices));
        }

        public static void StaticCapacity(out int vertices, out int indices)
        {
            Unpack(StaticCapacity(), out vertices, out indices);
        }

        public static void DropCapacity(int maxCount, RainQuality quality, out int vertices, out int indices)
        {
            Unpack(DropCapacity(maxCount, quality), out vertices, out indices);
        }

        public static void TrailCapacity(int maxCount, int maxPoints, RainQuality quality,
            out int vertices, out int indices)
        {
            Unpack(TrailCapacity(maxCount, maxPoints, quality), out vertices, out indices);
        }

        static void Unpack(in MeshCapacity capacity, out int vertices, out int indices)
        {
            vertices = capacity.Vertices;
            indices = capacity.Indices;
        }
    }
}
