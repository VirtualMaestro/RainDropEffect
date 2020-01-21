using System;
using UnityEngine;

namespace RainDropEffect2.Scripts.RainBehaviours.SimpleRain
{
    [Serializable]
    public class SimpleRainVariables
    {
        private static AnimationCurve _defaultAnimationCurve = new AnimationCurve(new Keyframe(0, 0, 2, 2), 
            new Keyframe(0.3f, 1f, -0.25f, -0.25f), 
            new Keyframe(1f, 0f, 0f, 0f));
        
        public bool autoStart = true;
        public bool playOnce;

        public Color overlayColor = new Color(0.8f, 0.8f, 0.8f, 0.2f);
        
        [Range(0.0f, 5.0f)] 
        public float darkness = 4f;

        public Texture normalMap;
        public Texture overlayTexture;

        public bool autoRotate = true;

        public float duration = 1f;
        public float delay;

        public int maxRainSpawnCount = 30;

        [Range(-2, 2f)] 
        public float spawnOffsetY;

        [Range(0f, 10.0f)] 
        public float lifetimeMin = 0.7f;
        [Range(0f, 10.0f)] 
        public float lifetimeMax = 0.9f;

        [Range(0, 50f)] 
        public int emissionRateMin = 15;
        [Range(0, 50f)] 
        public int emissionRateMax = 17;

        public AnimationCurve alphaOverLifetime = _defaultAnimationCurve;

        [Range(0.0f, 20f)] 
        public float sizeMinX = 0.26f;
        [Range(0.0f, 20f)] 
        public float sizeMaxX = 0.35f;
        [Range(0.0f, 20f)] 
        public float sizeMinY = 0.26f;
        [Range(0.0f, 20f)] 
        public float sizeMaxY = 0.35f;
        public AnimationCurve sizeOverLifetime = _defaultAnimationCurve;

        [Range(0.0f, 200.0f)] 
        public float distortionValue = 100f;
        public AnimationCurve distortionOverLifetime = _defaultAnimationCurve;

        [Range(0.0f, 2.0f)] 
        public float reliefValue;
        public AnimationCurve reliefOverLifetime = _defaultAnimationCurve;

        [Range(0.0f, 2.0f)] 
        public float blur;
        public AnimationCurve blurOverLifetime;

        public Texture bloomTexture;

        [Range(0.0f, 20.0f)] 
        public float bloom;
        
        public AnimationCurve bloomOverLifetime;
        public AnimationCurve posYOverLifetime;
    }
}