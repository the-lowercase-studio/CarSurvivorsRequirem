using System;
using Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.Constants;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.Combat
{
    public class RailgunArcLaserBeam : MonoBehaviour
    {
        private const float MIN_MAX_RADIUS = 0.1f;
        private const float MIN_ARC_DEGREES = 1f;
        private const float MAX_ARC_DEGREES = 360f;
        private const float MIN_BAND_RADIAL_THICKNESS = 0.05f;
        private const int ROWS_PER_FRAME = 4;

        [SerializeField] private MeshFilter _meshFilter;
        [SerializeField] private MeshRenderer _meshRenderer;
        [SerializeField] private int _arcSegments = RailgunTowerConstants.ARC_LASER_SEGMENTS;
        [SerializeField] private float _muzzleHalfWidth = RailgunTowerConstants.ARC_LASER_MUZZLE_HALF_WIDTH;
        [SerializeField] private float _groundBandHalfDepth = RailgunTowerConstants.ARC_LASER_BAND_VISUAL_HALF_DEPTH;

        private Mesh _mesh;
        private Vector3[] _vertices;
        private Vector3[] _normals;
        private Vector2[] _uvs;
        private int[] _triangles;
        private int _allocatedSegments = -1;

        private void Awake()
        {
            ValidateComponents();
        }

        private void OnDestroy()
        {
            if (_mesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_mesh);
            }
            else
            {
                DestroyImmediate(_mesh);
            }

            _mesh = null;
        }

        public void ShowArcLaser(
            Vector3 groundOrigin,
            Vector3 centerDirection,
            Vector3 muzzleWorldPos,
            float currentRadius,
            float maxRadius,
            float arcDegrees)
        {
            ValidateComponents();

            Vector3 flatDirection = new Vector3(centerDirection.x, 0f, centerDirection.z);
            if (flatDirection.sqrMagnitude < 0.001f)
            {
                flatDirection = Vector3.forward;
            }
            flatDirection.Normalize();

            transform.SetPositionAndRotation(
                groundOrigin,
                Quaternion.LookRotation(flatDirection, Vector3.up)
            );

            // Compensate authored parent/model scaling so arc dimensions remain exact world meters.
            transform.localScale = Vector3.one;
            Vector3 worldScale = transform.lossyScale;
            if (worldScale.x <= 0f || worldScale.y <= 0f || worldScale.z <= 0f)
            {
                throw new InvalidOperationException($"[{nameof(RailgunArcLaserBeam)}] requires positive parent scaling.");
            }
            transform.localScale = new Vector3(1f / worldScale.x, 1f / worldScale.y, 1f / worldScale.z);

            int segments = Mathf.Max(RailgunTowerConstants.MIN_ARC_LASER_SEGMENTS, _arcSegments);
            EnsureMeshBuffers(segments);

            Vector3 localMuzzle = transform.InverseTransformPoint(muzzleWorldPos);
            float clampedMaxRadius = Mathf.Max(MIN_MAX_RADIUS, maxRadius);
            float visualRadius = Mathf.Clamp(currentRadius, RailgunTowerConstants.ARC_LASER_MIN_RADIUS, clampedMaxRadius);
            float halfRad = Mathf.Clamp(arcDegrees, MIN_ARC_DEGREES, MAX_ARC_DEGREES) * 0.5f * Mathf.Deg2Rad;
            float groundY = RailgunTowerConstants.BEAM_GROUND_Y_OFFSET;

            float bandOuter = Mathf.Clamp(
                currentRadius + _groundBandHalfDepth,
                RailgunTowerConstants.ARC_LASER_MIN_RADIUS,
                clampedMaxRadius
            );
            float bandInner = Mathf.Clamp(
                currentRadius - _groundBandHalfDepth,
                0f,
                Mathf.Max(0f, bandOuter - MIN_BAND_RADIAL_THICKNESS)
            );

            int rowStride = segments + 1;
            int fanBottomOffset = rowStride;
            int bandInnerOffset = rowStride * 2;
            int bandOuterOffset = rowStride * 3;

            Vector3 curtainNormal = (Vector3.up + Vector3.back).normalized;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = Mathf.Lerp(-halfRad, halfRad, t);
                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);
                float muzzleOffsetX = Mathf.Lerp(-_muzzleHalfWidth, _muzzleHalfWidth, t);

                _vertices[i] = new Vector3(localMuzzle.x + muzzleOffsetX, localMuzzle.y, localMuzzle.z);
                _normals[i] = curtainNormal;

                _vertices[fanBottomOffset + i] = new Vector3(sin * visualRadius, groundY, cos * visualRadius);
                _normals[fanBottomOffset + i] = curtainNormal;

                _vertices[bandInnerOffset + i] = new Vector3(sin * bandInner, groundY, cos * bandInner);
                _normals[bandInnerOffset + i] = Vector3.up;

                _vertices[bandOuterOffset + i] = new Vector3(sin * bandOuter, groundY, cos * bandOuter);
                _normals[bandOuterOffset + i] = Vector3.up;
            }

            _mesh.vertices = _vertices;
            _mesh.normals = _normals;
            _mesh.RecalculateBounds();

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (!_meshRenderer.enabled)
            {
                _meshRenderer.enabled = true;
            }
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void ValidateComponents()
        {
            if (_meshFilter == null || _meshRenderer == null)
            {
                throw new InvalidOperationException($"[{nameof(RailgunArcLaserBeam)}] requires MeshFilter and MeshRenderer references.");
            }
        }

        private void EnsureMeshBuffers(int segments)
        {
            if (_mesh != null && _allocatedSegments == segments)
            {
                return;
            }

            _allocatedSegments = segments;

            if (_mesh == null)
            {
                _mesh = new Mesh
                {
                    name = "RailgunArcLaserMesh"
                };
                _mesh.MarkDynamic();
            }
            else
            {
                _mesh.Clear();
            }

            int rowStride = segments + 1;
            int vertexCount = rowStride * ROWS_PER_FRAME;
            int triangleIndexCount = segments * 12;

            _vertices = new Vector3[vertexCount];
            _normals = new Vector3[vertexCount];
            _uvs = new Vector2[vertexCount];
            _triangles = new int[triangleIndexCount];

            int fanBottomOffset = rowStride;
            int bandInnerOffset = rowStride * 2;
            int bandOuterOffset = rowStride * 3;

            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments;
                _uvs[i] = new Vector2(u, 0f);
                _uvs[fanBottomOffset + i] = new Vector2(u, 1f);
                _uvs[bandInnerOffset + i] = new Vector2(u, 0f);
                _uvs[bandOuterOffset + i] = new Vector2(u, 1f);
            }

            int triCursor = 0;
            for (int i = 0; i < segments; i++)
            {
                int top0 = i;
                int top1 = i + 1;
                int bot0 = fanBottomOffset + i;
                int bot1 = fanBottomOffset + i + 1;

                _triangles[triCursor++] = top0;
                _triangles[triCursor++] = top1;
                _triangles[triCursor++] = bot0;

                _triangles[triCursor++] = top1;
                _triangles[triCursor++] = bot1;
                _triangles[triCursor++] = bot0;

                int in0 = bandInnerOffset + i;
                int in1 = bandInnerOffset + i + 1;
                int out0 = bandOuterOffset + i;
                int out1 = bandOuterOffset + i + 1;

                _triangles[triCursor++] = in0;
                _triangles[triCursor++] = out0;
                _triangles[triCursor++] = out1;

                _triangles[triCursor++] = in0;
                _triangles[triCursor++] = out1;
                _triangles[triCursor++] = in1;
            }

            _mesh.vertices = _vertices;
            _mesh.normals = _normals;
            _mesh.uv = _uvs;
            _mesh.triangles = _triangles;

            _meshFilter.sharedMesh = _mesh;
        }
    }
}
