using UnityEngine;

namespace RainDropEffect.Samples
{
    /// <summary>The smallest possible use of the API: play, stop, clear on one effect.</summary>
    public sealed class BasicSample : MonoBehaviour
    {
        public RainEffect Effect;

        void Awake()
        {
            if (Effect == null)
            {
                Debug.LogWarning($"{nameof(BasicSample)} on '{name}' has no RainEffect; disabling.", this);
                enabled = false;
            }
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12f, 12f, 180f, 140f));

            if (GUILayout.Button(Effect.IsEmitting ? "Stop" : "Play", GUILayout.Height(34f)))
            {
                if (Effect.IsEmitting)
                {
                    Effect.Stop();
                }
                else
                {
                    Effect.Play();
                }
            }

            if (GUILayout.Button("Clear", GUILayout.Height(34f)))
            {
                Effect.Clear();
            }

            GUILayout.EndArea();
        }
    }
}
