using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace RainDropEffect2.Scripts.Common
{
    [ExecuteInEditMode]
    public class RainDrawer : MonoBehaviour
    {
        private const float Tolerance = 0.0001f;

        [NonSerialized] 
        public int RenderQueue = 3000;

        [NonSerialized] 
        public Vector3 CameraPos;

        [NonSerialized] 
        public Color OverlayColor;

        [NonSerialized] 
        public Texture NormalMap;

        [NonSerialized] 
        public Texture ReliefTexture;

        [NonSerialized] 
        public float DistortionStrength;

        [NonSerialized] 
        public float ReliefValue;

        [NonSerialized] 
        public float Shiness;

        [NonSerialized] 
        public float Blur;

        [NonSerialized] 
        public Texture BloomTexture;

        [NonSerialized] 
        public float Bloom;

        [NonSerialized] 
        public float Darkness;

        [NonSerialized] 
        public RainDropTools.RainDropShaderType ShaderType;

        private Material _material;
        private MeshFilter _meshFilter;
        private Mesh _mesh;
        private MeshRenderer _meshRenderer;
        private bool _changed;

        public bool IsEnabled => _meshRenderer != null && _meshRenderer.enabled;

        public void Refresh()
        {
            _changed = true;
        }

        public void Hide()
        {
            if (ReferenceEquals(_meshRenderer, null)) return;
            _meshRenderer.enabled = false;
        }

        public void Show()
        {
            if (_changed)
            {
                DestroyImmediate(_meshRenderer);
                DestroyImmediate(_meshFilter);
                _meshRenderer = null;
                _meshFilter = null;
                _material = null;
                _mesh = null;
                _changed = false;
            }

            if (ReferenceEquals(NormalMap, null))
            {
                Debug.LogError("Normal Map is null!");
                Hide();
                return;
            }

            if (ShaderType == RainDropTools.RainDropShaderType.Cheap && Math.Abs(DistortionStrength) < Tolerance)
            {
                Hide();
                return;
            }

            if ((DistortionStrength + ReliefValue + OverlayColor.a + Blur + Bloom) / 5 < Tolerance)
            {
                Hide();
                return;
            }

            if (ReferenceEquals(_material, null))
            {
                _material = RainDropTools.CreateRainMaterial(ShaderType, RenderQueue);
            }

            if (ReferenceEquals(_meshFilter, null))
            {
                _meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            if (ReferenceEquals(_meshRenderer, null))
            {
                _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            if (ReferenceEquals(_mesh, null))
            {
                _mesh = RainDropTools.CreateQuadMesh();
            }

            // Update shader if needed
            if (_material.shader.name != RainDropTools.GetShaderName(ShaderType))
            {
                _material = RainDropTools.CreateRainMaterial(ShaderType, _material.renderQueue);
            }

            _meshFilter.mesh = _mesh;
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.material = _material;
            _meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            _meshRenderer.enabled = true;

            RainDropTools.ApplyRainMaterialValue(
                _material,
                ShaderType,
                NormalMap,
                ReliefTexture,
                DistortionStrength,
                OverlayColor,
                ReliefValue,
                Blur,
                BloomTexture,
                Bloom,
                Darkness
            );
        }
    }
}