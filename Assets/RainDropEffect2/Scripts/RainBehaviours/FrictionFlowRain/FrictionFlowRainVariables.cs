using System;
using UnityEngine;

namespace RainDropEffect2.Scripts.RainBehaviours.FrictionFlowRain
{
    [Serializable]
    public class FrictionFlowRainVariables
    {
        private static AnimationCurve _defaultAnimationCurve = new AnimationCurve(new Keyframe(0, 0, 2, 2), 
            new Keyframe(0.3f, 1f, -0.25f, -0.25f), 
            new Keyframe(1f, 0f, 0f, 0f));
        
        public bool autoStart = true;
        public bool playOnce;

        public Color overlayColor = new Color(1f,1f,1f, 0.1f);
        
        [Range(0.0f, 5.0f)] 
        public float darkness = 4;

        public Texture normalMap;
        public Texture overlayTexture;
        public Texture2D frictionMap;

        public float duration = 1f;
        public float delay;

        public int maxRainSpawnCount = 30;

        [Range(-2, 2f)] 
        public float spawnOffsetY = 0.2f;

        [Range(0f, 10.0f)] 
        public float lifetimeMin = 1.9f;
        [Range(0f, 10.0f)] 
        public float lifetimeMax = 2.2f;

        [Range(0, 50f)] 
        public int emissionRateMin = 4;
        [Range(0, 50f)] 
        public int emissionRateMax = 5;

        [Range(5, 1024)] 
        public int resolution = 500;

        public AnimationCurve alphaOverLifetime = _defaultAnimationCurve;

        [Range(0.0f, 20f)] 
        public float sizeMinX = 0.5f;
        [Range(0.0f, 20f)] 
        public float sizeMaxX = 0.55f;
        public AnimationCurve trailWidth = _defaultAnimationCurve;

        [Range(0.0f, 200.0f)] 
        public float distortionValue = 100;
        public AnimationCurve distortionOverLifetime = _defaultAnimationCurve;

        [Range(0.0f, 2.0f)] 
        public float reliefValue;
        public AnimationCurve reliefOverLifetime;

        [Range(0.0f, 20.0f)] 
        public float blur;
        public AnimationCurve blurOverLifetime;

        public Texture bloomTexture;

        [Range(0.0f, 20.0f)] 
        public float bloom;
        public AnimationCurve bloomOverLifetime;

        [Range(-40f, 40f)] 
        public float initialVelocity = 13f;

        [Range(-5f, 5f)] 
        public float accelerationMin = 0.4f;

        [Range(-5f, 5f)]
        public float accelerationMax = 0.5f;
    }
}