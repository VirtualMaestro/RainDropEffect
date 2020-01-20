using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RainDropEffect2.Scripts.Common;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RainDropEffect2.Scripts.RainBehaviours.SimpleRain
{
    public class SimpleRainController : MonoBehaviour
    {
        private const float Tolerance = 0.0001f;

        public SimpleRainVariables Variables { get; set; }
        public int RenderQueue { get; set; }
        public UnityEngine.Camera Camera { get; set; }
        public float Alpha { get; set; }
        public Vector2 GlobalWind { get; set; }
        public Vector3 GForceVector { get; set; }
        public bool NoMoreRain { get; set; }
        public RainDropTools.RainDropShaderType ShaderType { get; set; }
        public List<SimpleRainDrawerContainer> drawers = new List<SimpleRainDrawerContainer>();

        private bool _isOneShot;
        private float _oneShotTimeleft;
        private float _timeElapsed;
        private float _interval;
        private bool _isWaitingDelay;

        public bool IsPlaying => GetDrawersCountByState(DrawState.Disabled) != drawers.Count;

        public enum DrawState
        {
            Playing,
            Disabled,
        }

        public void Refresh()
        {
            foreach (var d in drawers)
            {
                d.Drawer.Hide();
                DestroyImmediate(d.Drawer.gameObject);
            }

            drawers.Clear();

            for (var i = 0; i < Variables.maxRainSpawnCount; i++)
            {
                var container = new SimpleRainDrawerContainer($"Simple RainDrawer {i}", transform)
                {
                    currentState = DrawState.Disabled
                };
                
                drawers.Add(container);
            }
        }

        /// <summary>
        /// Play this instance.
        /// </summary>
        public void Play()
        {
            StartCoroutine(PlayDelay(Variables.delay));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetDrawersCountByState(DrawState state)
        {
            var disabledCount = 0;
            var totalCount = drawers.Count;
                
            for (var i = 0; i < totalCount; i++)
            {
                if (drawers[i].currentState == state) 
                    ++disabledCount;
            }

            return disabledCount;
        }

        private IEnumerator PlayDelay(float delay)
        {
            var t = 0f;
            
            while (t <= delay)
            {
                _isWaitingDelay = true;
                t += Time.deltaTime;
                yield return null;
            }

            _isWaitingDelay = false;

            if (drawers.Find(x => x.currentState == DrawState.Playing) != null)
            {
                yield break;
            }

            foreach (var drawer in drawers)
            {
                InitializeDrawer(drawer);
                drawer.currentState = DrawState.Disabled;
            }

            _isOneShot = Variables.playOnce;
          
            if (_isOneShot)
            {
                _oneShotTimeleft = Variables.duration;
            }
        }

        public void UpdateController()
        {
            if (Variables == null) return;

            CheckSpawnNum();

            if (NoMoreRain)
            {
                _timeElapsed = 0f;
            }
            else if (_isOneShot)
            {
                _oneShotTimeleft -= Time.deltaTime;
                if (_oneShotTimeleft > 0f)
                {
                    CheckSpawnTime();
                }
            }
            else if (!_isWaitingDelay)
            {
                CheckSpawnTime();
            }

            for (var i = 0; i < drawers.Count; i++)
            {
                UpdateInstance(drawers[i], i);
            }
        }

        private void CheckSpawnNum()
        {
            var diff = Variables.maxRainSpawnCount - drawers.Count;

            // MaxRainSpawnCount was increased
            if (diff > 0)
            {
                for (var i = 0; i < diff; i++)
                {
                    var container =
                        new SimpleRainDrawerContainer("Simple RainDrawer " + (drawers.Count + i), transform)
                        {
                            currentState = DrawState.Disabled
                        };
                    
                    drawers.Add(container);
                }
            }

            // MaxRainSpawnCount was decreased
            if (diff >= 0) return;
            
            var rmcnt = -diff;
            var removeList = drawers.FindAll(x => x.currentState != DrawState.Playing).Take(rmcnt).ToList();
            
            if (removeList.Count < rmcnt)
            {
                removeList.AddRange(drawers.FindAll(x => x.currentState == DrawState.Playing)
                    .Take(rmcnt - removeList.Count));
            }

            foreach (var rem in removeList)
            {
                rem.Drawer.Hide();
                DestroyImmediate(rem.Drawer.gameObject);
            }

            drawers.RemoveAll(x => x.Drawer == null);
        }

        private void CheckSpawnTime()
        {
            if (Math.Abs(_interval) < Tolerance)
            {
                _interval = Variables.duration /
                           RainDropTools.Random(Variables.emissionRateMin, Variables.emissionRateMax);
            }

            _timeElapsed += Time.deltaTime;
            
            if (!(_timeElapsed >= _interval)) return;

            var spawnNum = (int) Mathf.Min(_timeElapsed / _interval, Variables.maxRainSpawnCount - GetDrawersCountByState(DrawState.Playing));
            
            for (var i = 0; i < spawnNum; i++)
            {
                Spawn();
            }

            _interval = Variables.duration / RainDropTools.Random(Variables.emissionRateMin, Variables.emissionRateMax);
            _timeElapsed = 0f;
        }

        private void Spawn()
        {
            var spawnRain = drawers.Find(x => x.currentState == DrawState.Disabled);
            if (spawnRain == null) return;

            InitializeDrawer(spawnRain);
            spawnRain.currentState = DrawState.Playing;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetProgress(SimpleRainDrawerContainer dc)
        {
            return dc.timeElapsed / dc.lifetime;
        }

        private void InitializeDrawer(SimpleRainDrawerContainer dc)
        {
            dc.timeElapsed = 0f;
            dc.lifetime = RainDropTools.Random(Variables.lifetimeMin, Variables.lifetimeMax);
            dc.transform.localPosition = RainDropTools.GetSpawnLocalPos(transform, Camera, 0f, Variables.spawnOffsetY);
            dc.startSize = new Vector3(
                RainDropTools.Random(Variables.sizeMinX, Variables.sizeMaxX),
                RainDropTools.Random(Variables.sizeMinY, Variables.sizeMaxY),
                1f
            );
            dc.transform.localEulerAngles +=
                Vector3.forward * (Variables.autoRotate ? Random.Range(0f, 179.9f) : 0f);
            dc.Drawer.NormalMap = Variables.normalMap;
            dc.Drawer.ReliefTexture = Variables.overlayTexture;
            dc.Drawer.Darkness = Variables.darkness;
            dc.Drawer.Hide();
        }

        private void UpdateShader(SimpleRainDrawerContainer dc, int index)
        {
            var progress = GetProgress(dc);
            dc.Drawer.RenderQueue = RenderQueue + index;
            dc.Drawer.NormalMap = Variables.normalMap;
            dc.Drawer.ReliefTexture = Variables.overlayTexture;
            dc.Drawer.OverlayColor = new Color(
                Variables.overlayColor.r,
                Variables.overlayColor.g,
                Variables.overlayColor.b,
                Variables.overlayColor.a * Variables.alphaOverLifetime.Evaluate(progress) * Alpha
            );
            dc.Drawer.DistortionStrength =
                Variables.distortionValue * Variables.distortionOverLifetime.Evaluate(progress) * Alpha;
            dc.Drawer.ReliefValue = Variables.reliefValue * Variables.reliefOverLifetime.Evaluate(progress) * Alpha;
            dc.Drawer.Blur = Variables.blur * Variables.blurOverLifetime.Evaluate(progress) * Alpha;
            dc.Drawer.BloomTexture = Variables.bloomTexture;
            dc.Drawer.Bloom = Variables.bloom * Variables.bloomOverLifetime.Evaluate(progress) * Alpha;
            dc.Drawer.Darkness = Variables.darkness * Alpha;
            dc.transform.localScale = dc.startSize * Variables.sizeOverLifetime.Evaluate(progress);
            
            var gForced = RainDropTools.GetGForcedScreenMovement(Camera.transform, GForceVector);
            gForced = gForced.normalized;
            
            var localPosition = dc.transform.localPosition;
            localPosition += new Vector3(-gForced.x, -gForced.y, 0f) * (0.01f * Variables.posYOverLifetime.Evaluate(progress));
            localPosition += progress * new Vector3(GlobalWind.x, GlobalWind.y, 0f);
            localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            dc.transform.localPosition = localPosition;
            dc.Drawer.ShaderType = ShaderType;
            dc.Drawer.Show();
        }

        /// <summary>
        /// Update rain variables
        /// </summary>
        private void UpdateInstance(SimpleRainDrawerContainer dc, int index)
        {
            if (dc.currentState != DrawState.Playing) return;
            if (GetProgress(dc) >= 1.0f)
            {
                dc.Drawer.Hide();
                dc.currentState = DrawState.Disabled;
            }
            else
            {
                dc.timeElapsed += Time.deltaTime;
                UpdateShader(dc, index);
            }
        }
    }
    
    [Serializable]
    public class SimpleRainDrawerContainer : RainDrawerContainer<RainDrawer>
    {
        public SimpleRainController.DrawState currentState = SimpleRainController.DrawState.Disabled;
        public Vector3 startSize;
        public float timeElapsed = 0f;
        public float lifetime = 0f;

        public SimpleRainDrawerContainer(string name, Transform parent) : base(name, parent)
        {
        }
    }
}