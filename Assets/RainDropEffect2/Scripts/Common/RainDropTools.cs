//#define SHOW_HIDED

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RainDropEffect2.Scripts.Common
{
    public static class RainDropTools
    {
        public enum RainDropShaderType
        {
            Expensive,
            Cheap,
            NoDistortion
        }
        private const float Tolerance = 0.0001f;

        private const string ShaderForward = "RainDrop/Internal/RainDistortion (Forward)";
        private const string ShaderCheap = "RainDrop/Internal/RainDistortion (Mobile)";
        private const string ShaderNoDistortion = "RainDrop/Internal/RainNoDistortion";

        // private static string SHADER_DEFFERED = "RainDrop/Internal/RainDistortion (Deffered)";

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int StrengthId = Shader.PropertyToID("_Strength");
        private static readonly int ReliefId = Shader.PropertyToID("_Relief");
        private static readonly int BlurId = Shader.PropertyToID("_Blur");
        private static readonly int BloomId = Shader.PropertyToID("_Bloom");
        private static readonly int DarknessId = Shader.PropertyToID("_Darkness");
        private static readonly int DistortionId = Shader.PropertyToID("_Distortion");
        private static readonly int ReliefTexId = Shader.PropertyToID("_ReliefTex");
        private static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        
        private static readonly Vector3[] Vertices =
        {
            new Vector3(1, 1, 0),
            new Vector3(1, -1, 0),
            new Vector3(-1, 1, 0),
            new Vector3(-1, -1, 0),
        };
        
        private static readonly Vector2[] Uvs =
        {
            new Vector2(1, 1),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(0, 0),
        };

        private static readonly int[] Triangles =
        {
            0, 1, 2,
            2, 1, 3
        };

        public static string GetShaderName(RainDropShaderType shaderType)
        {
            switch (shaderType)
            {
                case RainDropShaderType.Expensive:
                    return ShaderForward;
                case RainDropShaderType.Cheap:
                    return ShaderCheap;
                case RainDropShaderType.NoDistortion:
                    return ShaderNoDistortion;
                default:
                    return "";
            }
        }

        public static Material CreateRainMaterial(RainDropShaderType shaderType, int renderQueue)
        {
            var shader = Shader.Find(GetShaderName(shaderType));
            return new Material(shader) {renderQueue = renderQueue};
        }

        public static void ApplyRainMaterialValue(
            Material material,
            RainDropShaderType shaderType,
            Texture normalMap,
            Texture overlayTexture = null,
            float distortionValue = 0f,
            Color? overlayColor = null,
            float reliefValue = 0f,
            float blur = 0f,
            Texture bloomTexture = null,
            float bloom = 0f,
            float darkness = 0f
        )
        {
            // Apply shader values
            switch (shaderType)
            {
                case RainDropShaderType.Expensive:
                    material.SetColor(ColorId, overlayColor ?? Color.white);
                    material.SetFloat(StrengthId, distortionValue);
                    material.SetFloat(ReliefId, reliefValue);
                    
                    if (Math.Abs(blur) > Tolerance) material.EnableKeyword("BLUR");
                    else material.DisableKeyword("BLUR");

                    material.SetFloat(BlurId, blur);
                    material.SetFloat(BloomId, bloom);
                    material.SetFloat(DarknessId, darkness);
                    material.SetTexture(DistortionId, normalMap);
                    material.SetTexture(ReliefTexId, overlayTexture);
                    material.SetTexture(BloomTexId, bloomTexture);
                    break;
                
                case RainDropShaderType.Cheap:
                    material.SetFloat(StrengthId, distortionValue);
                    material.SetTexture(DistortionId, normalMap);
                    break;
                
                case RainDropShaderType.NoDistortion:
                    material.SetTexture(MainTexId, overlayTexture);
                    material.SetTexture(DistortionId, normalMap);
                    material.SetColor(ColorId, overlayColor ?? Color.white);
                    material.SetFloat(DarknessId, darkness);
                    material.SetFloat(ReliefId, reliefValue);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(shaderType), shaderType, null);
            }
        }

        public static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh
            {
                hideFlags = HideFlags.DontSave,
                name = "Rain Mesh",
                vertices = Vertices,
                uv = Uvs,
                triangles = Triangles
            };

            mesh.Optimize();
            mesh.MarkDynamic();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Transform CreateHolder(string name, Transform parent)
        {
            var gameObject = new GameObject {name = name};
            var transform = gameObject.transform;
            transform.parent = parent;
            transform.localScale = Vector3.one;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.Euler(Vector3.zero);
            return transform;
        }

        public static float Random(float min, float max)
        {
            return UnityEngine.Random.Range(min, max);
        }

        public static int Random(int min, int max)
        {
            return UnityEngine.Random.Range(min, max);
        }

        public static void DestroyChildren(Transform t)
        {
            foreach (Transform child in t)
            {
                Object.Destroy(child.gameObject);
            }
        }

        public static void DestroyChildrenImmediate(Transform t)
        {
            foreach (Transform child in t)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        public static Vector2 GetCameraOrthographicSize(UnityEngine.Camera cam)
        {
            var h = cam.orthographicSize * 2f;
            var w = h * cam.aspect;
            return new Vector2(w, h);
        }

        public static void CalculateCameraOrthographicSize(UnityEngine.Camera cam, out float w, out float h)
        {
            h = cam.orthographicSize * 2f;
            w = h * cam.aspect;
        }

        /// <summary>
        /// Get Spawn Position by camera and offset position
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="cam"></param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        /// <returns></returns>
        public static Vector3 GetSpawnLocalPos(Transform parent, UnityEngine.Camera cam, float offsetX, float offsetY)
        {
            var camSize = GetCameraOrthographicSize(cam);
            var p = new Vector3(
                Random(-camSize.x / 2f, camSize.x / 2f),
                Random(-camSize.y / 2f, camSize.y / 2f),
                0f
            );
            
            p = cam.transform.rotation * p + parent.position;
            p.x += camSize.x * offsetX;
            p.y += camSize.y * offsetY;
            
            return parent.InverseTransformPoint(p);
        }

        /// <summary>
        /// Get the g-forced screen movement
        /// That is to say, we gets the rotation vector that applies gravity
        /// </summary>
        public static Vector3 GetGForcedScreenMovement(Transform screenTransform, Vector3 gForce)
        {
            var projY = Vector3.Project(gForce, screenTransform.up);
            var projX = Vector3.Project(gForce, screenTransform.right);
            var projZ = Vector3.Project(gForce, screenTransform.forward);

            var position = screenTransform.position;
            var relativePointY = screenTransform.InverseTransformPoint(position + projY);
            var relativePointX = screenTransform.InverseTransformPoint(position + projX);
            var relativePointZ = screenTransform.InverseTransformPoint(position + projZ);

            return new Vector3(relativePointX.x, relativePointY.y, relativePointZ.z);
        }

        /// <summary>
        /// Get an element from a weighted KeyValuePair list
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="list"></param>
        /// <returns></returns>
        public static KeyValuePair<T1, T2> GetWeightedElement<T1, T2>(List<KeyValuePair<T1, T2>> list)
            where T2 : IComparable
        {
            if (list.Count == 0)
            {
                return list.FirstOrDefault();
            }

            var totalWeight = (float) list.Sum(t => Convert.ToDouble(t.Value));
            var choice = Random(0f, totalWeight);
            float sum = 0;

            foreach (var obj in list)
            {
                for (var i = sum; i < Convert.ToDouble(obj.Value) + sum; i++)
                {
                    if (i >= choice)
                    {
                        return obj;
                    }
                }

                sum += (float) Convert.ToDouble(obj.Value);
            }

            return list.First();
        }
    }
}