using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RainDropEffect2.Scripts.Common;
using UnityEngine;

namespace RainDropEffect2.Scripts.RainBehaviours.FlowRain
{
    public class FlowRainController : MonoBehaviour
    {
        public FlowRainVariables Variables { get; set; }
        public int RenderQueue { get; set; }
        public UnityEngine.Camera Camera { get; set; }
        public float Alpha { get; set; }
        public Vector2 GlobalWind { get; set; }
        public Vector3 GForceVector { get; set; }
        public bool NoMoreRain { get; set; }
        public RainDropTools.RainDropShaderType ShaderType { get; set; }
        public float Distance { get; set; }

        private bool _isOneShot;
        private bool _isWaitingDelay;
        private float _oneShotTimeleft;
        private float _timeElapsed;
        private float _interval;

        public bool IsPlaying => drawers.FindAll(t => t.currentState == DrawState.Disabled).Count != drawers.Count;

        public enum DrawState
        {
            Playing,
            Disabled,
        }

        public List<FlowRainDrawerContainer> drawers = new List<FlowRainDrawerContainer>();

        public void Refresh()
        {
            foreach (var d in drawers)
            {
                DestroyImmediate(d.Drawer.gameObject);
            }

            drawers.Clear();

            for (int i = 0; i < Variables.maxRainSpawnCount; i++)
            {
                FlowRainDrawerContainer container = new FlowRainDrawerContainer("Flow RainDrawer " + i, transform)
                {
                    currentState = DrawState.Disabled
                };

                drawers.Add(container);
            }
        }

        public void Play()
        {
            StartCoroutine(PlayDelay(Variables.delay));
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

            if (_isOneShot) _oneShotTimeleft = Variables.duration;
        }

        public void UpdateController()
        {
            if (Variables == null) return;

            CheckSpawnNum();

            if (NoMoreRain) _timeElapsed = 0f;
            else if (_isOneShot)
            {
                _oneShotTimeleft -= Time.deltaTime;

                if (_oneShotTimeleft > 0f) CheckSpawnTime();
            }
            else if (!_isWaitingDelay) CheckSpawnTime();

            for (int i = 0; i < drawers.Count; i++)
            {
                UpdateInstance(drawers[i], i);
            }
        }

        private void CheckSpawnNum()
        {
            int diff = Variables.maxRainSpawnCount - drawers.Count;

            // MaxRainSpawnCount was increased
            if (diff > 0)
            {
                for (int i = 0; i < diff; i++)
                {
                    FlowRainDrawerContainer container = new FlowRainDrawerContainer("Flow RainDrawer " + (drawers.Count() + i), transform)
                    {
                        currentState = DrawState.Disabled
                    };
                    
                    drawers.Add(container);
                }
            }

            // MaxRainSpawnCount was decreased
            if (diff >= 0) return;

            int rmcnt = -diff;
            List<FlowRainDrawerContainer> removeList =
                drawers.FindAll(x => x.currentState != DrawState.Playing).Take(rmcnt).ToList();
            
            if (removeList.Count < rmcnt)
            {
                var range = drawers.FindAll(x => x.currentState == DrawState.Playing).Take(rmcnt - removeList.Count);
                removeList.AddRange(range);
            }

            foreach (var rem in removeList)
            {
                rem.Drawer.Clear();
                DestroyImmediate(rem.Drawer.gameObject);
            }

            drawers.RemoveAll(x => x.Drawer == null);
        }

