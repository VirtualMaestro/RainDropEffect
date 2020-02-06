using System;
using System.Collections.Generic;
using UnityEngine;

namespace RainDropEffect2.Scripts.Common
{
    public class DropTrail : MonoBehaviour
    {
        private const string Name = "DropTrailMesh";

        // [SerializeField] 
        private List<Path> _paths = new List<Path>();

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
            _paths.RemoveAll(t => t.TimeElapsed >= lifeTime);

            var localPosition = transform.localPosition;
            var localRotation = transform.localRotation;

            switch (_paths.Count)
            {
                case 0:
                    _paths.Add(new Path(localPosition, localRotation));
                    // paths.Add(new Path(localPosition, localRotation));

                    _relativePos = localPosition;
                    break;
                case 1:
                    _paths.Add(new Path(localPosition, localRotation));
                    _relativePos = localPosition;
                    break;
            }

            // Add if needed
            var distSqr = (_paths[0].localPosition - localPosition).sqrMagnitude;
            if (distSqr < vertexDistance) return;

            var vec1 = _paths[0].localPosition - _paths[1].localPosition;
            var vec2 = transform.localPosition - _paths[0].localPosition;

            Quaternion qv1 = Quaternion.identity;
            Quaternion qv2 = Quaternion.identity;

            if (Math.Abs(vec1.magnitude) > 0.0001)
                qv1 = Quaternion.LookRotation(vec1, Vector3.forward);

            if (Math.Abs(vec2.magnitude) > 0.0001)
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
                        _paths.Insert(0, new Path(_paths[0].localPosition, q));
                    }
                }
            }

            _relativePos = vec2;
            _paths.Insert(0, new Path(transform.localPosition, qv2));
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
                
                _trail.transform.localPosition = p.localPosition;
                _trail.transform.localRotation = p.localRotation;

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

    [Serializable]
    internal class Path
    {
        public float timeCreated;
        public float TimeElapsed => Time.time - timeCreated;
        public Vector3 localPosition;
        public Quaternion localRotation;

        public Path(Vector3 localPosition, Quaternion localRotation)
        {
            this.localPosition = localPosition;
            this.localRotation = localRotation;
            timeCreated = Time.time;
        }
    }
}