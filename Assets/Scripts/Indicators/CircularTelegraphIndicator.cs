using System;
using DG.Tweening;
using Assets.Scripts.Indicators.Constants;
using Assets.Scripts.Navigation.GridSystem;
using UnityEngine;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Indicators
{
    public class CircularTelegraphIndicator : MonoBehaviour, ITelegraphIndicator
    {
        [SerializeField] private Transform _outerRing;
        [SerializeField] private Transform _innerFill;
        [SerializeField] private float _expandDuration = 0.2f;
        [SerializeField] private float _contractDuration = 0.15f;

        private Sequence _activeSequence;
        private Action _onImpactCallback;
        private Transform _followTarget;
        private Transform _reusableParent;
        private int _reusableGeneration;
        private bool _isReusable;

        public Vector3 SnappedPosition { get; private set; }

        private void OnDisable()
        {
            KillActiveSequence();
            _reusableGeneration++;
            _followTarget = null;
            _onImpactCallback = null;
            RestoreReusableParent();
        }

        private void LateUpdate()
        {
            if (_isReusable && _followTarget != null)
            {
                SynchronizeAttachedPosition();
            }
        }

        private void OnDestroy()
        {
            KillActiveSequence();
        }

        public Vector3 Show(Vector3 worldPosition, float radius, float duration, Grid worldGrid = null, Action onImpact = null, bool autoContractOnFillComplete = false, bool exactPosition = false)
        {
            _isReusable = false;
            KillActiveSequence();
            _onImpactCallback = onImpact;

            Vector3 finalPosition = worldPosition;
            if (!exactPosition && worldGrid != null && worldGrid.Cells != null)
            {
                finalPosition = SnapToWalkableCell(worldPosition, worldGrid);
            }

            SnappedPosition = finalPosition;
            Vector3 spawnPosition = finalPosition;
            spawnPosition.y += IndicatorConstants.GROUND_Y_OFFSET;

            transform.position = spawnPosition;
            gameObject.SetActive(true);

            float targetScaleFactor = radius / IndicatorConstants.CIRCLE_MESH_RADIUS;
            Vector3 targetScale = new Vector3(targetScaleFactor, 1f, targetScaleFactor);

            if (_outerRing != null)
            {
                _outerRing.localScale = Vector3.zero;
            }

            if (_innerFill != null)
            {
                _innerFill.localScale = Vector3.zero;
            }

            _activeSequence = DOTween.Sequence();

            if (_outerRing != null)
            {
                _activeSequence.Append(_outerRing.DOScale(targetScale, _expandDuration).SetEase(Ease.OutQuad));
            }

            if (_innerFill != null)
            {
                _activeSequence.Join(_innerFill.DOScale(targetScale, duration).SetEase(Ease.Linear));
            }
            else
            {
                _activeSequence.AppendInterval(duration);
            }

            _activeSequence.OnComplete(() =>
            {
                _onImpactCallback?.Invoke();
                if (autoContractOnFillComplete)
                {
                    PlayContractAndDismiss();
                }
            });

            return finalPosition;
        }

        public void ContractAndDismiss()
        {
            PlayContractAndDismiss();
        }

        public void ShowAttachedWarning(Transform target, float radius)
        {
            HideReusable();
            if (_outerRing == null || _innerFill == null)
            {
                throw new InvalidOperationException("Attached warnings require both ring and fill geometry.");
            }

            _isReusable = true;
            _reusableParent = transform.parent;
            // Detaching avoids inherited rotation, non-uniform scale, and shear.
            transform.SetParent(null, false);
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            _followTarget = target;
            SynchronizeAttachedPosition();
            float scale = radius / IndicatorConstants.CIRCLE_MESH_RADIUS;
            Vector3 geometryScale = new Vector3(scale, 1f, scale);
            // The ring includes an authored border beyond the nominal mesh radius.
            Bounds ringBounds = _outerRing.GetComponent<MeshFilter>().sharedMesh.bounds;
            float ringRadius = Mathf.Max(ringBounds.extents.x, ringBounds.extents.z);
            float ringScale = radius / ringRadius;
            _outerRing.localScale = new Vector3(ringScale, 1f, ringScale);
            _innerFill.localScale = geometryScale;
            gameObject.SetActive(true);
        }

        public void SynchronizeAttachedPosition()
        {
            Vector3 position = _followTarget.position;
            SnappedPosition = position;
            position.y += IndicatorConstants.GROUND_Y_OFFSET;
            transform.position = position;
        }

        public void HideReusable()
        {
            _reusableGeneration++;
            KillActiveSequence();
            _followTarget = null;
            _onImpactCallback = null;
            if (_outerRing != null)
            {
                _outerRing.localScale = Vector3.zero;
            }
            if (_innerFill != null)
            {
                _innerFill.localScale = Vector3.zero;
            }
            RestoreReusableParent();
            gameObject.SetActive(false);
        }

        public void ContractAndHideReusable(Action onHidden)
        {
            KillActiveSequence();
            int generation = ++_reusableGeneration;
            if (!gameObject.activeInHierarchy)
            {
                HideReusable();
                onHidden?.Invoke();
                return;
            }

            SynchronizeAttachedPosition();
            _activeSequence = DOTween.Sequence();
            _activeSequence.Join(_outerRing.DOScale(Vector3.zero, _contractDuration).SetEase(Ease.InQuad));
            _activeSequence.Join(_innerFill.DOScale(Vector3.zero, _contractDuration).SetEase(Ease.InQuad));
            _activeSequence.OnComplete(() =>
            {
                if (generation != _reusableGeneration)
                {
                    return;
                }
                HideReusable();
                onHidden?.Invoke();
            });
        }

        private void RestoreReusableParent()
        {
            if (_isReusable && _reusableParent != null && _reusableParent.gameObject.activeInHierarchy)
            {
                transform.SetParent(_reusableParent, false);
                _reusableParent = null;
            }
        }

        public void Dismiss()
        {
            KillActiveSequence();
            if (this != null && gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        private void PlayContractAndDismiss()
        {
            KillActiveSequence();

            _activeSequence = DOTween.Sequence();

            if (_outerRing != null)
            {
                _activeSequence.Join(_outerRing.DOScale(Vector3.zero, _contractDuration).SetEase(Ease.InQuad));
            }

            if (_innerFill != null)
            {
                _activeSequence.Join(_innerFill.DOScale(Vector3.zero, _contractDuration).SetEase(Ease.InQuad));
            }

            if (_outerRing == null && _innerFill == null)
            {
                if (this != null && gameObject != null)
                {
                    Destroy(gameObject);
                }
                return;
            }

            _activeSequence.OnComplete(() =>
            {
                if (this != null && gameObject != null)
                {
                    Destroy(gameObject);
                }
            });
        }

        private Vector3 SnapToWalkableCell(Vector3 originalPos, Grid grid)
        {
            Cell centerCell = WorldPosToCellConverter.GetCellFromGridByWorldPos(grid, originalPos);
            if (centerCell != null && CellStatusDescriber.IsWalkable(centerCell))
            {
                return new Vector3(centerCell.WorldPos.x, originalPos.y, centerCell.WorldPos.z);
            }

            if (centerCell == null)
            {
                return originalPos;
            }

            int centerX = centerCell.WorldGridPos.x;
            int centerY = centerCell.WorldGridPos.y;
            int gridWidth = grid.Width;
            int gridHeight = grid.Height;

            Cell bestCell = null;
            float minSqrDistance = float.MaxValue;

            for (int r = 1; r <= IndicatorConstants.MAX_SEARCH_RADIUS; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r)
                        {
                            continue;
                        }

                        int nx = centerX + dx;
                        int ny = centerY + dy;

                        if (nx >= 0 && nx < gridWidth && ny >= 0 && ny < gridHeight)
                        {
                            Cell candidate = grid.Cells[nx, ny];
                            if (candidate != null && CellStatusDescriber.IsWalkable(candidate))
                            {
                                float sqrDist = (candidate.WorldPos - originalPos).sqrMagnitude;
                                if (sqrDist < minSqrDistance)
                                {
                                    minSqrDistance = sqrDist;
                                    bestCell = candidate;
                                }
                            }
                        }
                    }
                }

                if (bestCell != null)
                {
                    break;
                }
            }

            if (bestCell != null)
            {
                return new Vector3(bestCell.WorldPos.x, originalPos.y, bestCell.WorldPos.z);
            }

            return originalPos;
        }

        private void KillActiveSequence()
        {
            if (_activeSequence != null && _activeSequence.IsActive())
            {
                _activeSequence.Kill();
            }
            _activeSequence = null;
        }
    }
}
