using System;
using Assets.Scripts.Indicators.Constants;
using Assets.Scripts.PowerUps.Constants;
using DG.Tweening;
using UnityEngine;

namespace Assets.Scripts.Indicators
{
    public class ArcTelegraphIndicator : MonoBehaviour, ITelegraphIndicator
    {
        private const float MIN_ANIMATED_FILL_DURATION = 0.01f;
        private const float MIN_ARC_DEGREES = 1f;
        private const float MAX_ARC_DEGREES = 360f;
        private const float MIN_RADIUS = 0.1f;
        private const float MIN_BORDER_WIDTH = 0.02f;

        [SerializeField] private Transform _outerBorder;
        [SerializeField] private Transform _innerFill;
        [SerializeField] private MeshFilter _outerBorderMeshFilter;
        [SerializeField] private MeshFilter _innerFillMeshFilter;
        [SerializeField] private float _borderWidth = IndicatorConstants.DEFAULT_ARC_BORDER_WIDTH;
        [SerializeField] private int _arcSegments = IndicatorConstants.DEFAULT_ARC_SEGMENTS;
        [SerializeField] private float _contractDuration = 0.12f;

        private Sequence _activeSequence;
        private Mesh _borderMesh;
        private Mesh _fillMesh;
        private float _cachedRadius = -1f;
        private float _cachedArcDegrees = -1f;
        private float _cachedBorderWidth = -1f;
        private int _cachedSegments = -1;
        private bool _isFreezable;

        private void OnDisable()
        {
            KillActiveSequence();
        }

        private void OnDestroy()
        {
            KillActiveSequence();
            DestroyRuntimeMesh(ref _borderMesh);
            DestroyRuntimeMesh(ref _fillMesh);
        }

        public void SetFreezable(bool isFreezable)
        {
            _isFreezable = isFreezable;
            if (_activeSequence != null && _activeSequence.IsActive() && isFreezable)
            {
                _activeSequence.SetId(TimeFreezeConstants.FREEZABLE_TWEEN_ID);
            }
        }

        public void ShowArcWarning(Vector3 origin, Vector3 centerDirection, float radius, float arcDegrees, float fillDuration)
        {
            ValidateMeshReferences();
            KillActiveSequence();

            Vector3 flatDirection = new Vector3(centerDirection.x, 0f, centerDirection.z);
            if (flatDirection.sqrMagnitude < 0.001f)
            {
                flatDirection = Vector3.forward;
            }
            flatDirection.Normalize();

            transform.SetPositionAndRotation(
                origin + Vector3.up * IndicatorConstants.GROUND_Y_OFFSET,
                Quaternion.LookRotation(flatDirection, Vector3.up)
            );

            // Compensate authored parent/model scaling so arc dimensions remain world meters.
            transform.localScale = Vector3.one;
            Vector3 worldScale = transform.lossyScale;
            if (worldScale.x <= 0f || worldScale.y <= 0f || worldScale.z <= 0f)
            {
                throw new InvalidOperationException("Arc telegraph indicator requires positive parent scaling.");
            }
            transform.localScale = new Vector3(1f / worldScale.x, 1f / worldScale.y, 1f / worldScale.z);

            float clampedRadius = Mathf.Max(MIN_RADIUS, radius);
            float clampedArcDegrees = Mathf.Clamp(arcDegrees, MIN_ARC_DEGREES, MAX_ARC_DEGREES);
            float clampedBorderWidth = Mathf.Clamp(_borderWidth, MIN_BORDER_WIDTH, clampedRadius * 0.45f);
            int clampedSegments = Mathf.Max(IndicatorConstants.MIN_ARC_SEGMENTS, _arcSegments);

            EnsureMeshesBuilt(clampedRadius, clampedArcDegrees, clampedBorderWidth, clampedSegments);

            _outerBorder.localPosition = new Vector3(0f, IndicatorConstants.FILL_Y_OFFSET, 0f);
            _outerBorder.localRotation = Quaternion.identity;
            _outerBorder.localScale = Vector3.one;

            _innerFill.localPosition = Vector3.zero;
            _innerFill.localRotation = Quaternion.identity;

            gameObject.SetActive(true);

            if (fillDuration <= MIN_ANIMATED_FILL_DURATION)
            {
                _innerFill.localScale = Vector3.one;
                return;
            }

            _innerFill.localScale = Vector3.zero;
            _activeSequence = CreateSequence();
            _activeSequence.Append(_innerFill.DOScale(Vector3.one, fillDuration).SetEase(Ease.Linear));
        }

