using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RainDropEffect2.Scripts.Common;
using UnityEditor;
using UnityEngine;

namespace RainDropEffect2.Scripts.RainBehaviours.SimpleRain
{
    [ExecuteInEditMode]
    public class SimpleRainBehaviour : RainBehaviourBase
    {
        private const float Tolerance = 0.0001f;

        private SimpleRainController RainController { get; set; }

        [SerializeField] 
        private SimpleRainVariables variables;

        public override int CurrentDrawCall => RainController == null ? 0 : RainController.drawers.FindAll(x => x.drawer.IsEnabled).Count;
        public override int MaxDrawCall => variables.maxRainSpawnCount;
        public override bool IsPlaying => RainController != null && RainController.IsPlaying;

        /// <summary>
        /// Gets a value indicating whether rain is shown on the screen.
        /// </summary>
        public override bool IsEnabled => Math.Abs(alpha) > Tolerance && CurrentDrawCall != 0;

        public override void Refresh()
        {
            if (ReferenceEquals(RainController, null) == false)
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
            if (ReferenceEquals(RainController, null))
            {
                RainController = CreateController();
                RainController.Refresh();
            }

            RainController.NoMoreRain = false;
            RainController.Play();
        }

        public override void StopRain()
        {
            if (ReferenceEquals(RainController, null)) return;

            RainController.NoMoreRain = true;
        }

        public override void StopRainImmediate()
        {
            if (ReferenceEquals(RainController, null)) return;

            DestroyImmediate(RainController.gameObject);
            RainController = null;
        }

        public override void ApplyFinalDepth(int finalDepth)
        {
            if (ReferenceEquals(RainController, null)) return;

            RainController.RenderQueue = finalDepth;
        }

        public override void ApplyGlobalWind(Vector2 globalWind)
        {
            if (ReferenceEquals(RainController, null)) return;

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

            if (ReferenceEquals(RainController, null)) return;

            RainController.ShaderType = shaderType;
            RainController.Alpha = alpha;
            RainController.GForceVector = gForceVector;
            RainController.UpdateController();
        }

        private SimpleRainController CreateController()
        {
            var tr = RainDropTools.CreateHiddenObject("Controller", transform);
            var con = tr.gameObject.AddComponent<SimpleRainController>();
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
            if (variables.lifetimeMin > variables.lifetimeMax)
                Swap(ref variables.lifetimeMin, ref variables.lifetimeMax);
            if (variables.emissionRateMin > variables.emissionRateMax)
                Swap(ref variables.emissionRateMin, ref variables.emissionRateMax);
            if (variables.sizeMinX > variables.sizeMaxX)
                Swap(ref variables.sizeMinX, ref variables.sizeMaxX);
            if (variables.sizeMinY > variables.sizeMaxY)
                Swap(ref variables.sizeMinY, ref variables.sizeMaxY);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Swap<T>(ref T a, ref T b)
        {
            var temp = a;
            a = b;
            b = temp;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var rainCam = GetComponentInParent<UnityEngine.Camera>();

            if (rainCam == null) return;

            if (RainController != null)
            {
                foreach (var dc in RainController.drawers)
                {
                    if (dc.currentState == SimpleRainController.DrawState.Playing)
                        Gizmos.color = new Color(1f, 0.6f, 0.1f, 1f);
                    else
                        Gizmos.color = new Color(1f, 1f, 1f, 0.4f);

                    Gizmos.DrawWireSphere(dc.drawer.transform.position, .5f);
                }
            }

            if (Selection.Contains(gameObject))
            {
                float h = rainCam.orthographicSize * 2f;
                float w = h * rainCam.aspect;
                Vector3 p = transform.position + (Vector3.up * h * variables.spawnOffsetY);
                Gizmos.color = new Color(0.5f, 0.9f, 0.8f, 0.8f);
                Gizmos.DrawWireCube(p, new Vector3(w, h, rainCam.nearClipPlane - rainCam.nearClipPlane + 0.1f));
                Gizmos.color = new Color(0.5f, 0.9f, 0.8f, 0.2f);
                Gizmos.DrawCube(p, new Vector3(w, h, rainCam.farClipPlane - rainCam.nearClipPlane + 0.1f));
            }
        }
#endif
    }
}