using System.Collections;
using System.Collections.Generic;
using RainDropEffect2.Scripts.Camera;
using UnityEngine;

namespace RainDropEffect2.Demo.Scripts
{
    public class DemoScene1 : MonoBehaviour 
    {
        [SerializeField] 
        private List<RainCameraController> rainControllers;

        private void Awake()
        {
            // For mobile optimization, we should reduce the resolution on iOS & Android
#if UNITY_IOS || UNITY_ANDROID
		SetResolution (512);
		Screen.orientation = ScreenOrientation.LandscapeLeft;
		Application.targetFrameRate = 60;
#endif
        }

        private IEnumerator Start()
        {
            yield return null; // Since rains starts automatically, we have to wait for initialization.
            _StopAll();
        }

        private void OnGUI()
        {
            int index = 0;
            foreach (var con in rainControllers)
            {
                var isPressed = GUILayout.Button($"Rain[{index}]", GUILayout.Height(40), GUILayout.Width(150));
            
                if (isPressed)
                {
                    _StopAll();
                    con.Play();
                }
            
                index++;
            }
        }

        private void _StopAll()
        {
            foreach (var con in rainControllers)
            {
                con.StopImmediate();
            }
        }
    }
}
