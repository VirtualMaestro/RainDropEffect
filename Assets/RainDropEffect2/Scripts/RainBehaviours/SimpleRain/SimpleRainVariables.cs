using System;
using UnityEngine;

namespace RainDropEffect2.Scripts.RainBehaviours.SimpleRain
{
    [Serializable]
    public class SimpleRainVariables
    {
        public bool autoStart = true;
        public bool playOnce;

        public Color overlayColor = Color.gray;
        [Range(0.0f, 5.0f)] public float darkness;

        public Texture normalMap;
        public Texture overlayTexture;

        public bool autoRotate;

        public float duration = 1f;
        public float delay;

        public int maxRainSpawnCount = 30;

        [Range(-2, 2f)] public float spawnOffsetY;

        [Range(0f, 10.0f)] public float lifetimeMin = 0.6f;
        [Range(0f, 10.0f)] public float lifetimeMax = 1.4f;

        [Range(0, 50f)] public int emissionRateMax = 5;

        [Range(0, 50f)] public int emissionRateMin = 2;

        public AnimationCurve alphaOverLifetime;

        [Range(0.0f, 20f)] public float sizeMinX = 0.75f;
        [Range(0.0f, 20f)] public float sizeMaxX = 0.75f;
        [Range(0.0f, 20f)] public float sizeMinY = 0.75f;
        [Range(0.0f, 20f)] public float sizeMaxY = 0.75f;
        public AnimationCurve sizeOverLifetime;

        [Range(0.0f, 200.0f)] public float distortionValue;
        public AnimationCurve distortionOverLifetime;

        [Range(0.0f, 2.0f)] public float reliefValue;
        public AnimationCurve reliefOverLifetime;

        [Range(0.0f, 2.0f)] public float blur;
        public AnimationCurve blurOverLifetime;

        public Texture bloomTexture;

        [Range(0.0f, 20.0f)] public float bloom;
        public AnimationCurve bloomOverLifetime;

        public AnimationCurve posYOverLifetime;
    }
}