        public void HidePersistent()
        {
            KillActiveSequence();
            gameObject.SetActive(false);
        }

        public void ContractAndDismiss()
        {
            KillActiveSequence();

            if (!gameObject.activeInHierarchy)
            {
                Dismiss();
                return;
            }

            _activeSequence = CreateSequence();
            if (_outerBorder != null)
            {
                _activeSequence.Join(_outerBorder.DOScale(Vector3.zero, _contractDuration).SetEase(Ease.InQuad));
            }

            if (_innerFill != null)
            {
                _activeSequence.Join(_innerFill.DOScale(Vector3.zero, _contractDuration).SetEase(Ease.InQuad));
            }

            _activeSequence.OnComplete(() =>
            {
                if (this != null && gameObject != null)
                {
                    Destroy(gameObject);
                }
            });
        }

        public void Dismiss()
        {
            KillActiveSequence();
            if (this != null && gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        private void ValidateMeshReferences()
        {
            if (_outerBorder == null || _innerFill == null || _outerBorderMeshFilter == null || _innerFillMeshFilter == null)
            {
                throw new InvalidOperationException("Arc telegraph indicator requires outer border and inner fill transform and MeshFilter references.");
            }
        }

        private void EnsureMeshesBuilt(float radius, float arcDegrees, float borderWidth, int segments)
        {
            bool isDirty = _fillMesh == null || _borderMesh == null
                || !Mathf.Approximately(_cachedRadius, radius)
                || !Mathf.Approximately(_cachedArcDegrees, arcDegrees)
                || !Mathf.Approximately(_cachedBorderWidth, borderWidth)
                || _cachedSegments != segments;

            if (!isDirty)
            {
                return;
            }

            _cachedRadius = radius;
            _cachedArcDegrees = arcDegrees;
            _cachedBorderWidth = borderWidth;
            _cachedSegments = segments;

            BuildFillMesh(radius, arcDegrees, segments);
            BuildBorderMesh(radius, arcDegrees, borderWidth, segments);
        }

        private void BuildFillMesh(float radius, float arcDegrees, int segments)
        {
            if (_fillMesh == null)
            {
                _fillMesh = new Mesh
                {
                    name = "ArcTelegraphFillMesh"
                };
            }
            else
            {
                _fillMesh.Clear();
            }

            int vertexCount = segments + 2;
            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            normals[0] = Vector3.up;
            uvs[0] = new Vector2(0.5f, 0f);

            float halfRad = arcDegrees * 0.5f * Mathf.Deg2Rad;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = Mathf.Lerp(-halfRad, halfRad, t);
                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);

                int vertIndex = i + 1;
                vertices[vertIndex] = new Vector3(sin * radius, 0f, cos * radius);
                normals[vertIndex] = Vector3.up;
                uvs[vertIndex] = new Vector2(t, 1f);
            }

            for (int i = 0; i < segments; i++)
            {
                int triIndex = i * 3;
                triangles[triIndex] = 0;
                triangles[triIndex + 1] = i + 1;
                triangles[triIndex + 2] = i + 2;
            }

            _fillMesh.vertices = vertices;
            _fillMesh.normals = normals;
            _fillMesh.uv = uvs;
            _fillMesh.triangles = triangles;
            _fillMesh.RecalculateBounds();

            _innerFillMeshFilter.sharedMesh = _fillMesh;
        }

