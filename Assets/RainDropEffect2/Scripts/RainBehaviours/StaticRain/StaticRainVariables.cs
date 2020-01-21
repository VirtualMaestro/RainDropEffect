using System;
using UnityEngine;

namespace RainDropEffect2.Scripts.RainBehaviours.StaticRain
{
    [Serializable]
    public class StaticRainVariables
    {
        private static AnimationCurve _defaultAnimationCurve = new AnimationCurve(new Keyframe(0, 0, 2, 2), 
            new Keyframe(0.5f, 0.1f, -0.25f, -0.25f), 
            new Keyframe(1f, 0.25f, 0f, 0f));
        
        public bool autoStart = true;
        public bool fullScreen = true;

        public Color overlayColor = new Color(0.6f, 0.9f, 1.0f, 0.1f);
        
        [Range(0.0f, 5.0f)] 
        public float darkness = 1f;

        public Texture overlayTexture;
        public Texture normalMap;

        [Range(0, 15f)] 
        public float fadeTime = 2f;
        public AnimationCurve fadeinCurve = _defaultAnimationCurve;

        [Range(0.01f, 20f)] 
        public float sizeX = 9.0f;
        [Range(0.01f, 20f)] 
        public float sizeY = 9.0f;

        [Range(-2, 2f)] 
        public float spawnOffsetX;
        [Range(-2, 2f)] 
        public float spawnOffsetY;

        [Range(0.05f, 200.0f)] 
        public float distortionValue = 50f;

        [Range(0.0f, 2.0f)] 
        public float reliefValue;

        [Range(0.0f, 2.0f)] 
        public float blur;

        public Texture bloomTexture;

        [Range(0.0f, 20.0f)] 
        public float bloom;
    }
}