using NUnit.Framework;
using UnityEngine;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// Pins the batch geometry contract from Task 6: four vertices and six indices per quad,
    /// overflow flagged instead of thrown, and a clean empty submesh when nothing was written.
    /// </summary>
    public class RainBatchMeshTests
    {
        static readonly Color32 White = new Color32(255, 255, 255, 255);

        [Test]
        public void AddQuadWritesFourVerticesSixIndices()
        {
            using (var batch = new RainBatchMesh(64, 96, "test"))
            {
                batch.Begin();
                RainBatchMesh.AddQuad(batch, Vector2.zero, new Vector2(0.1f, 0.1f), 0f, 1f, White, Vector4.zero);
                batch.End();

                Assert.AreEqual(4, batch.VertexCount);
                Assert.AreEqual(6, batch.IndexCount);
                Assert.IsFalse(batch.Overflowed);
                Assert.AreEqual(6u, batch.Mesh.GetIndexCount(0));

                // mesh.vertexCount reports the buffer capacity after SetVertexBufferParams,
                // so the submesh descriptor is the meaningful assertion here.
                Assert.AreEqual(4, batch.Mesh.GetSubMesh(0).vertexCount);
            }
        }

        [Test]
        public void OverflowIsFlaggedNotThrown()
        {
            using (var batch = new RainBatchMesh(4, 12, "test"))
            {
                batch.Begin();
                RainBatchMesh.AddQuad(batch, Vector2.zero, Vector2.one * 0.1f, 0f, 1f, White, Vector4.zero);
                Assert.DoesNotThrow(() =>
                    RainBatchMesh.AddQuad(batch, Vector2.one, Vector2.one * 0.1f, 0f, 1f, White, Vector4.zero));
                batch.End();

                Assert.IsTrue(batch.Overflowed);
                Assert.AreEqual(4, batch.VertexCount);
                Assert.AreEqual(6, batch.IndexCount);
            }
        }

        [Test]
        public void EndWithNoGeometryYieldsEmptySubmesh()
        {
            using (var batch = new RainBatchMesh(16, 24, "test"))
            {
                batch.Begin();
                batch.End();

                Assert.IsTrue(batch.IsEmpty);
                Assert.AreEqual(0, batch.Mesh.GetSubMesh(0).indexCount);
            }
        }

        [Test]
        public void DisposeDestroysMesh()
        {
            var batch = new RainBatchMesh(16, 24, "test");
            batch.Dispose();

            Assert.IsNull(batch.Mesh);
        }

        [Test]
        public void TooManyVerticesIsRejected()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new RainBatchMesh(RainBatchMesh.MaxVerticesLimit + 1, 8, "test"));
        }
    }
}