        private void CheckSpawnTime()
        {
            if (Math.Abs(_interval) < 0.001)
            {
                _interval = Variables.duration /
                            RainDropTools.Random(Variables.emissionRateMin, Variables.emissionRateMax);
            }

            _timeElapsed += Time.deltaTime;

            if (!(_timeElapsed >= _interval)) return;

            var playingDrawers = drawers.FindAll(x => x.currentState == DrawState.Playing).Count;
            var spawnNum = (int) Mathf.Min((_timeElapsed / _interval), Variables.maxRainSpawnCount - playingDrawers);

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

        private float GetProgress(FlowRainDrawerContainer dc)
        {
            return dc.timeElapsed / dc.lifetime;
        }

        private void InitializeDrawer(FlowRainDrawerContainer dc)
        {
            dc.timeElapsed = 0f;
            dc.lifetime = RainDropTools.Random(Variables.lifetimeMin, Variables.lifetimeMax);
            dc.fluctuationRate = RainDropTools.Random(Variables.fluctuationRateMin, Variables.fluctuationRateMax);
            dc.acceleration = RainDropTools.Random(Variables.accelerationMin, Variables.accelerationMax);
            dc.transform.localPosition =
                RainDropTools.GetSpawnLocalPos(this.transform, Camera, 0f, Variables.spawnOffsetY);
            dc.startPos = dc.transform.localPosition;
            dc.acceleration = RainDropTools.Random(Variables.accelerationMin, Variables.accelerationMax);

            Material mat = RainDropTools.CreateRainMaterial(ShaderType, RenderQueue);

            RainDropTools.ApplyRainMaterialValue(
                mat,
                ShaderType,
                Variables.normalMap,
                Variables.overlayTexture,
                Variables.distortionValue,
                Variables.overlayColor,
                Variables.reliefValue,
                Variables.blur,
                Variables.bloomTexture,
                Variables.bloom,
                Variables.darkness
            );

            dc.Drawer.lifeTime = dc.lifetime;
            dc.Drawer.vertexDistance = 0.01f;
            dc.Drawer.angleDivisions = 20;
            dc.Drawer.material = mat;
            dc.Drawer.widthCurve = Variables.trailWidth;
            dc.Drawer.widthMultiplier = RainDropTools.Random(Variables.sizeMinX, Variables.sizeMaxX);
            dc.Drawer.textureMode = LineTextureMode.Stretch;
            dc.Drawer.vertexDistance = (1f * this.Distance * RainDropTools.GetCameraOrthographicSize(this.Camera).y) /
                                       (Variables.resolution * 10f);
            dc.Drawer.Clear();
            dc.Drawer.enabled = false;
        }

        private void UpdateTransform(FlowRainDrawerContainer dc)
        {
            Action initRnd = () =>
            {
                dc.rnd1 = RainDropTools.Random(-0.1f * Variables.amplitude, 0.1f * Variables.amplitude);
                dc.posXDt = 0f;
            };

            if (dc.posXDt == 0f)
            {
                StartCoroutine(
                    Wait(
                        0.01f,
                        0.01f,
                        (int) (1f / dc.fluctuationRate * 100),
                        () => { initRnd(); }
                    )
                );
            }

            dc.posXDt += 0.01f * Variables.smooth * Time.deltaTime;

            if (Math.Abs(dc.rnd1) < 0.0001) initRnd();

            var t = dc.timeElapsed;

            Vector3 downward = RainDropTools.GetGForcedScreenMovement(Camera.transform, GForceVector);
            downward = -downward.normalized;

            var localPosition = dc.transform.localPosition;
            var xp = Vector3.Slerp(localPosition, localPosition + downward * dc.rnd1, dc.posXDt).x;
            var yp = dc.startPos.y - downward.y * (1 / 2f) * t * t * dc.acceleration - Variables.initialVelocity * t;

            localPosition = new Vector3(xp, yp, 0.001f); // TODO: Work around
            localPosition += GetProgress(dc) * new Vector3(GlobalWind.x, GlobalWind.y, 0f);
            dc.transform.localPosition = localPosition;
        }

        private void UpdateShader(FlowRainDrawerContainer dc, int index)
        {
            float progress = GetProgress(dc);
            dc.Drawer.material.renderQueue = RenderQueue + index;

            // Update shader if needed
            if (dc.Drawer.material.shader.name != RainDropTools.GetShaderName(ShaderType))
            {
                dc.Drawer.material = RainDropTools.CreateRainMaterial(ShaderType, RenderQueue + index);
            }

            float distortionValue =
                Variables.distortionValue * Variables.distortionOverLifetime.Evaluate(progress) * Alpha;
            float reliefValue = Variables.reliefValue * Variables.reliefOverLifetime.Evaluate(progress) * Alpha;
            float blurValue = Variables.blur * Variables.blurOverLifetime.Evaluate(progress) * Alpha;
            float bloomValue = Variables.bloom * Variables.bloomOverLifetime.Evaluate(progress) * Alpha;
            Color overlayColor = new Color(
                Variables.overlayColor.r,
                Variables.overlayColor.g,
                Variables.overlayColor.b,
                Variables.overlayColor.a * Variables.alphaOverLifetime.Evaluate(progress) * Alpha
            );

            switch (ShaderType)
            {
                case RainDropTools.RainDropShaderType.Expensive:
                    if (distortionValue == 0f && reliefValue == 0f && overlayColor.a == 0f && blurValue == 0f)
                    {
                        dc.Drawer.enabled = false;
                        return;
                    }

                    break;
                case RainDropTools.RainDropShaderType.Cheap:
                    if (distortionValue == 0f)
                    {
                        dc.Drawer.enabled = false;
                        return;
                    }

                    break;
                case RainDropTools.RainDropShaderType.NoDistortion:
                    if (reliefValue == 0f && overlayColor.a == 0f)
                    {
                        dc.Drawer.enabled = false;
                        return;
                    }

                    break;
            }

            RainDropTools.ApplyRainMaterialValue(
                dc.Drawer.material,
                ShaderType,
                Variables.normalMap,
                Variables.overlayTexture,
                distortionValue,
                overlayColor,
                reliefValue,
                blurValue,
                Variables.bloomTexture,
                Variables.darkness * Alpha
            );
            
            dc.Drawer.enabled = true;
        }

        private void UpdateInstance(FlowRainDrawerContainer dc, int index)
        {
            if (dc.currentState != DrawState.Playing) return;

            if (GetProgress(dc) >= 1.0f)
            {
                dc.Drawer.Clear();
                dc.currentState = DrawState.Disabled;
            }
            else
            {
                dc.timeElapsed += Time.deltaTime;
                UpdateTransform(dc);
                UpdateShader(dc, index);
            }
        }

        IEnumerator Wait(float atLeast = 0.5f, float step = 0.1f, int rndMax = 20, Action callBack = null)
        {
            float elapsed = 0f;
            while (elapsed < atLeast)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            while (RainDropTools.Random(0, rndMax) != 0)
            {
                elapsed = 0f;
                while (elapsed < step)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            callBack?.Invoke();
        }
    }

    [Serializable]
    public class FlowRainDrawerContainer : RainDrawerContainer<DropTrail>
    {
        public FlowRainController.DrawState currentState = FlowRainController.DrawState.Disabled;
        public float posXDt;
        public float rnd1;
        public float fluctuationRate = 5f;
        public float acceleration = 0.1f;

        public Vector3 startPos;
        public float timeElapsed;
        public float lifetime;

        public bool IsEnable => Drawer.material != null && Drawer.enabled;

        public FlowRainDrawerContainer(string name, Transform parent) : base(name, parent)
        {
        }
    }
}