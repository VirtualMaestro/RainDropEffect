using UnityEngine;

namespace RainDropEffect
{
    /// <summary>
    /// All trail points of one layer in a single array: a fixed-capacity ring per trail, allocated
    /// once. Nothing here allocates per frame, and a trail that runs long simply drops its oldest
    /// point instead of growing.
    ///
    /// Points are stored oldest-first. The 1.x trail stored them newest-first and ran
    /// its width curve and UVs from newest to oldest, so both are mirrored on the way out
    /// (<c>1 - progress</c>) rather than by reversing the storage.
    /// </summary>
    internal sealed class TrailBuffer
    {
        public struct Point
        {
            public Vector2 Pos;
            public float BornAt;

            /// <summary>Unit direction of travel at this point; the ribbon width is laid out across it.</summary>
            public Vector2 Dir;
        }

        /// <summary>A turn sharper than this (radians) gets intermediate points so the ribbon does not crease.</summary>
        const float SubdivisionAngle = 0.35f;

        /// <summary>Zero-area quads flicker; keep a hair of width.</summary>
        const float MinHalfWidth = 0.001f;

        /// <summary>Vertices the head quad adds on top of the ribbon's two per point.</summary>
        public const int HeadVertices = 4;

        /// <summary>Indices the head quad adds on top of the ribbon's six per segment.</summary>
        public const int HeadIndices = 6;

        readonly Point[] points;
        readonly int maxPoints;
        readonly int[] head;
        readonly int[] count;

        public TrailBuffer(int trailCount, int maxPoints)
        {
            TrailCount = Mathf.Max(1, trailCount);
            this.maxPoints = Mathf.Max(2, maxPoints);

            points = new Point[TrailCount * this.maxPoints];
            head = new int[TrailCount];
            count = new int[TrailCount];
        }

        public int TrailCount { get; }

        public int MaxPoints => maxPoints;

        public int Count(int trail) => count[trail];

        public void Clear(int trail)
        {
            head[trail] = 0;
            count[trail] = 0;
        }

        public void ClearAll()
        {
            for (var t = 0; t < TrailCount; t++)
            {
                Clear(t);
            }
        }

        /// <summary>Point <paramref name="i"/> of a trail: 0 is the oldest, Count-1 the newest.</summary>
        public ref Point Get(int trail, int i)
        {
            return ref points[trail * maxPoints + (head[trail] + i) % maxPoints];
        }

        /// <summary>
        /// Records a new position when it is at least <paramref name="spacing"/> away from the last
        /// one, inserting intermediate points across a sharp turn. The oldest point is dropped when
        /// the ring is full.
        /// </summary>
        public void Push(int trail, Vector2 pos, float now, float spacing, int maxSubdivisions = 4)
        {
            spacing = Mathf.Max(0.001f, spacing);

            if (count[trail] == 0)
            {
                // Two coincident points, like the legacy bootstrap: a ribbon needs a pair before it
                // has a direction of its own.
                Add(trail, pos, now, Vector2.down);
                Add(trail, pos, now, Vector2.down);
                return;
            }

            var last = Get(trail, count[trail] - 1);
            var delta = pos - last.Pos;
            var dist = delta.magnitude;
            if (dist < spacing)
            {
                return;
            }

            var dir = delta / dist;

            if (count[trail] >= 2)
            {
                var prevDir = last.Dir;
                var cos = Mathf.Clamp(Vector2.Dot(prevDir, dir), -1f, 1f);
                var angle = Mathf.Acos(cos);

                if (angle > SubdivisionAngle)
                {
                    var sub = Mathf.Min(maxSubdivisions, (int)(angle / SubdivisionAngle));
                    for (var k = 1; k <= sub; k++)
                    {
                        var f = k / (float)(sub + 1);
                        var d = Vector2.Lerp(prevDir, dir, f).normalized;
                        Add(trail, last.Pos + d * (dist * f), now, d);
                    }
                }
            }

            Add(trail, pos, now, dir);
        }

        void Add(int trail, Vector2 pos, float now, Vector2 dir)
        {
            if (count[trail] == maxPoints)
            {
                head[trail] = (head[trail] + 1) % maxPoints;
                count[trail]--;
            }

            var index = trail * maxPoints + (head[trail] + count[trail]) % maxPoints;
            points[index] = new Point { Pos = pos, BornAt = now, Dir = dir };
            count[trail]++;
        }

        /// <summary>Drops points older than <paramref name="lifetime"/>, so a trail fades from its tail.</summary>
        public void Expire(int trail, float now, float lifetime)
        {
            while (count[trail] > 0 && now - Get(trail, 0).BornAt >= lifetime)
            {
                head[trail] = (head[trail] + 1) % maxPoints;
                count[trail]--;
            }
        }

        /// <summary>
        /// Writes one trail as a ribbon: a pair of vertices per point, offset across the direction
        /// of travel, plus one quad at the leading end for the drop's head. Returns false when there
        /// is nothing to draw or no room left in the mesh.
        /// </summary>
        public bool EmitRibbon(int trail, RainBatchMesh mesh, float widthMul, AnimationCurve widthCurve,
            float opacity, Color32 tint, Vector4 prm)
        {
            var n = count[trail];
            if (n < 2)
            {
                return false;
            }

            // Reserve the head quad here too: a ribbon emitted without its head reads as a scratch
            // on the glass rather than a drop that left a trail.
            if (mesh.VertexCount + n * 2 + HeadVertices > mesh.VertexCapacity)
            {
                return false;
            }

            var baseVertex = (ushort)mesh.VertexCount;

            for (var i = 0; i < n; i++)
            {
                ref var p = ref Get(trail, i);

                // 0 at the oldest point, 1 at the newest; the curve is authored the other way round.
                var progress = i / (float)(n - 1);
                var w = Mathf.Max(widthMul * widthCurve.Evaluate(1f - progress) * 0.5f, MinHalfWidth);
                var side = new Vector2(-p.Dir.y, p.Dir.x) * w;

                ref var a = ref mesh.AddVertex();
                a.Position = new Vector3(p.Pos.x + side.x, p.Pos.y + side.y, opacity);
                a.UV = new Vector2(1f - progress, 0f);
                a.Tint = tint;
                a.Params = prm;

                ref var b = ref mesh.AddVertex();
                b.Position = new Vector3(p.Pos.x - side.x, p.Pos.y - side.y, opacity);
                b.UV = new Vector2(1f - progress, 1f);
                b.Tint = tint;
                b.Params = prm;
            }

            for (var i = 1; i < n; i++)
            {
                var v = (ushort)(baseVertex + i * 2);
                mesh.AddIndex((ushort)(v - 2));
                mesh.AddIndex((ushort)(v - 1));
                mesh.AddIndex(v);
                mesh.AddIndex((ushort)(v + 1));
                mesh.AddIndex(v);
                mesh.AddIndex((ushort)(v - 1));
            }

            // The head. Sized from widthMul rather than from the curve so it stays a blob even where
            // the width curve has tapered the ribbon to nothing, and rotated so the quad's local +y
            // runs along the direction of travel — the same convention SimpleLayerRuntime uses.
            ref var headPoint = ref Get(trail, n - 1);
            var headHalf = Mathf.Max(widthMul * 0.5f, MinHalfWidth);
            var rotation = Mathf.Atan2(headPoint.Dir.y, headPoint.Dir.x) - Mathf.PI * 0.5f;
            RainBatchMesh.AddQuad(mesh, headPoint.Pos, new Vector2(headHalf, headHalf), rotation, opacity, tint, prm);

            return true;
        }
    }
}