        private void BuildBorderMesh(float radius, float arcDegrees, float borderWidth, int segments)
        {
            if (_borderMesh == null)
            {
                _borderMesh = new Mesh
                {
                    name = "ArcTelegraphBorderMesh"
                };
            }
            else
            {
                _borderMesh.Clear();
            }

            // Outer arc ribbon + left radial edge + right radial edge.
            int arcVertexCount = (segments + 1) * 2;
            int totalVertexCount = arcVertexCount + 8;
            int arcTriangleCount = segments * 6;
            int totalTriangleCount = arcTriangleCount + 12;

            Vector3[] vertices = new Vector3[totalVertexCount];
            Vector3[] normals = new Vector3[totalVertexCount];
            int[] triangles = new int[totalTriangleCount];

            for (int i = 0; i < totalVertexCount; i++)
            {
                normals[i] = Vector3.up;
            }

            float halfRad = arcDegrees * 0.5f * Mathf.Deg2Rad;
            float innerRadius = Mathf.Max(0.02f, radius - borderWidth);

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = Mathf.Lerp(-halfRad, halfRad, t);
                Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

                int baseVert = i * 2;
                vertices[baseVert] = dir * innerRadius;
                vertices[baseVert + 1] = dir * radius;
            }

            for (int i = 0; i < segments; i++)
            {
                int baseVert = i * 2;
                int triIndex = i * 6;

                triangles[triIndex] = baseVert;
                triangles[triIndex + 1] = baseVert + 1;
                triangles[triIndex + 2] = baseVert + 3;

                triangles[triIndex + 3] = baseVert;
                triangles[triIndex + 4] = baseVert + 3;
                triangles[triIndex + 5] = baseVert + 2;
            }

            // Left radial edge strip from apex to outer radius.
            Vector3 leftDir = new Vector3(Mathf.Sin(-halfRad), 0f, Mathf.Cos(-halfRad));
            Vector3 leftInward = new Vector3(leftDir.z, 0f, -leftDir.x);
            int leftVertOffset = arcVertexCount;
            vertices[leftVertOffset] = Vector3.zero;
            vertices[leftVertOffset + 1] = leftDir * radius;
            vertices[leftVertOffset + 2] = leftDir * radius + leftInward * borderWidth;
            vertices[leftVertOffset + 3] = leftInward * borderWidth;

            int leftTriOffset = arcTriangleCount;
            triangles[leftTriOffset] = leftVertOffset;
            triangles[leftTriOffset + 1] = leftVertOffset + 1;
            triangles[leftTriOffset + 2] = leftVertOffset + 2;
            triangles[leftTriOffset + 3] = leftVertOffset;
            triangles[leftTriOffset + 4] = leftVertOffset + 2;
            triangles[leftTriOffset + 5] = leftVertOffset + 3;

            // Right radial edge strip from apex to outer radius.
            Vector3 rightDir = new Vector3(Mathf.Sin(halfRad), 0f, Mathf.Cos(halfRad));
            Vector3 rightInward = new Vector3(-rightDir.z, 0f, rightDir.x);
            int rightVertOffset = arcVertexCount + 4;
            vertices[rightVertOffset] = rightInward * borderWidth;
            vertices[rightVertOffset + 1] = rightDir * radius + rightInward * borderWidth;
            vertices[rightVertOffset + 2] = rightDir * radius;
            vertices[rightVertOffset + 3] = Vector3.zero;

            int rightTriOffset = arcTriangleCount + 6;
            triangles[rightTriOffset] = rightVertOffset;
            triangles[rightTriOffset + 1] = rightVertOffset + 1;
            triangles[rightTriOffset + 2] = rightVertOffset + 2;
            triangles[rightTriOffset + 3] = rightVertOffset;
            triangles[rightTriOffset + 4] = rightVertOffset + 2;
            triangles[rightTriOffset + 5] = rightVertOffset + 3;

            _borderMesh.vertices = vertices;
            _borderMesh.normals = normals;
            _borderMesh.triangles = triangles;
            _borderMesh.RecalculateBounds();

            _outerBorderMeshFilter.sharedMesh = _borderMesh;
        }

        private Sequence CreateSequence()
        {
            Sequence sequence = DOTween.Sequence();
            if (_isFreezable)
            {
                sequence.SetId(TimeFreezeConstants.FREEZABLE_TWEEN_ID);
            }
            return sequence;
        }

        private void KillActiveSequence()
        {
            if (_activeSequence != null && _activeSequence.IsActive())
            {
                _activeSequence.Kill();
            }
            _activeSequence = null;
        }

        private void DestroyRuntimeMesh(ref Mesh mesh)
        {
            if (mesh != null)
            {
                Destroy(mesh);
                mesh = null;
            }
        }
    }
}
