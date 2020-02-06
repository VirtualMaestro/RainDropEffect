using System;
using RainDropEffect2.Scripts.Common;
using UnityEditor;
using UnityEngine;

namespace RainDropEffect2.Scripts.RainBehaviours.StaticRain
{
    [ExecuteInEditMode]
    public class StaticRainBehaviour : RainBehaviourBase
    {
        private const float Tolerance = 0.0001f;

        private StaticRainController RainController { get; set; }

        [SerializeField] 
        public StaticRainVariables variables;

        public override int CurrentDrawCall => RainController == null ? 0 : 1;
        public override int MaxDrawCall => 1;

        public override bool IsPlaying => !ReferenceEquals(RainController, null) && RainController.IsPlaying;

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
            Refresh(); // Work around TODO: fix initialize bug
        }

        public override void ApplyFinalDepth(int finalDepth)
        {
            if (ReferenceEquals(RainController, null)) return;

            RainController.RenderQueue = finalDepth;
        }

        public override void Awake()
        {
            if (Application.isPlaying)
            {
                Refresh(); // Work around TODO: fix initialize bug
            }
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
            if (ReferenceEquals(RainController, null)) return;

            RainController.ShaderType = shaderType;
            RainController.Alpha = alpha;
            RainController.VrMode = vrMode;
            RainController.UpdateController();
        }

        private StaticRainController CreateController()
        {
            var tr = RainDropTools.CreateHolder("Controller", transform);
            var con = tr.gameObject.AddComponent<StaticRainController>();
            con.Variables = variables;
            con.Alpha = 0f;
            con.NoMoreRain = false;
            con.Camera = GetComponentInParent<UnityEngine.Camera>();
            return con;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            UnityEngine.Camera rainCam = GetComponentInParent<UnityEngine.Camera>();

            if (rainCam == null)
                return;

            if (RainController != null)
            {
                if (RainController.staticDrawer.currentState == DrawState.Playing)
                    Gizmos.color = new Color(0.6f, 0.0f, 0.1f, 1f);
                else
                    Gizmos.color = new Color(1f, 1f, 1f, 0.4f);

                Gizmos.DrawWireCube(RainController.staticDrawer.transform.position, new Vector3(1f, 1f, 0f));
            }

            if (Selection.Contains(gameObject))
            {
                float h = rainCam.orthographicSize * 2f;
                float w = h * rainCam.aspect;
                Vector3 p = transform.position - (Vector3.up * h * variables.spawnOffsetY) -
                            (Vector3.right * w * variables.spawnOffsetX);

                Vector3 size = new Vector3(
                    variables.sizeX * 2f,
                    variables.sizeY * 2f,
                    1f
                );
                if (variables.fullScreen)
                {
                    size = new Vector3(w, h, 1f);
                }

                Gizmos.color = new Color(0.5f, 0.9f, 0.8f, 0.8f);
                Gizmos.DrawWireCube(p,
                    new Vector3(size.x, size.y, rainCam.nearClipPlane - rainCam.nearClipPlane + 0.1f));
                Gizmos.color = new Color(0.5f, 0.9f, 0.8f, 0.2f);
                Gizmos.DrawCube(p, new Vector3(size.x, size.y, rainCam.farClipPlane - rainCam.nearClipPlane + 0.1f));
            }
        }
#endif
    }
}