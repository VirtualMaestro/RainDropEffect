using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RainDropEffect2.Scripts.Common;
using UnityEditor;
using UnityEngine;

namespace RainDropEffect2.Scripts.RainBehaviours.FlowRain
{
    [ExecuteInEditMode]
    public class FlowRainBehaviour : RainBehaviourBase
    {
        private FlowRainController RainController { get; set; }

        /// <summary>
        /// The variables.
        /// </summary>
        [SerializeField] private FlowRainVariables variables;

        /// <summary>
        /// Gets the current draw call.
        /// </summary>
        /// <value>The current draw call.</value>
        public override int CurrentDrawCall =>
            RainController == null ? 0 : RainController.drawers.FindAll(x => x.Drawer.enabled).Count;

        /// <summary>
        /// Gets the max draw call.
        /// </summary>
        /// <value>The max draw call.</value>
        public override int MaxDrawCall => variables.maxRainSpawnCount;

        /// <summary>
        /// Gets a value indicating whether this instance is playing.
        /// </summary>
        /// <value><c>true</c> if this instance is playing; otherwise, <c>false</c>.</value>
        public override bool IsPlaying => RainController != null && RainController.IsPlaying;

        /// <summary>
        /// Gets a value indicating whether rain is shown on the screen.
        /// </summary>
        /// <value><c>true</c> if this instance is enabled; otherwise, <c>false</c>.</value>
        public override bool IsEnabled => Math.Abs(Alpha) > 0.0001 && CurrentDrawCall != 0;

        public override void Refresh()
        {
            if (RainController != null)
            {
                DestroyImmediate(RainController.gameObject);
                RainController = null;
            }

            RainController = CreateController();
            RainController.Refresh();
            RainController.NoMoreRain = true;
        }

        public override void StartRain()
        {
            if (RainController == null)
            {
                RainController = CreateController();
                RainController.Refresh();
            }

            RainController.NoMoreRain = false;
            RainController.Play();
        }

        public override void StopRain()
        {
            if (RainController == null) return;
            RainController.NoMoreRain = true;
        }

        public override void StopRainImmediate()
        {
            if (RainController == null) return;

            DestroyImmediate(RainController.gameObject);
            RainController = null;
        }

        public override void ApplyFinalDepth(int depth)
        {
            if (RainController == null) return;
            RainController.RenderQueue = depth;
        }

        public override void ApplyGlobalWind(Vector2 globalWind)
        {
            if (RainController == null) return;
            RainController.GlobalWind = globalWind;
        }

        private void Start()
        {
            if (Application.isPlaying && variables.autoStart)
            {
                StartRain();
            }
        }

        public override void Update()
        {
            InitParams();

            if (RainController == null) return;

            RainController.ShaderType = ShaderType;
            RainController.Alpha = Alpha;
            RainController.Distance = Distance;
            RainController.GForceVector = GForceVector;
            RainController.UpdateController();
        }

        private FlowRainController CreateController()
        {
            Transform tr = RainDropTools.CreateHiddenObject("Controller", transform);
            FlowRainController con = tr.gameObject.AddComponent<FlowRainController>();
            con.Variables = variables;
            con.Alpha = 0f;
            con.NoMoreRain = false;
            con.Camera = GetComponentInParent<UnityEngine.Camera>();
            return con;
        }

        /// <summary>
        /// (Internal) Initialize inspector params
        /// </summary>
        [Conditional("DEBUG")]
        private void InitParams()
        {
            if (variables == null) return;

            if (variables.maxRainSpawnCount < 0)
                variables.maxRainSpawnCount = 0;
            if (variables.sizeMinX > variables.sizeMaxX)
                Swap(ref variables.sizeMinX, ref variables.sizeMaxX);
            if (variables.fluctuationRateMin > variables.fluctuationRateMax)
                Swap(ref variables.fluctuationRateMin, ref variables.fluctuationRateMax);
            if (variables.lifetimeMin > variables.lifetimeMax)
                Swap(ref variables.lifetimeMin, ref variables.lifetimeMax);
            if (variables.accelerationMin > variables.accelerationMax)
                Swap(ref variables.accelerationMin, ref variables.accelerationMax);
            if (variables.emissionRateMin > variables.emissionRateMax)
                Swap(ref variables.emissionRateMin, ref variables.emissionRateMax);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Swap<T>(ref T a, ref T b)
        {
            var temp = a;
            a = b;
            b = temp;
        }

#if UNITY_EDITOR
        private static readonly Color Khaki = new Color(0.6f, 0.6f, 0.0f, 1f);
        private static readonly Color White = new Color(0f, 0.1f, 0.7f, 0.8f);

        private static readonly Color Aquamarine08 = new Color(0.5f, 0.9f, 0.8f, 0.8f);
        private static readonly Color Aquamarine02 = new Color(0.5f, 0.9f, 0.8f, 0.2f);
        private void OnDrawGizmos()
        {
            UnityEngine.Camera rainCam = GetComponentInParent<UnityEngine.Camera>();

            if (rainCam == null) return;

            if (RainController != null)
            {
                foreach (var dc in RainController.drawers)
                {
                    Gizmos.color = dc.IsEnable ? Khaki : White;
                    Gizmos.DrawWireSphere(dc.Drawer.transform.position, .5f);
                }
            }

            if (!Selection.Contains(gameObject)) return;

            float h = rainCam.orthographicSize * 2f;
            float w = h * rainCam.aspect;
            Vector3 center = transform.position + Vector3.up * (h * variables.spawnOffsetY);
            Vector3 size = new Vector3(w, h, rainCam.farClipPlane - rainCam.nearClipPlane + 0.1f);

            Gizmos.color = Aquamarine08;
            Gizmos.DrawWireCube(center, size);

            Gizmos.color = Aquamarine02;
            Gizmos.DrawCube(center, size);
        }
#endif
    }
}