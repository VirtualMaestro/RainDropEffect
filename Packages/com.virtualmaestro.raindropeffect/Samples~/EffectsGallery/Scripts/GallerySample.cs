using UnityEngine;

namespace RainDropEffect.Samples
{
    /// <summary>
    /// Switches one <see cref="RainEffect"/> between the shipped profiles. One camera, one effect
    /// component: swapping <see cref="RainEffect.Profile"/> and calling <see cref="RainEffect.Rebuild"/>
    /// is the whole preset mechanism, so a game needs no per-preset camera prefab.
    /// </summary>
    public sealed class GallerySample : MonoBehaviour
    {
        public RainEffect Effect;

        /// <summary>Profiles offered as buttons, in order.</summary>
        public RainProfile[] Profiles = new RainProfile[0];

        /// <summary>Quality forced when the profile at the same index is selected; the Mobile presets use Low.</summary>
        public RainQuality[] Qualities = new RainQuality[0];

        int selected = -1;

        void Awake()
        {
            if (Effect == null)
            {
                Debug.LogWarning($"{nameof(GallerySample)} on '{name}' has no RainEffect; disabling.", this);
                enabled = false;
            }
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12f, 12f, 210f, Screen.height - 24f));
            GUILayout.Label("Presets");

            for (var i = 0; i < Profiles.Length; i++)
            {
                var profile = Profiles[i];
                if (profile == null)
                {
                    continue;
                }

                var label = i == selected ? $"▶ {profile.name}" : profile.name;
                if (GUILayout.Button(label, GUILayout.Height(28f)))
                {
                    Select(i);
                }
            }

            GUILayout.Space(10f);
            GUILayout.Label("Quality");

            if (GUILayout.Button("Low", GUILayout.Height(24f))) SetQuality(RainQuality.Low);
            if (GUILayout.Button("Balanced", GUILayout.Height(24f))) SetQuality(RainQuality.Balanced);
            if (GUILayout.Button("High", GUILayout.Height(24f))) SetQuality(RainQuality.High);

            GUILayout.Space(10f);

            if (GUILayout.Button("Stop", GUILayout.Height(24f))) Effect.Stop();
            if (GUILayout.Button("Clear", GUILayout.Height(24f))) Effect.Clear();

            GUILayout.EndArea();
        }

        void Select(int index)
        {
            selected = index;
            Effect.Profile = Profiles[index];
            Effect.Quality = index < Qualities.Length ? Qualities[index] : RainQuality.High;
            Effect.Rebuild();
            Effect.Play();
        }

        void SetQuality(RainQuality quality)
        {
            Effect.Quality = quality;
            Effect.Rebuild();
            Effect.Play();
        }
    }
}
