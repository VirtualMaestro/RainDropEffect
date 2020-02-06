using UnityEngine;

namespace RainDropEffect2.Scripts.Common
{
	[System.Serializable]
	public class RainDrawerContainer<T> where T : Component 
	{
		public T drawer; // Drawer controls mesh, render and shader
		public Transform transform;

		public RainDrawerContainer (string name, Transform parent) 
		{
			transform = RainDropTools.CreateHolder (name, parent);
			drawer = transform.gameObject.AddComponent <T> ();
		}
	}
}
