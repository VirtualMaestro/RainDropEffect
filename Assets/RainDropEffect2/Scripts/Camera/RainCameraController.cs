//#define SHOW_HIDED

using System.Collections.Generic;
using System.Linq;
using RainDropEffect2.Scripts.Common;
using UnityEngine;

namespace RainDropEffect2.Scripts.Camera
{
	[ExecuteInEditMode]
	public class RainCameraController : MonoBehaviour {
		private static readonly Color Blue01 = new Color(0f, 0.1f, 0.7f, 0.1f);
		private static readonly Color Blue08 = new Color(0f, 0.1f, 0.7f, 0.8f);

		private UnityEngine.Camera _cam;
		private UnityEngine.Camera Camera => _cam == null ? (_cam = GetComponent<UnityEngine.Camera> ()) : _cam;

		private List <RainBehaviourBase> _rainBehaviours;

		private List<RainBehaviourBase> RainBehaviours => _rainBehaviours ?? (_rainBehaviours = GetComponentsInChildren<RainBehaviourBase>(false).ToList());

		/// <summary>
		/// The render queue.
		/// </summary>
		[SerializeField]
		private int renderQueue = 3000;

		/// <summary>
		/// The alpha.
		/// </summary>
		[Range (0f, 1f)]
		public float alpha = 1f;

		/// <summary>
		/// The global wind.
		/// </summary>
		[SerializeField]
		private Vector2 globalWind = Vector3.zero;

		/// <summary>
		/// Gravity vector
		/// </summary>
		[SerializeField]
		public Vector3 gForceVector = Vector3.down;

		[SerializeField]
		private RainDropTools.RainDropShaderType shaderType;

		[SerializeField]
		[Range(0.02f, 10f)]
		private float distance = 8.3f;

		[SerializeField]
		public bool vrMode;

		/// <summary>
		/// Gets the current draw call.
		/// </summary>
		/// <value>The current draw call.</value>
		public int CurrentDrawCall => RainBehaviours.Select(x => x.CurrentDrawCall).Sum();

		/// <summary>
		/// Gets the max draw call.
		/// </summary>
		/// <value>The max draw call.</value>
		public int MaxDrawCall => RainBehaviours.Select (x => x.MaxDrawCall).Sum ();

		/// <summary>
		/// Gets a value indicating whether this instance is playing.
		/// </summary>
		/// <value><c>true</c> if this instance is playing; otherwise, <c>false</c>.</value>
		public bool IsPlaying => RainBehaviours.FindAll (x => x.IsPlaying).Count != 0;

		private void Awake ()
		{
			foreach (var beh in RainBehaviours)
			{
				beh.StopRainImmediate ();
			}
		}

		private void Start () 
		{
			if (Camera != null) return;
			Debug.LogError ("You must add component (Camera)");
		}

		private void Update () 
		{
			if (Camera == null) return;
        
			Camera.orthographic = !vrMode;
			Camera.orthographicSize = 5f;
			Camera.nearClipPlane = 0.01f;
			Camera.farClipPlane = distance + 0.01f;

			if (transform.childCount != _rainBehaviours.Count) _rainBehaviours = null; 
				
			RainBehaviours.Sort ((a, b) => a.depth - b.depth);

			int cnt = 0;
			
			foreach (var beh in RainBehaviours)
			{
				var rainTransform = beh.transform;
				rainTransform.localRotation = Quaternion.Euler(Vector3.zero);
				rainTransform.localScale = Vector3.one;
				
				if (Application.isPlaying)
				{
					var frustumHeight = 2.0f * distance * Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
					Camera.orthographicSize = frustumHeight * .5f;
					rainTransform.localPosition = Vector3.forward * distance;
				}
				else
				{
					rainTransform.localPosition = Vector3.zero;
				}
				
				beh.shaderType = shaderType;
				beh.vrMode = vrMode;
				beh.distance = distance;
				beh.ApplyFinalDepth (renderQueue + cnt);
				beh.ApplyGlobalWind (globalWind);
				beh.gForceVector = gForceVector;
				beh.alpha = alpha;
				cnt += beh.MaxDrawCall;
			}
		}

		/// <summary>
		/// You can call this when you want to redraw rain
		/// </summary>
		public void Refresh ()
		{
			foreach (var beh in RainBehaviours)
			{
				beh.StopRainImmediate ();
			}
			
			_rainBehaviours = GetComponentsInChildren <RainBehaviourBase> (false).ToList ();
			
			foreach (var beh in RainBehaviours)
			{
				beh.Refresh ();
			}
		}

		/// <summary>
		/// Starts the rain increasingly.
		/// </summary>
		public void Play ()
		{
			foreach (var beh in RainBehaviours)
			{
				beh.StartRain ();
			}
		}

		/// <summary>
		/// Stops the rain gradually.
		/// </summary>
		public void Stop () 
		{
			foreach (var beh in RainBehaviours)
			{
				beh.StopRain ();
			}
		}

		/// <summary>
		/// Stops the rain immediately.
		/// </summary>
		public void StopImmediate () 
		{
			foreach (var beh in RainBehaviours)
			{
				beh.StopRainImmediate ();
			}
		}

		private void OnDrawGizmos()
		{
			if (Camera == null) return;

			float h = Camera.orthographicSize * 2f;
			float w = h * Camera.aspect;

			var position = transform.position;
			Gizmos.color = Blue01;
			Gizmos.DrawCube(position, new Vector3(w, h, Camera.farClipPlane - Camera.nearClipPlane));
			
			Gizmos.color = Blue08;
			Gizmos.DrawWireCube(position, new Vector3(w, h, Camera.farClipPlane - Camera.nearClipPlane));
		}
	}
}
