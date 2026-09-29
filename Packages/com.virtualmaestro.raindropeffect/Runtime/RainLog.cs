using System.Collections.Generic;
using UnityEngine;

namespace RainDropEffect
{
    /// <summary>
    /// Logging for the package. Verbose messages compile away unless the host defines
    /// RAINDROP_VERBOSE_LOG; warnings and errors are always compiled. Nothing here is called
    /// per frame (decision D17).
    /// </summary>
    public static class RainLog
    {
        const string Prefix = "[RainDropEffect] ";

        static readonly HashSet<string> once = new HashSet<string>();

        [System.Diagnostics.Conditional("RAINDROP_VERBOSE_LOG")]
        public static void Verbose(string message, Object context = null)
        {
            Debug.Log(Prefix + message, context);
        }

        public static void Warn(string message, Object context = null)
        {
            Debug.LogWarning(Prefix + message, context);
        }

        public static void Error(string message, Object context = null)
        {
            Debug.LogError(Prefix + message, context);
        }

        /// <summary>Warns once per key, so a per-frame condition cannot spam the console.</summary>
        public static void WarnOnce(string key, string message, Object context = null)
        {
            if (once.Add(key))
            {
                Warn(message, context);
            }
        }

        public static void ResetOnce()
        {
            once.Clear();
        }
    }
}
