using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RainDropEffect2.Scripts.Common;
using UnityEngine;

namespace RainDropEffect2.Scripts.RainBehaviours.FrictionFlowRain
{
    public class FrictionFlowRainController : MonoBehaviour
    {
        private const float Tolerance = 0.0001f;

        public FrictionFlowRainVariables Variables { get; set; }
        public int RenderQueue { get; set; }
        public UnityEngine.Camera Camera { get; set; }
        public float Alpha { get; set; }
        public Vector2 GlobalWind { get; set; }
        public Vector3 GForceVector { get; set; }
        public bool NoMoreRain { get; set; }
        public RainDropTools.RainDropShaderType ShaderType { get; set; }
        public float Distance { get; set; }
        public List<FrictionFlowRainDrawerContainer> drawers = new List<FrictionFlowRainDrawerContainer>();

        private Transform _dummy;
        private bool _isOneShot;
        private bool _isWaitingDelay;
        private float _oneShotTimeleft;
        private float _timeElapsed;
        private float _interval;

        public bool IsPlaying => drawers.FindAll(t => t.currentState == DrawState.Disabled).Count != drawers.Count;
        private Transform Dummy => _dummy == null ? _dummy = RainDropTools.CreateHiddenObject("dummy", transform) : _dummy;

        public void Refresh()
        {
            foreach (var d in drawers)
            {
                DestroyImmediate(d.drawer.gameObject);
            }

            drawers.Clear();

            for (var i = 0; i < Variables.maxRainSpawnCount; i++)
            {
                var container = new FrictionFlowRainDrawerContainer($"Friction Flow RainDrawer {i}", transform)
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

            foreach (var drawerContainer in drawers)
            {
                InitializeDrawer(drawerContainer);
                drawerContainer.currentState = DrawState.Disabled;
            }

            _isOneShot = Variables.playOnce;

            if (_isOneShot) _oneShotTimeleft = Variables.duration;
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
                    var container = new FrictionFlowRainDrawerContainer($"Friction Flow RainDrawer {drawers.Count + i}", transform) 
                        {currentState = DrawState.Disabled};
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
                    .Take(rmcnt - removeList.Count()));
            }

            foreach (var rem in removeList)
            {
                rem.drawer.Clear();
                DestroyImmediate(rem.drawer.gameObject);
            }

            drawers.RemoveAll(x => x.drawer == null);
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
            
            var spawnNum = (int) Mathf.Min(_timeElapsed / _interval,
                Variables.maxRainSpawnCount - drawers.FindAll(x => x.currentState == DrawState.Playing).Count);
            
            for (var i = 0; i < spawnNum; i++)
            {
                Spawn();
            }

            _interval = Variables.duration /
                        RainDropTools.Random(Variables.emissionRateMin, Variables.emissionRateMax);
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
        private float GetProgress(FrictionFlowRainDrawerContainer dc)
        {
            return dc.timeElapsed / dc.lifetime;
        }

        private void InitializeDrawer(FrictionFlowRainDrawerContainer dc)
        {
            dc.timeElapsed = 0f;
            dc.lifetime = RainDropTools.Random(Variables.lifetimeMin, Variables.lifetimeMax);
            dc.acceleration = RainDropTools.Random(Variables.accelerationMin, Variables.accelerationMax);
            dc.transform.localPosition = RainDropTools.GetSpawnLocalPos(transform, Camera, 0f, Variables.spawnOffsetY);
            dc.startPos = dc.transform.localPosition;
            dc.acceleration = RainDropTools.Random(Variables.accelerationMin, Variables.accelerationMax);

            if (ReferenceEquals(dc.drawer.material, null))
            {
                var mat = RainDropTools.CreateRainMaterial(ShaderType, RenderQueue);
                
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
                
                dc.drawer.material = mat;
            }

            dc.drawer.lifeTime = dc.lifetime;
            dc.drawer.vertexDistance = 0.01f;
            dc.drawer.angleDivisions = 20;
            dc.drawer.widthCurve = Variables.trailWidth;
            dc.drawer.widthMultiplier = RainDropTools.Random(Variables.sizeMinX, Variables.sizeMaxX);
            dc.drawer.textureMode = LineTextureMode.Stretch;
            dc.drawer.Clear();
            dc.drawer.enabled = false;
        }

