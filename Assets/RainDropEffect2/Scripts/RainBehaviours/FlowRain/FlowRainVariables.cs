using UnityEngine;

namespace RainDropEffect2.Scripts.RainBehaviours.FlowRain
{
	[System.Serializable]
	public class FlowRainVariables {

		public bool autoStart = true;
		public bool playOnce;

		public Color overlayColor = Color.gray;
		[Range(0.0f, 5.0f)]
		public float darkness;

		public Texture normalMap;
		public Texture overlayTexture;

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
		public int emissionRateMax = 5;

		[Range(0, 50f)]
		public int emissionRateMin = 2;

		[Range(5, 500)]
		public float resolution = 200;

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

		[Range(0f, 20.0f)]
		public float amplitude = 5f;

		[Range(0f, 10.0f)]
		public float smooth = 5f;

		[Range(0f, 60.0f)]
		public float fluctuationRateMin = 5f;

		[Range(0f, 60.0f)]
		public float fluctuationRateMax = 5f;

		[Range(-20f, 20f)]
		public float initialVelocity;

		[Range(-5f, 5f)]
		public float accelerationMin = 0.06f;

		[Range(-5f, 5f)]
		public float accelerationMax = 0.2f;
	}
}