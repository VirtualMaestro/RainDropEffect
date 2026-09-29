using NUnit.Framework;
using UnityEngine;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// The trail ring: bounded, deterministic, and producing the legacy ribbon layout. Both trail
    /// families sit on top of it, so a bug here is a bug in two effects at once.
    /// </summary>
    public class TrailBufferTests
    {
        [Test]
        public void PushBootstrapsTwoPoints()
        {
            var buffer = new TrailBuffer(1, 8);
            buffer.Push(0, Vector2.zero, 0f, 0.01f);

            Assert.AreEqual(2, buffer.Count(0), "a ribbon needs a pair before it has a direction");
            Assert.AreEqual(buffer.Get(0, 0).Pos, buffer.Get(0, 1).Pos);
        }

        [Test]
        public void PushRespectsSpacing()
        {
            var buffer = new TrailBuffer(1, 8);
            buffer.Push(0, Vector2.zero, 0f, 0.1f);

            buffer.Push(0, new Vector2(0f, -0.01f), 0.1f, 0.1f);
            Assert.AreEqual(2, buffer.Count(0), "a point closer than the spacing must be ignored");

            buffer.Push(0, new Vector2(0f, -0.2f), 0.2f, 0.1f);
            Assert.AreEqual(3, buffer.Count(0));
        }

        [Test]
        public void RingDropsOldestWhenFull()
        {
            var buffer = new TrailBuffer(1, 8);
            for (var i = 0; i < 20; i++)
            {
                buffer.Push(0, new Vector2(0f, -0.1f * i), i * 0.1f, 0.05f);
            }

            Assert.AreEqual(8, buffer.Count(0), "the ring must stay at its capacity");

            // 20 pushes with a bootstrap pair produce 21 points; the last 8 survive, so the oldest
            // kept point is the 13th.
            Assert.AreEqual(-0.1f * 12, buffer.Get(0, 0).Pos.y, 1e-4f);
            Assert.AreEqual(-0.1f * 19, buffer.Get(0, 7).Pos.y, 1e-4f);
        }

        [Test]
        public void ExpireRemovesOldPoints()
        {
            var buffer = new TrailBuffer(1, 16);
            for (var i = 0; i < 10; i++)
            {
                buffer.Push(0, new Vector2(0f, -0.1f * i), i * 0.1f, 0.05f);
            }

            var before = buffer.Count(0);
            buffer.Expire(0, now: 1.0f, lifetime: 0.5f);

            Assert.Less(buffer.Count(0), before, "old points should have been dropped");
            Assert.GreaterOrEqual(buffer.Get(0, 0).BornAt, 0.5f);
        }

        [Test]
        public void SharpTurnSubdividesAtMostFour()
        {
            var buffer = new TrailBuffer(1, 32);

            buffer.Push(0, Vector2.zero, 0f, 0.1f);
            buffer.Push(0, new Vector2(0f, -0.5f), 0.1f, 0.1f);
            var before = buffer.Count(0);

            // A right-angle turn: the largest subdivision the buffer is allowed to insert is 4.
            buffer.Push(0, new Vector2(0.5f, -0.5f), 0.2f, 0.1f);

            var added = buffer.Count(0) - before;
            Assert.That(added, Is.InRange(2, 5), $"expected the pushed point plus up to 4 inserts, got {added}");
        }

        [Test]
        public void EmitRibbonLayout()
        {
            var buffer = new TrailBuffer(1, 8);
            buffer.Push(0, Vector2.zero, 0f, 0.1f);
            buffer.Push(0, new Vector2(0f, -0.2f), 0.1f, 0.1f);

            Assert.AreEqual(3, buffer.Count(0));

            using (var mesh = new RainBatchMesh(64, 128, "ribbon"))
            {
                mesh.Begin();
                var emitted = buffer.EmitRibbon(0, mesh, 0.1f, AnimationCurve.Constant(0f, 1f, 1f), 1f,
                    new Color32(255, 255, 255, 255), Vector4.zero);
                mesh.End();

                Assert.IsTrue(emitted);
                Assert.AreEqual(3 * 2 + TrailBuffer.HeadVertices, mesh.VertexCount,
                    "two vertices per point, plus the four of the head quad");
                Assert.AreEqual(2 * 6 + TrailBuffer.HeadIndices, mesh.IndexCount,
                    "six indices per segment, plus the six of the head quad");
                Assert.IsFalse(mesh.Overflowed);
            }
        }

        [Test]
        public void EmitRibbonReturnsFalseBelowTwoPoints()
        {
            var buffer = new TrailBuffer(1, 8);

            using (var mesh = new RainBatchMesh(64, 128, "ribbon"))
            {
                mesh.Begin();
                Assert.IsFalse(buffer.EmitRibbon(0, mesh, 0.1f, AnimationCurve.Constant(0f, 1f, 1f), 1f,
                    new Color32(255, 255, 255, 255), Vector4.zero));
                mesh.End();

                Assert.AreEqual(0, mesh.VertexCount);
            }
        }

        [Test]
        public void EmitRibbonProducesFiniteVertices()
        {
            var buffer = new TrailBuffer(1, 96);
            var rng = new RainRandom(11);

            var pos = Vector2.zero;
            for (var i = 0; i < 500; i++)
            {
                pos += new Vector2(rng.Range(-0.05f, 0.05f), rng.Range(-0.05f, 0.05f));
                buffer.Push(0, pos, i * 0.01f, 0.01f);
            }

            using (var mesh = new RainBatchMesh(256, 1024, "walk"))
            {
                mesh.Begin();
                buffer.EmitRibbon(0, mesh, 0.1f, AnimationCurve.Constant(0f, 1f, 1f), 1f,
                    new Color32(255, 255, 255, 255), Vector4.zero);
                mesh.End();

                Assert.AreEqual(buffer.Count(0) * 2 + TrailBuffer.HeadVertices, mesh.VertexCount);
                Assert.IsFalse(mesh.Overflowed);

                // The loop runs past the ribbon into the head quad's four vertices: a head placed
                // from a zero-length direction would be the NaN this catches.
                for (var i = 0; i < mesh.VertexCount; i++)
                {
                    var p = mesh.GetVertex(i).Position;
                    Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y),
                        $"vertex {i} is not finite: {p}");
                }
            }
        }

        [Test]
        public void ClearEmptiesOneTrailOnly()
        {
            var buffer = new TrailBuffer(2, 8);
            buffer.Push(0, Vector2.zero, 0f, 0.1f);
            buffer.Push(1, Vector2.zero, 0f, 0.1f);

            buffer.Clear(0);

            Assert.AreEqual(0, buffer.Count(0));
            Assert.AreEqual(2, buffer.Count(1));
        }
    }
}
