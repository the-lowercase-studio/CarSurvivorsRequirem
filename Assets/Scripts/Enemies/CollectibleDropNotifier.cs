using System;
using System.Collections.Generic;
using Assets.Scripts.Skills.ObjectsImpactingSkills.Crate;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.Pool;
using DG.Tweening;

namespace Assets.Scripts.Enemies
{
    public interface ICollectibleDropNotifier
    {
        event EventHandler OnSkillUpgradeCollectibleCollected;
        void SpawnCollectible(GameObject prefab, Vector3 spawnPos, Vector3 targetPos);
    }

    public class CollectibleDropNotifier : MonoBehaviour, ICollectibleDropNotifier
    {
        [Inject] private readonly DropAnimationConfiguration _animationConfig;

        [SerializeField] private Transform _collectibleItemsParent;

        public event EventHandler OnSkillUpgradeCollectibleCollected;

        private readonly Dictionary<GameObject, ObjectPool<GameObject>> _pools = new();
        private readonly Dictionary<GameObject, ObjectPool<GameObject>> _instancePoolMap = new();

        private ObjectPool<GameObject> GetOrCreatePool(GameObject prefab)
        {
            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new ObjectPool<GameObject>(
                    createFunc: () => Instantiate(prefab, _collectibleItemsParent),
                    actionOnGet: go => {
                        go.SetActive(true);
                        if (go.TryGetComponent<Pooling.IPoolable>(out var poolable))
                        {
                            poolable.OnGet();
                            poolable.OnCanBeReleased += Collectible_OnCanBeReleased;
                        }
                        if (go.TryGetComponent<ICollectible>(out var collectible))
                        {
                            collectible.OnCollected += Collectible_OnCollected;
                        }
                    },
                    actionOnRelease: go => {
                        if (go.TryGetComponent<Pooling.IPoolable>(out var poolable))
                        {
                            poolable.OnRelease();
                            poolable.OnCanBeReleased -= Collectible_OnCanBeReleased;
                        }
                        if (go.TryGetComponent<ICollectible>(out var collectible))
                        {
                            collectible.OnCollected -= Collectible_OnCollected;
                        }
                        go.SetActive(false);
                    },
                    actionOnDestroy: go => Destroy(go)
                );
                _pools.Add(prefab, pool);
            }
            return pool;
        }

        public void SpawnCollectible(GameObject prefab, Vector3 spawnPos, Vector3 targetPos)
        {
            var pool = GetOrCreatePool(prefab);
            var go = pool.Get();
            _instancePoolMap[go] = pool;

            go.transform.position = spawnPos;
            go.transform.localScale = Vector3.zero;

            float jumpPower = UnityEngine.Random.Range(_animationConfig.MinJumpPower, _animationConfig.MaxJumpPower);
            float duration = _animationConfig.ScatterDuration * UnityEngine.Random.Range(_animationConfig.MinDurationMultiplier, _animationConfig.MaxDurationMultiplier);

            var sequence = DG.Tweening.DOTween.Sequence();
            sequence.Join(go.transform.DOScale(Vector3.one, _animationConfig.ScaleDuration).SetEase(DG.Tweening.Ease.OutBack));
            sequence.Join(go.transform.DOJump(targetPos, jumpPower, 1, duration));
        }

        private void Collectible_OnCollected(object sender, EventArgs e)
        {
            if (sender is ICollectible collectible)
            {
                if (collectible is ISkillUpgradeCollectible)
                {
                    OnSkillUpgradeCollectibleCollected?.Invoke(collectible, EventArgs.Empty);
                }
            }
        }

        private void Collectible_OnCanBeReleased(object sender, EventArgs e)
        {
            if (sender is Pooling.IPoolable poolable && sender is MonoBehaviour mb)
            {
                var go = mb.gameObject;
                if (_instancePoolMap.TryGetValue(go, out var pool))
                {
                    pool.Release(go);
                }
            }
        }
    }
}
