﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace RainDropEffect2.Scripts.Common
{
    public sealed class DropTrail : MonoBehaviour
    {
        private const string Name = "DropTrailMesh";

        // [SerializeField] 
        private readonly List<Path> _paths = new List<Path>();

        // public new bool enabled = true;
        public Material material;
        public float lifeTime = 3f;
        public AnimationCurve widthCurve;
        public float widthMultiplier = .5f;
        public int angleDivisions = 10;
        public float vertexDistance = .5f;
        public LineTextureMode textureMode;

        private GameObject _trail;
        private Vector3 _relativePos;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;

        private Mesh Mesh
        {
            get => _meshFilter.mesh;
            set => _meshFilter.mesh = value;
        }

        private void Awake()
        {
            _trail = RainDropTools.CreateHolder(Name, transform).gameObject;
            _meshFilter = _trail.AddComponent<MeshFilter>();
            _meshRenderer = _trail.AddComponent<MeshRenderer>();
        }

        private void Update()
        {
            if (!CheckExistence()) return;

            _UpdateTrail();
            _UpdateMesh();
        }

        public void Clear()
        {
            _paths.Clear();
        }

        private bool CheckExistence()
        {
            if (ReferenceEquals(material, null)) return false;

            _meshRenderer.material = material;

            return true;
        }

        private void _UpdateTrail()
        {
            // Remove all expires
            _paths.RemoveAll(p =>
            {
                if (Time.time - p.TimeCreated >= lifeTime)
                {
                    p.Dispose();
                    return true;
                }
            
                return false;
            });

            var trans = transform;
            var localPosition = trans.localPosition;
            var localRotation = trans.localRotation;
            
            switch (_paths.Count)
            {
                case 0:
                    _paths.Add(Path.Create(ref localPosition, ref localRotation));
                    _relativePos = localPosition;
                    break;
                case 1:
                    _paths.Add(Path.Create(ref localPosition, ref localRotation));
                    _relativePos = localPosition;
                    break;
            }

            // Add if needed
            var path0LocalPosition = _paths[0].LocalPosition;
            var distSqr = (path0LocalPosition - localPosition).sqrMagnitude;
            if (distSqr < vertexDistance) return;

            var vec1 = path0LocalPosition - _paths[1].LocalPosition;
            var vec2 = trans.localPosition - path0LocalPosition;

            Quaternion qv1 = Quaternion.identity;
            Quaternion qv2 = Quaternion.identity;

            if (Math.Abs(vec1.sqrMagnitude) > 0.0001)
                qv1 = Quaternion.LookRotation(vec1, Vector3.forward);

            if (Math.Abs(vec2.sqrMagnitude) > 0.0001)
                qv2 = Quaternion.LookRotation(vec2, Vector3.forward);

            qv1.eulerAngles += Vector3.forward * -90f;
            qv2.eulerAngles += Vector3.forward * -90f;

            if (_paths.Count >= 2)
            {
                //Get the dot product
                var dot = Vector3.Dot(vec1, vec2);
                dot = dot / (vec1.magnitude * vec2.magnitude);
                var angle = Mathf.Acos(dot) * Mathf.Rad2Deg;

                if (!float.IsNaN(angle))
                {
                    var angleResol = (int) angle / angleDivisions;
                    for (var j = 0; j < angleResol; j++)
                    {
                        var q = Quaternion.Slerp(qv1, qv2, j / (float) angleResol);
                        
                        _paths.Insert(0, Path.Create(ref _paths[0].LocalPosition, ref q));
                    }
                }
            }

            _relativePos = vec2;
           
            var transformLocalPosition = transform.localPosition;
            _paths.Insert(0, Path.Create(ref transformLocalPosition, ref qv2));
        }

        private void _UpdateMesh()
        {
            if (_paths.Count <= 1)
            {
                _meshRenderer.enabled = false;
                return;
            }

            _meshRenderer.enabled = true;

            var verts = new Vector3[_paths.Count * 2];
            var uvs = new Vector2[_paths.Count * 2];
            var tris = new int[(_paths.Count - 1) * 6];

            _trail.transform.parent = transform.parent;
            
            for (var i = 0; i < _paths.Count; i++)
            {
                var progress = i / (float) _paths.Count;
                var p = _paths[i];
                
                _trail.transform.localPosition = p.LocalPosition;
                _trail.transform.localRotation = p.LocalRotation;

                var w = Mathf.Max(widthMultiplier * widthCurve.Evaluate(progress) * 0.5f, 0.001f);
                verts[i * 2] = _trail.transform.TransformPoint(0, w, 0);
                verts[(i * 2) + 1] = _trail.transform.TransformPoint(0, -w, 0);

                var uvRatio = progress;
                
                if (textureMode == LineTextureMode.Tile)
                {
                    uvRatio = i;
                }

                uvs[i * 2] = new Vector2(uvRatio, 0f);
                uvs[(i * 2) + 1] = new Vector2(uvRatio, 1f);

                if (i == 0) continue;
                
                tris[((i - 1) * 6) + 0] = (i * 2) - 2;
                tris[((i - 1) * 6) + 1] = (i * 2) - 1;
                tris[((i - 1) * 6) + 2] = (i * 2) - 0;
                tris[((i - 1) * 6) + 3] = (i * 2) + 1;
                tris[((i - 1) * 6) + 4] = (i * 2) + 0;
                tris[((i - 1) * 6) + 5] = (i * 2) - 1;
            }

            Mesh.Clear();
            Mesh.vertices = verts;
            Mesh.uv = uvs;
            Mesh.triangles = tris;

            _trail.transform.parent = null;
            _trail.transform.localPosition = Vector3.zero;
            _trail.transform.localRotation = Quaternion.identity;
            _trail.transform.localScale = Vector3.one;
            
            _trail.transform.parent = transform;
        }

        void OnDrawGizmos()
        {
            if (_relativePos == Vector3.zero) return;
            Vector3 fwd1 = transform.TransformPoint(0f, 0f, 0f);
            Vector3 fwd2 = transform.TransformPoint(_relativePos);
            Vector3 fwd = fwd2 - fwd1;

            var position = transform.position;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(position, position + fwd.normalized * 2f);
        }
    }

    // [Serializable]
    public sealed class Path
    {
        private static StackPool<Path> _pool;

        public static Path Create(ref Vector3 position, ref Quaternion rotation)
        {
            if (_pool == null)
                _pool = new StackPool<Path>(50, () => new Path());
            
            var path = _pool.Get();
            path.SetTo(ref position, ref rotation);
            return path;
        }

        public static void DisposePool()
        {
            _pool.Dispose();
            _pool = null;
        }

        public static string StatPool => _pool.ToString();
        
        public float TimeCreated;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;

        private Path()
        { }

        public void Dispose()
        {
            _pool.Put(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetTo(ref Vector3 position, ref Quaternion rotation)
        {
            LocalPosition = position;
            LocalRotation = rotation;
            TimeCreated = Time.time;
        }
    }
}