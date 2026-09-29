using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// Informational CPU measurement for the densest configuration the package supports: 32 flow
    /// trails, 32 friction trails and 256 droplets, all at 96 points.
    ///
    /// The assertion here is a sanity bound, not a device budget — an editor on a desktop is not a
    /// phone. Task 25 records the real budgets; this exists so Phase 6 starts from a number rather
    /// than from an opinion.
    /// </summary>
    public class DenseTrailBenchmarkTests
    {
        const string OutputPath = "Logs/benchmark-cpu.json";

        GameObject host;
        RainProfile profile;
        RainEffect effect;

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                UnityEngine.Object.DestroyImmediate(host);
            }

            if (profile != null)
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [UnityTest]
        public IEnumerator DenseProfileTickStaysUnderSanityBound()
        {
            host = new GameObject("RainBenchmarkCamera", typeof(Camera));

            profile = ScriptableObject.CreateInstance<RainProfile>();
            profile.name = "BenchmarkProfile";
            profile.SimpleLayers = new[]
            {
                new SimpleLayerSettings
                {
                    Name = "Drops",
                    Duration = 1f,
                    EmissionRateRange = new Vector2Int(120, 120),
                    MaxCount = 256,
                    LifetimeRange = new Vector2(3f, 3f)
                }
            };
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
                    PointSpacing = 0.004f
                }
            };

            effect = RainTestProfiles.CreateEffect(host, profile);
            effect.Play();

            // Warm up until the trails are at full length; an empty simulation measures nothing.
            for (var i = 0; i < 120; i++)
            {
                effect.TickForTest(1f / 60f);
                effect.EmitForTest();
            }

            yield return null;

            const int samples = 300;
            var times = new double[samples];
            var watch = new Stopwatch();

            for (var i = 0; i < samples; i++)
            {
                watch.Restart();
                effect.TickForTest(1f / 60f);
                effect.EmitForTest();
                watch.Stop();
                times[i] = watch.Elapsed.TotalMilliseconds;
            }

            Array.Sort(times);
            var mean = 0d;
            foreach (var t in times)
            {
                mean += t;
            }

            mean /= samples;
            var p95 = times[(int)(samples * 0.95f)];

            Directory.CreateDirectory("Logs");
            File.WriteAllText(OutputPath,
                "{\n" +
                $"  \"meanTickMs\": {mean:0.####},\n" +
                $"  \"p95TickMs\": {p95:0.####},\n" +
                $"  \"samples\": {samples},\n" +
                $"  \"machine\": \"{SystemInfo.processorType}\"\n" +
                "}\n");

            TestContext.WriteLine($"dense tick+emit: mean {mean:0.###} ms, p95 {p95:0.###} ms over {samples} samples");
            TestContext.WriteLine($"written to {OutputPath}");

            Assert.Less(p95, 5.0, "the dense configuration is far slower than expected on a desktop editor");
        }
    }
}
