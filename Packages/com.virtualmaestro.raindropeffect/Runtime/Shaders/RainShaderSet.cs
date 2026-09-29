using UnityEngine;

namespace RainDropEffect
{
    /// <summary>
    /// Shader references shipped with the package. Held as an asset so runtime code never calls
    /// a lookup by shader name, and the shaders are always pulled into a player build.
    /// </summary>
    public sealed class RainShaderSet : ScriptableObject
    {
        public Shader Lens;
        public Shader Blur;
    }
}
