using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RainDropEffect2.Scripts.Common
{
    public class DropTrail : MonoBehaviour
    {
        private const string Name = "[Hidden]DropTrailMesh";

        public new bool enabled = true;
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

        [SerializeField] private List<Path> paths = new List<Path>();

        // Use this for initialization
        private void Awake()
        {
            CheckExistence();
        }

        // Update is called once per frame
        private void Update()
        {
            if (!CheckExistence() || !CheckActive()) return;

            UpdateTrail();
            UpdateMesh();
        }

        public void Clear()
        {
            paths.Clear();
        }

        private bool CheckExistence()
        {
            if (!_trail)
            {
                Transform oldTrail = transform.Find(Name);

                if (oldTrail)
                {
                    _trail = oldTrail.gameObject;
                    _meshFilter = _trail.GetComponent<MeshFilter>();
                    _meshRenderer = _trail.GetComponent<MeshRenderer>();
                }
                else
                {
                    _trail = RainDropTools.CreateHiddenObject(Name, this.transform).gameObject;
                }
            }

            if (!_meshFilter)
            {
                _meshFilter = _trail.AddComponent<MeshFilter>();
            }

            if (!_meshRenderer)
            {
                _meshRenderer = _trail.AddComponent<MeshRenderer>();
            }

            if (material == null) return false;

            _meshRenderer.material = material;

            return true;
        }

        bool CheckActive()
        {
            _meshRenderer.enabled = enabled;
            return enabled;
        }

        private void UpdateTrail()
        {
            // Remove all expires
            paths.RemoveAll(t => t.TimeElapsed >= lifeTime);

            var localPosition = transform.localPosition;
            var localRotation = transform.localRotation;

            switch (paths.Count)
            {
                case 0:
                    paths.Add(new Path(localPosition, localRotation));
                    paths.Add(new Path(localPosition, localRotation));

                    _relativePos = localPosition;
                    break;
                case 1:
                    paths.Add(new Path(localPosition, localRotation));
                    _relativePos = localPosition;
                    break;
            }

            // Add if needed
            var distSqr = (paths[0].localPosition - localPosition).sqrMagnitude;
            if (distSqr < vertexDistance) return;

            Vector3 vec1 = paths[0].localPosition - paths[1].localPosition;
            Vector3 vec2 = transform.localPosition - paths[0].localPosition;

            Quaternion qv1 = Quaternion.identity;
            Quaternion qv2 = Quaternion.identity;

            if (Math.Abs(vec1.magnitude) > 0.0001)
                qv1 = Quaternion.LookRotation(vec1, Vector3.forward);

            if (Math.Abs(vec2.magnitude) > 0.0001)
                qv2 = Quaternion.LookRotation(vec2, Vector3.forward);

            qv1.eulerAngles += Vector3.forward * -90f;
            qv2.eulerAngles += Vector3.forward * -90f;

            if (paths.Count >= 2)
            {
                //Get the dot product
                float dot = Vector3.Dot(vec1, vec2);
                dot = dot / (vec1.magnitude * vec2.magnitude);
                float acos = Mathf.Acos(dot);
                float angle = acos * 180f / Mathf.PI;

                if (!float.IsNaN(angle))
                {
                    int angleResol = (int) angle / angleDivisions;
                    for (int j = 0; j < angleResol; j++)
                    {
                        Quaternion q = Quaternion.Slerp(qv1, qv2, j / (float) angleResol);
                        paths.Insert(0, new Path(paths[0].localPosition, q));
                    }
                }
            }

            _relativePos = vec2;
            paths.Insert(0, new Path(transform.localPosition, qv2));
        }

        void UpdateMesh()
        {
            if (paths.Count <= 1)
            {
                _meshRenderer.enabled = false;
                return;
            }

            _meshRenderer.enabled = true;

            Vector3[] verts = new Vector3[paths.Count * 2];
            Vector2[] uvs = new Vector2[paths.Count * 2];
            int[] tris = new int[(paths.Count - 1) * 6];

            for (int i = 0; i < paths.Count; i++)
            {
                float progress = i / (float) paths.Count();
                Path p = paths[i];
                _trail.transform.parent = transform.parent;
                _trail.transform.localPosition = p.localPosition;
                _trail.transform.localRotation = p.localRotation;

                float w = Mathf.Max(widthMultiplier * widthCurve.Evaluate(progress) * 0.5f, 0.001f);
                verts[i * 2] = _trail.transform.TransformPoint(0, w, 0);
                verts[(i * 2) + 1] = _trail.transform.TransformPoint(0, -w, 0);

                float uvRatio = progress;
                if (textureMode == LineTextureMode.Tile)
                {
                    uvRatio = i;
                }

                uvs[i * 2] = new Vector2(uvRatio, 0f);
                uvs[(i * 2) + 1] = new Vector2(uvRatio, 1f);

                if (i != 0)
                {
                    tris[((i - 1) * 6) + 0] = (i * 2) - 2;
                    tris[((i - 1) * 6) + 1] = (i * 2) - 1;
                    tris[((i - 1) * 6) + 2] = (i * 2) - 0;
                    tris[((i - 1) * 6) + 3] = (i * 2) + 1;
                    tris[((i - 1) * 6) + 4] = (i * 2) + 0;
                    tris[((i - 1) * 6) + 5] = (i * 2) - 1;
                }

                _trail.transform.parent = null;
            }

            Mesh.Clear();
            Mesh.vertices = verts;
            Mesh.uv = uvs;
            Mesh.triangles = tris;

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