        private void Shuffle<T>(IList<T> list)
        {
            var cnt = list.Count;
            
            while (cnt > 1)
            {
                cnt--;
                
                var rnd = RainDropTools.Random(0, cnt + 1);
                var value = list[rnd];
                list[rnd] = list[cnt];
                list[cnt] = value;
            }
        }

        private KeyValuePair<Vector3, float> PickRandomWeightedElement(Dictionary<Vector3, float> dictionary)
        {
            var kvList = dictionary.ToList();
            // If all the value is same, then we return a random element
            var firstVal = kvList[0].Value;
            
            if (kvList.FindAll(t => Math.Abs(t.Value - firstVal) < Tolerance).Count == kvList.Count)
            {
                Shuffle(kvList);
                return kvList[0];
            }

            kvList.Sort((x, y) => x.Value.CompareTo(y.Value));
            return kvList.FirstOrDefault(x => Math.Abs(x.Value - dictionary.Values.Max()) < Tolerance);
        }

        private Vector3 GetNextPositionWithFriction(FrictionFlowRainDrawerContainer dc, float downValue, int resolution,
            int widthResolution, float dt)
        {
            var drawerTransform = dc.drawer.transform;
            Dummy.parent = drawerTransform.parent;
            Dummy.localRotation = drawerTransform.localRotation;
            Dummy.localPosition = drawerTransform.localPosition;

            var texW = Variables.frictionMap.width;
            var texH = Variables.frictionMap.height;
            var iter = (int) (Mathf.Clamp(resolution * dt, 2, 5));

            var widthPixels = new Dictionary<Vector3, float>();

            // Get the gravity forced vector
            Vector3 downward = RainDropTools.GetGForcedScreenMovement(this.Camera.transform, this.GForceVector);
            downward = downward.normalized;

            var angle = Mathf.Rad2Deg * Mathf.Atan2(downward.y, downward.x);

            Dummy.localRotation = Quaternion.AngleAxis(angle + 90f, Vector3.forward);

            var step = downValue * (1f / iter) * 3f / widthResolution;
            var clampedResolution = Mathf.Clamp(2 * widthResolution, 2, 5);

            for (var i = 0; i < iter; i++)
            {
                Dummy.localPosition += downValue * (1f / iter) * new Vector3(downward.x, downward.y, 0f);

                for (var j = 0; j <= clampedResolution; j++)
                {
                    var ww = (j * step - (clampedResolution / 2f) * step);
                    var downPos = Dummy.TransformPoint(new Vector3(ww, 0f, 0f));
                    var downVector2ViewPoint = Camera.WorldToViewportPoint(downPos);

                    // Get the pixel grayscale
                    var pixel = Variables.frictionMap.GetPixel(
                        (int) (texW * downVector2ViewPoint.x),
                        (int) (texH * -downVector2ViewPoint.y)
                    ).grayscale;

                    // If never added to the list, we add it
                    if (!widthPixels.ContainsKey(downPos))
                    {
                        widthPixels.Add(downPos, 1.0f - pixel);
                    }
                }
            }

            Vector3 frictionWay = PickRandomWeightedElement(widthPixels).Key;
            frictionWay = dc.drawer.transform.parent.InverseTransformPoint(frictionWay);
            Dummy.parent = null;

            return frictionWay;
        }

