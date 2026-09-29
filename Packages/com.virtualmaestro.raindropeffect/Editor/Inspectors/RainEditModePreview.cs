using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace RainDropEffect.Editor
{
    /// <summary>
    /// Drives the player loop outside Play Mode so a component with <c>PreviewInEditMode</c>
    /// animates in the Game view.
    ///
    /// The editor only ticks the player loop when something asks it to, which is why this exists at
    /// all. It is throttled to roughly 60 Hz: without the throttle the editor would repaint as fast
    /// as it can and peg a core for a preview nobody is watching.
    /// </summary>
    [InitializeOnLoad]
    static class RainEditModePreview
    {
        const double Interval = 1.0 / 60.0;

        static double nextTick;

        static RainEditModePreview()
        {
            EditorApplication.update += OnUpdate;
        }

        static void OnUpdate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (now < nextTick)
            {
                return;
            }

            if (!RainEffect.AnyEditModePreviewActive())
            {
                return;
            }

            nextTick = now + Interval;
            EditorApplication.QueuePlayerLoopUpdate();
            InternalEditorUtility.RepaintAllViews();
        }
    }
}
