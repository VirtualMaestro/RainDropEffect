using UnityEngine;

namespace RainDropEffect2.Scripts.Common
{
    /// <summary>
    /// ABSTRACT Rain base.
    /// </summary>
    public abstract class RainBehaviourBase : MonoBehaviour
    {
        /// <summary>
        /// Rendering Queue
        /// </summary>
        public int depth;

        [HideInInspector] 
        public float alpha;

        [HideInInspector] 
        public RainDropTools.RainDropShaderType shaderType;

        [HideInInspector] 
        public bool vrMode;

        /// <summary>
        /// Rain distance from camera
        /// </summary>
        [HideInInspector] 
        public float distance;

        [HideInInspector] 
        public Vector3 gForceVector;

        /// <summary>
        /// Gets a value indicating whether this instance is playing.
        /// </summary>
        /// <value><c>true</c> if this instance is playing; otherwise, <c>false</c>.</value>
        public virtual bool IsPlaying => false;

        /// <summary>
        /// Gets a value indicating whether rain is shown on the screen.
        /// </summary>
        /// <value><c>true</c> if this instance is enabled; otherwise, <c>false</c>.</value>
        public virtual bool IsEnabled => false;

        public virtual int CurrentDrawCall => 0;
        public virtual int MaxDrawCall => 0;

        /// <summary>
        /// You can call this when you want to redraw rain
        /// </summary>
        public virtual void Refresh()
        {
        }

        /// <summary>
        /// Starts the rain increasingly.
        /// </summary>
        public virtual void StartRain()
        {
        }

        /// <summary>
        /// Stops the rain gradually.
        /// </summary>
        public virtual void StopRain()
        {
        }

        /// <summary>
        /// Stops the rain immediately.
        /// </summary>
        public virtual void StopRainImmediate()
        {
        }

        /// <summary>
        /// Applies the final depth.
        /// </summary>
        public virtual void ApplyFinalDepth(int finalDepth)
        {
        }

        /// <summary>
        /// Applies the global wind
        /// </summary>
        /// <param name="globalWind"></param>
        public virtual void ApplyGlobalWind(Vector2 globalWind)
        {
        }

        /// <summary>
        /// Unity's Awake
        /// </summary>
        public virtual void Awake()
        {
        }

        /// <summary>
        /// Unity's Update
        /// </summary>
        public virtual void Update()
        {
        }
    }
}