using System;
using UnityEngine;

namespace RainDropEffect2.Scripts.RainBehaviours.FrictionFlowRain
{
    [Serializable]
    public class FrictionFlowRainVariables
    {
        public bool autoStart = true;
        public bool playOnce;

        public Color overlayColor = Color.gray;
        
        [Range(0.0f, 5.0f)] 
        public float darkness;

        public Texture normalMap;
        public Texture overlayTexture;
        public Texture2D frictionMap;

        public float duration = 1f;
        public float delay;

        public int maxRainSpawnCount = 30;

        [Range(-2, 2f)] 
        public float spawnOffsetY;

        [Range(0f, 10.0f)] 
        public float lifetimeMin = 0.6f;
        [Range(0f, 10.0f)] 
        public float lifetimeMax = 1.4f;

        [Range(0, 50f)] 
        public int emissionRateMin = 2;
        [Range(0, 50f)] 
        public int emissionRateMax = 5;

        [Range(5, 1024)] 
        public int resolution = 500;

        public AnimationCurve alphaOverLifetime;

        [Range(0.0f, 20f)] 
        public float sizeMinX = 0.75f;
        [Range(0.0f, 20f)] 
        public float sizeMaxX = 0.75f;
        public AnimationCurve trailWidth;

        [Range(0.0f, 200.0f)] 
        public float distortionValue;
        public AnimationCurve distortionOverLifetime;

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
        public float initialVelocity;

        [Range(-5f, 5f)] 
        public float accelerationMin = 0.06f;

        [Range(-5f, 5f)]
        public float accelerationMax = 0.2f;
    }
}