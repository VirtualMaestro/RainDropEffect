using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RainDropEffect2.Scripts.Common;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RainDropEffect2.Scripts.RainBehaviours.FrictionFlowRain
{
    public class FrictionFlowRainController : IDisposable
    {
        private const float Tolerance = 0.0001f;

        public int RenderQueue { get; set; }
        public UnityEngine.Camera Camera { get; set; }
        public float Alpha { get; set; }
        public Vector2 GlobalWind { get; set; }
        public Vector3 GForceVector { get; set; }
        public bool NoMoreRain { get; set; }


        public float Distance { get; set; }
        public int NumDrawCalls => _drawers.FindAll(x => x.drawer.enabled).Count;

        private readonly FrictionFlowRainVariables _variables;
        private List<FrictionFlowRainDrawerContainer> _drawers;
        private Transform _dummy;
        private bool _isOneShot;
        private bool _isWaitingDelay;
        private float _oneShotTimeleft;
        private float _timeElapsed;
        private float _interval;
        private readonly FrictionFlowRainBehaviour _behaviour;
        private RainDropTools.RainDropShaderType _shaderType;
        private string _shaderName;

        public RainDropTools.RainDropShaderType ShaderType
        {
            get => _shaderType;
            set {
                _shaderType = value;
                _shaderName = RainDropTools.GetShaderName(value);
            }
        }

        public bool IsPlaying => _drawers.FindAll(t => t.currentState == DrawState.Disabled).Count != _drawers.Count;
        private Transform Dummy => _dummy == null ? _dummy = RainDropTools.CreateHolder("dummy", _behaviour.transform) : _dummy;

        public FrictionFlowRainController(FrictionFlowRainBehaviour behaviour, FrictionFlowRainVariables variables)
        {
            _drawers = new List<FrictionFlowRainDrawerContainer>();
            _behaviour = behaviour;
            _variables = variables;
            
            _InitDrawers(_drawers);
        }

        public void Refresh()
        {
            _DisposeDrawers(_drawers);
            _InitDrawers(_drawers);
        }

        public void Play()
        {
            _behaviour.StartCoroutine(PlayDelay(_variables.delay));
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

            if (_drawers.Exists(x => x.currentState == DrawState.Playing))
            {
                yield break;
            }

            foreach (var drawerContainer in _drawers)
            {
                _InitializeDrawer(drawerContainer);
                drawerContainer.currentState = DrawState.Disabled;
            }

            _isOneShot = _variables.playOnce;

            if (_isOneShot) _oneShotTimeleft = _variables.duration;
        }
        
        public void UpdateController()
        {
            if (_variables == null) return;

            _CheckSpawnNum();

            if (NoMoreRain)
            {
                _timeElapsed = 0f;
            }
            else if (_isOneShot)
            {
                _oneShotTimeleft -= Time.deltaTime;
                if (_oneShotTimeleft > 0f)
                {
                    _CheckSpawnTime();
                }
            }
            else if (!_isWaitingDelay)
            {
                _CheckSpawnTime();
            }

            for (var i = 0; i < _drawers.Count; i++)
            {
                _UpdateInstance(_drawers[i], i);
            }
        }

        private void _CheckSpawnNum()
        {
            var diff = _variables.maxRainSpawnCount - _drawers.Count;

            // MaxRainSpawnCount was increased
            if (diff > 0)
            {
                for (var i = 0; i < diff; i++)
                {
                    var container = new FrictionFlowRainDrawerContainer($"Friction Flow RainDrawer {_drawers.Count + i}", _behaviour.transform) 
                        {currentState = DrawState.Disabled};
                    _drawers.Add(container);
                }
            }

            // MaxRainSpawnCount was decreased
            if (diff >= 0) return;
            var rmcnt = -diff;
            var removeList = _drawers.FindAll(x => x.currentState != DrawState.Playing).Take(rmcnt).ToList();
            
            if (removeList.Count < rmcnt)
            {
                var playingDrawers = _drawers.FindAll(x => x.currentState == DrawState.Playing);
                var itemsToRemove = playingDrawers.Take(rmcnt - removeList.Count);
                removeList.AddRange(itemsToRemove);
            }

            foreach (var rem in removeList)
            {
                rem.drawer.Clear();
                Object.DestroyImmediate(rem.drawer.gameObject);
            }

            _drawers.RemoveAll(x => x.drawer == null);
        }

        private void _CheckSpawnTime()
        {
            if (_interval <= 0)
            {
                _interval = _variables.duration /
                            RainDropTools.Random(_variables.emissionRateMin, _variables.emissionRateMax);
            }

            _timeElapsed += Time.deltaTime;
            
            if (_timeElapsed < _interval) return;

            var playingDrawersCount = _drawers.FindAll(x => x.currentState == DrawState.Playing).Count;
            var spawnNum = (int) Mathf.Min(_timeElapsed / _interval, _variables.maxRainSpawnCount - playingDrawersCount);
            
            for (var i = 0; i < spawnNum; i++)
            {
                _Spawn();
            }

            var rndEmission = RainDropTools.Random(_variables.emissionRateMin, _variables.emissionRateMax);
            _interval = _variables.duration / rndEmission;
            _timeElapsed = 0f;
        }

        private void _Spawn()
        {
            var spawnRain = _drawers.Find(x => x.currentState == DrawState.Disabled);
            if (spawnRain == null) return;
           
            _InitializeDrawer(spawnRain);
            spawnRain.currentState = DrawState.Playing;
        }

        private void _InitializeDrawer(FrictionFlowRainDrawerContainer dc)
        {
            dc.timeElapsed = 0f;
            dc.lifetime = RainDropTools.Random(_variables.lifetimeMin, _variables.lifetimeMax);
            dc.acceleration = RainDropTools.Random(_variables.accelerationMin, _variables.accelerationMax);
            dc.transform.localPosition = RainDropTools.GetSpawnLocalPos(_behaviour.transform, Camera, 0f, _variables.spawnOffsetY);
            dc.startPos = dc.transform.localPosition;

            if (ReferenceEquals(dc.drawer.material, null))
            {
                var mat = RainDropTools.CreateRainMaterial(_shaderType, RenderQueue);
                
                RainDropTools.ApplyRainMaterialValue(
                    mat,
                    _shaderType,
                    _variables.normalMap,
                    _variables.overlayTexture,
                    _variables.distortionValue,
                    _variables.overlayColor,
                    _variables.reliefValue,
                    _variables.blur,
                    _variables.bloomTexture,
                    _variables.bloom,
                    _variables.darkness
                );
                
                dc.drawer.material = mat;
            }

            dc.drawer.lifeTime = dc.lifetime;
            dc.drawer.vertexDistance = 0.01f;
            dc.drawer.angleDivisions = 20;
            dc.drawer.widthCurve = _variables.trailWidth;
            dc.drawer.widthMultiplier = RainDropTools.Random(_variables.sizeMinX, _variables.sizeMaxX);
            dc.drawer.textureMode = LineTextureMode.Stretch;
            dc.drawer.Clear();
            dc.drawer.enabled = false;
        }

        private Vector3 _GetNextPositionWithFrictionCheap(FrictionFlowRainDrawerContainer dc, float downValue, int resolution,
            int widthResolution, float dt)
        {
            var drawerTransform = dc.drawer.transform;
            Dummy.rotation = drawerTransform.rotation;
            Dummy.position = drawerTransform.position;

            var iter = (int) Mathf.Clamp(resolution * dt, 2, 5);
            var step = downValue * (1f / iter) * 3f / widthResolution;
            var clampedResolution = Mathf.Clamp(2 * widthResolution, 2, 5);
            
            Dummy.localPosition += downValue * (1f / iter) * GForceVector;
            var j = Random.Range(0, clampedResolution);
            var ww = j * step - clampedResolution / 2f * step;

            return dc.drawer.transform.parent.InverseTransformPoint(Dummy.TransformPoint(ww, 0f, 0f));
        }
        
        // TODO: High consumption
        private Vector3 _GetNextPositionWithFriction(FrictionFlowRainDrawerContainer dc, float downValue, int resolution,
            int widthResolution, float dt)
        {
            var drawerTransform = dc.drawer.transform;
            Dummy.parent = drawerTransform.parent;
            Dummy.localRotation = drawerTransform.localRotation;
            Dummy.localPosition = drawerTransform.localPosition;
        
            var texW = _variables.frictionMap.width;
            var texH = _variables.frictionMap.height;
            var iter = (int) Mathf.Clamp(resolution * dt, 2, 5);
        
            var widthPixels = new Dictionary<Vector3, float>();
        
            // Get the gravity forced vector
            Vector3 downward = RainDropTools.GetGForcedScreenMovement(Camera.transform, GForceVector);
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
                    var downPos = Dummy.TransformPoint(ww, 0f, 0f);
                    var downVector2ViewPoint = Camera.WorldToViewportPoint(downPos);
        
                    // Get the pixel grayscale
                    var pixel = _variables.frictionMap.GetPixel(
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
        
            Vector3 frictionWay = _PickRandomWeightedElement(widthPixels).Key;
            frictionWay = dc.drawer.transform.parent.InverseTransformPoint(frictionWay);
            Dummy.parent = null;
        
            return frictionWay;
        }
        
        private KeyValuePair<Vector3, float> _PickRandomWeightedElement(Dictionary<Vector3, float> dictionary)
        {
            var kvList = dictionary.ToList();
            // If all the value is same, then we return a random element
            var firstVal = kvList[0].Value;
            
            if (kvList.FindAll(t => Math.Abs(t.Value - firstVal) < Tolerance).Count == kvList.Count)
            {
                _Shuffle(kvList);
                return kvList[0];
            }

            kvList.Sort((x, y) => x.Value.CompareTo(y.Value));
            return kvList.FirstOrDefault(x => Math.Abs(x.Value - dictionary.Values.Max()) < Tolerance);
        }
        
        private void _Shuffle<T>(IList<T> list)
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
        
        /// <summary>
        /// Update rain variables
        /// </summary>
        private void _UpdateInstance(FrictionFlowRainDrawerContainer dc, int index)
        {
            if (dc.currentState != DrawState.Playing) return;
            
            if (dc.Progress >= 1.0f)
            {
                dc.drawer.Clear();
                dc.currentState = DrawState.Disabled;
            }
            else
            {
                dc.timeElapsed += Time.deltaTime;
                _UpdateTransform(dc);
                _UpdateShader(dc, index);
            }
        }

        private void _UpdateTransform(FrictionFlowRainDrawerContainer dc)
        {
            var progress = dc.Progress;
            var t = dc.timeElapsed;
            var downValue = 1 / 2f * t * t * dc.acceleration * 0.1f + _variables.initialVelocity * t * 0.01f;
            var nextPos = _shaderType == RainDropTools.RainDropShaderType.Cheap
                ? _GetNextPositionWithFrictionCheap(dc, downValue, 150, 8, Time.deltaTime)
                : _GetNextPositionWithFriction(dc, downValue, 150, 8, Time.deltaTime);
            
            nextPos.x += progress * GlobalWind.x;
            nextPos.y += progress * GlobalWind.y;
            nextPos.z = 0;

            RainDropTools.CalculateCameraOrthographicSize(Camera, out var x, out var y);
            dc.drawer.vertexDistance = (Distance * y) / (_variables.resolution * 10f);
            dc.transform.localPosition = nextPos;
        }

        private void _UpdateShader(FrictionFlowRainDrawerContainer dc, int index)
        {
            var progress = dc.Progress;
            dc.drawer.material.renderQueue = RenderQueue + index;

            // Update shader if needed
            if (dc.drawer.material.shader.name != _shaderName)
            {
                dc.drawer.material = RainDropTools.CreateRainMaterial(_shaderType, RenderQueue + index);
            }

            var distortionValue = _variables.distortionValue * _variables.distortionOverLifetime.Evaluate(progress) * Alpha;
            var reliefValue = _variables.reliefValue * _variables.reliefOverLifetime.Evaluate(progress) * Alpha;
            var blurValue = _variables.blur * _variables.blurOverLifetime.Evaluate(progress) * Alpha;
            var bloomValue = _variables.bloom * _variables.bloomOverLifetime.Evaluate(progress) * Alpha;
            var overlayColor = new Color(
                _variables.overlayColor.r,
                _variables.overlayColor.g,
                _variables.overlayColor.b,
                _variables.overlayColor.a * _variables.alphaOverLifetime.Evaluate(progress) * Alpha
            );

            switch (_shaderType)
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
                _shaderType,
                _variables.normalMap,
                _variables.overlayTexture,
                distortionValue,
                overlayColor,
                reliefValue,
                blurValue,
                _variables.bloomTexture,
                bloomValue,
                _variables.darkness * Alpha
            );
            dc.drawer.enabled = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _InitDrawers(List<FrictionFlowRainDrawerContainer> drawersList)
        {
            for (var i = 0; i < _variables.maxRainSpawnCount; i++)
            {
                var container = new FrictionFlowRainDrawerContainer($"Friction Flow RainDrawer Container {i}", _behaviour.transform)
                {
                    currentState = DrawState.Disabled
                };

                drawersList.Add(container);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _DisposeDrawers(List<FrictionFlowRainDrawerContainer> drawersList)
        {
            foreach (var d in drawersList)
            {
                Object.DestroyImmediate(d.drawer.gameObject);
            }

            drawersList.Clear();
        }

        public void Dispose()
        {
            _DisposeDrawers(_drawers);
            _drawers = null;
            Camera = null;
        }
        
#if UNITY_EDITOR
        public List<FrictionFlowRainDrawerContainer> Drawers => _drawers;
#endif
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
        public float Progress => timeElapsed / lifetime;

        public FrictionFlowRainDrawerContainer(string name, Transform parent) : base(name, parent)
        {
        }
    }
}