        private void UpdateTransform(FrictionFlowRainDrawerContainer dc)
        {
            var progress = GetProgress(dc);
            var t = dc.timeElapsed;
            var downValue = 1 / 2f * t * t * dc.acceleration * 0.1f + Variables.initialVelocity * t * 0.01f;
            var nextPos = GetNextPositionWithFriction(dc, downValue, 150, 8, Time.deltaTime);
            nextPos = new Vector3(nextPos.x, nextPos.y, 0f);
            nextPos += progress * new Vector3(GlobalWind.x, GlobalWind.y, 0f);
            dc.drawer.vertexDistance = (1f * Distance * RainDropTools.GetCameraOrthographicSize(Camera).y) / (Variables.resolution * 10f);
            dc.transform.localPosition = nextPos;
        }

        private void UpdateShader(FrictionFlowRainDrawerContainer dc, int index)
        {
            var progress = GetProgress(dc);
            dc.drawer.material.renderQueue = RenderQueue + index;

            // Update shader if needed
            if (dc.drawer.material.shader.name != RainDropTools.GetShaderName(ShaderType))
            {
                dc.drawer.material = RainDropTools.CreateRainMaterial(ShaderType, RenderQueue + index);
            }

            var distortionValue =
                Variables.distortionValue * Variables.distortionOverLifetime.Evaluate(progress) * Alpha;
            var reliefValue = Variables.reliefValue * Variables.reliefOverLifetime.Evaluate(progress) * Alpha;
            var blurValue = Variables.blur * Variables.blurOverLifetime.Evaluate(progress) * Alpha;
            var bloomValue = Variables.bloom * Variables.bloomOverLifetime.Evaluate(progress) * Alpha;
            var overlayColor = new Color(
                Variables.overlayColor.r,
                Variables.overlayColor.g,
                Variables.overlayColor.b,
                Variables.overlayColor.a * Variables.alphaOverLifetime.Evaluate(progress) * Alpha
            );

            switch (ShaderType)
            {
                case RainDropTools.RainDropShaderType.Expensive:
                    if ((distortionValue + reliefValue + overlayColor.a + blurValue) / 4 < Tolerance)
                    {
                        dc.drawer.enabled = false;
                        return;
                    }

                    break;
                case RainDropTools.RainDropShaderType.Cheap:
                    if (Math.Abs(distortionValue) < Tolerance)
                    {
                        dc.drawer.enabled = false;
                        return;
                    }

                    break;
                case RainDropTools.RainDropShaderType.NoDistortion:
                    if (Math.Abs(reliefValue) < Tolerance && Math.Abs(overlayColor.a) < Tolerance)
                    {
                        dc.drawer.enabled = false;
                        return;
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            RainDropTools.ApplyRainMaterialValue(
                dc.drawer.material,
                ShaderType,
                Variables.normalMap,
                Variables.overlayTexture,
                distortionValue,
                overlayColor,
                reliefValue,
                blurValue,
                Variables.bloomTexture,
                bloomValue,
                Variables.darkness * Alpha
            );
            dc.drawer.enabled = true;
        }

        /// <summary>
        /// Update rain variables
        /// </summary>
        private void UpdateInstance(FrictionFlowRainDrawerContainer dc, int index)
        {
            if (dc.currentState != DrawState.Playing) return;
            
            if (GetProgress(dc) >= 1.0f)
            {
                dc.drawer.Clear();
                dc.currentState = DrawState.Disabled;
            }
            else
            {
                dc.timeElapsed += Time.deltaTime;
                UpdateTransform(dc);
                UpdateShader(dc, index);
            }
        }
    }
    
    public enum DrawState
    {
        Playing,
        Disabled,
    }
    
    [Serializable]
    public class FrictionFlowRainDrawerContainer : RainDrawerContainer<DropTrail>
    {
        public DrawState currentState = DrawState.Disabled;
        public float initRnd = 0f;
        public float posXDt;
        public float rnd1;
        public float fluctuationRate = 5f;
        public float acceleration = 0.1f;

        public Vector3 startPos;
        public float timeElapsed;
        public float lifetime;

        public bool IsEnable => drawer.material != null && drawer.enabled;

        public FrictionFlowRainDrawerContainer(string name, Transform parent) : base(name, parent)
        {
        }
    }
}