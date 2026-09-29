using UnityEngine;

namespace RainDropEffect.Samples
{
    /// <summary>Frame counter from the 1.x demo, so the gallery shows the cost of a preset switch.</summary>
    public sealed class FpsDisplaySample : MonoBehaviour
    {
        const float Interval = 0.2f;

        float startTime;
        int frames;
        int fps;

        GUIStyle style;

        void LateUpdate()
        {
            frames++;
            var elapsed = Time.time - startTime;

            if (elapsed < Interval)
            {
                return;
            }

            fps = (int)(frames / elapsed);
            frames = 0;
            startTime = Time.time;
        }

        void OnGUI()
        {
            // Allocating a GUIStyle per OnGUI call is a per-frame allocation; the 1.x script did it.
            style ??= new GUIStyle { alignment = TextAnchor.UpperRight };
            style.fontSize = Mathf.Max(14, Screen.height / 20);
            style.normal.textColor = Color.white;

            GUI.Label(new Rect(0f, 8f, Screen.width - 12f, style.fontSize * 1.4f), $"FPS:{fps}", style);
        }
    }
}
