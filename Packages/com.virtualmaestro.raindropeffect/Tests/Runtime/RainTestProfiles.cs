using UnityEngine;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// Profiles built in code rather than loaded from an asset: the shipped ones live in
    /// <c>Samples~</c>, which the Asset Database cannot see, and a test that builds its own data is
    /// also a test that says what it depends on.
    /// </summary>
    internal static class RainTestProfiles
    {
        /// <summary>One static frame plus falling droplets — the shape of the Basic preset.</summary>
        public static RainProfile CreateBasic(int simpleCount = 20)
        {
            var profile = ScriptableObject.CreateInstance<RainProfile>();
            profile.name = "TestBasicProfile";

            profile.StaticLayers = new[]
            {
                new StaticLayerSettings
                {
                    Name = "Frame",
                    Depth = 0,
                    Distortion = 20f,
                    Relief = 1f,
                    FadeTime = 2f
                }
            };

            profile.SimpleLayers = new[]
            {
                new SimpleLayerSettings
                {
                    Name = "Drops",
                    Depth = 1,
                    MaxCount = simpleCount,
                    Duration = 1f,
                    EmissionRateRange = new Vector2Int(30, 30),
                    LifetimeRange = new Vector2(0.6f, 1.4f),
                    Distortion = 50f,
                    Relief = 1.5f
                }
            };

            return profile;
        }

        /// <summary>
        /// Attaches a configured effect to a fresh camera. The shader set is normally assigned by
        /// the inspector, which never runs in a test.
        /// </summary>
        public static RainEffect CreateEffect(GameObject host, RainProfile profile, int seed = 1234)
        {
            var effect = host.AddComponent<RainEffect>();
#if UNITY_EDITOR
            effect.EditorAutoAssignShaderSet();
#endif
            effect.Profile = profile;
            effect.Seed = seed;
            effect.Rebuild();
            return effect;
        }
    }
}
