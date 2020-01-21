using System;
using System.Runtime.CompilerServices;
using RainDropEffect2.Scripts.Common;
using UnityEngine;

namespace RainDropEffect2.Scripts.RainBehaviours.StaticRain
{
    public class StaticRainController : MonoBehaviour
    {
        private const float Tolerance = 0.0001f;

        public StaticRainVariables Variables { get; set; }
        public int RenderQueue { get; set; }
        public UnityEngine.Camera Camera { get; set; }
        public float Alpha { get; set; }
        public bool NoMoreRain { get; set; }
        public RainDropTools.RainDropShaderType ShaderType { get; set; }
        public bool VrMode { get; set; }
        public bool IsPlaying => staticDrawer.currentState == DrawState.Playing;
        public StaticRainDrawerContainer staticDrawer;

        public void Refresh()
        {
            if (staticDrawer != null)
            {
                DestroyImmediate(staticDrawer.drawer.gameObject);
            }

            staticDrawer = new StaticRainDrawerContainer("Static RainDrawer", transform)
            {
                currentState = DrawState.Disabled
            };
            
            InitializeInstance(staticDrawer);
        }

        public void Play()
        {
            if (staticDrawer.currentState == DrawState.Playing) return;

            InitializeInstance(staticDrawer);
        }

        public void UpdateController()
        {
            if (Variables == null) return;

            UpdateInstance(staticDrawer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetProgress(StaticRainDrawerContainer dc)
        {
            return dc.timeElapsed / Variables.fadeTime;
        }

        /// <summary>
        /// Initializes the rain instance.
        /// </summary>
        private void InitializeInstance(StaticRainDrawerContainer dc)
        {
            dc.timeElapsed = 0f;
            dc.drawer.NormalMap = Variables.normalMap;
            dc.drawer.ReliefTexture = Variables.overlayTexture;
            dc.drawer.Hide();
        }

        /// <summary>
        /// Update rain variables
        /// </summary>
        private void UpdateInstance(StaticRainDrawerContainer dc)
        {
            var fadeCurve = Variables.fadeinCurve;

            // Update time
            dc.timeElapsed = !NoMoreRain ? Mathf.Min(Variables.fadeTime, dc.timeElapsed + Time.deltaTime) : Mathf.Max(0f, dc.timeElapsed - Time.deltaTime);

            if (Math.Abs(dc.timeElapsed) < Tolerance)
            {
                dc.drawer.Hide();
                dc.currentState = DrawState.Disabled;
                return;
            }
         
            dc.currentState = DrawState.Playing;

            if (Variables.fullScreen)
            {
                var orthSize = RainDropTools.GetCameraOrthographicSize(this.Camera);
                var targetScale = new Vector3(
                    orthSize.x / 2f,
                    orthSize.y / 2f,
                    0f
                );
                
                if (VrMode) targetScale += Vector3.one * 0.02f;
                
                dc.transform.localScale = targetScale;
                dc.transform.localPosition = Vector3.zero;
            }
            else
            {
                dc.transform.localScale = new Vector3(
                    Variables.sizeX,
                    Variables.sizeY,
                    1f
                );

                var p = Camera.ScreenToWorldPoint(
                    new Vector3(
                        -Screen.width * Variables.spawnOffsetX + Screen.width / 2.0f,
                        -Screen.height * Variables.spawnOffsetY + Screen.height / 2.0f,
                        0f
                    ));

                var position = transform.InverseTransformPoint(p);
                dc.transform.localPosition = position - Vector3.forward * position.z;
            }

            var progress = GetProgress(dc);
            dc.drawer.RenderQueue = RenderQueue;
            dc.drawer.NormalMap = Variables.normalMap;
            dc.drawer.ReliefTexture = Variables.overlayTexture;
            dc.drawer.OverlayColor = new Color(
                Variables.overlayColor.r,
                Variables.overlayColor.g,
                Variables.overlayColor.b,
                Variables.overlayColor.a * fadeCurve.Evaluate(progress) * Alpha
            );

            dc.drawer.DistortionStrength = Variables.distortionValue * fadeCurve.Evaluate(progress) * Alpha;
            dc.drawer.ReliefValue = Variables.reliefValue * fadeCurve.Evaluate(progress) * Alpha;
            dc.drawer.Blur = Variables.blur * fadeCurve.Evaluate(progress) * Alpha;
            dc.drawer.BloomTexture = Variables.bloomTexture;
            dc.drawer.Bloom = Variables.bloom * fadeCurve.Evaluate(progress) * Alpha;
            dc.drawer.Darkness = Variables.darkness;
            dc.drawer.ShaderType = ShaderType;
            dc.drawer.Show();
        }
    }
    
    public enum DrawState
    {
        Playing,
        Disabled,
    }

    [Serializable]
    public class StaticRainDrawerContainer : RainDrawerContainer<RainDrawer>
    {
        public DrawState currentState = DrawState.Disabled;
        public float timeElapsed;

        public StaticRainDrawerContainer(string name, Transform parent) : base(name, parent) { }
    }
}