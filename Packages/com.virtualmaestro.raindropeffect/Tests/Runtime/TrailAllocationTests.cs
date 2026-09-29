using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// The trail families at their maximum supported size must not allocate per frame either. They
    /// are the heaviest simulations in the package, so if anything is going to allocate it is these.
    /// </summary>
    public class TrailAllocationTests
    {
        GameObject host;
        RainProfile profile;
        RainEffect effect;

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }

            if (profile != null)
            {
                Object.DestroyImmediate(profile);
            }
        }

        IEnumerator WarmDenseTrails()
        {
            host = new GameObject("RainTrailAllocationCamera", typeof(Camera));

            profile = ScriptableObject.CreateInstance<RainProfile>();
            profile.name = "DenseTrailProfile";
            profile.FlowLayers = new[]
            {
                new FlowLayerSettings
                {
                    Name = "Flow",
                    Duration = 1f,
                    EmissionRateRange = new Vector2Int(60, 60),
                    MaxCount = 32,
                    MaxPoints = 96,
                    LifetimeRange = new Vector2(5f, 5f),
                    AccelerationRange = new Vector2(0.2f, 0.2f),
                    FluctuationRateRange = new Vector2(5f, 5f),
                    PointSpacing = 0.004f
                }
            };
            profile.FrictionLayers = new[]
            {
                new FrictionLayerSettings
                {
                    Name = "Friction",
                    Duration = 1f,
                    EmissionRateRange = new Vector2Int(60, 60),
                    MaxCount = 32,
                    MaxPoints = 96,
                    LifetimeRange = new Vector2(5f, 5f),
                    AccelerationRange = new Vector2(0.2f, 0.2f),
                    PointSpacing = 0.004f,
                    Field = FrictionFieldForTest()
                }
            };

            effect = RainTestProfiles.CreateEffect(host, profile);
            effect.Play();

            for (var i = 0; i < 30; i++)
            {
                effect.TickForTest(1f / 60f);
                effect.EmitForTest();
                yield return null;
            }
        }

        static FrictionField FrictionFieldForTest()
        {
#if UNITY_EDITOR
            var shipped = UnityEditor.AssetDatabase.LoadAssetAtPath<FrictionField>(
                "Packages/com.virtualmaestro.raindropeffect/Runtime/Data/FrictionField_friction_map.asset");
            if (shipped != null)
            {
                return shipped;
            }
#endif
            return null;
        }

        [UnityTest]
        public IEnumerator DenseTrailTickDoesNotAllocate()
        {
            yield return WarmDenseTrails();

            Assert.That(() => effect.TickForTest(1f / 60f), Is.Not.AllocatingGCMemory());
        }

        [UnityTest]
        public IEnumerator DenseTrailEmitDoesNotAllocate()
        {
            yield return WarmDenseTrails();

            Assert.That(() => effect.EmitForTest(), Is.Not.AllocatingGCMemory());
        }
    }
}
