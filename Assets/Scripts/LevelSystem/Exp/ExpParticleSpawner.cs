using Assets.Scripts.Spawners.WorldSpace;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;

namespace Assets.Scripts.LevelSystem.Exp
{
    public class ExpParticleSpawner : MonoBehaviour, IInWorldSpaceSpawner<ExpParticleSpawner, float>
    {
        public struct ExpParticleSpawnData
        {
            public Vector3 Pos;
            public float Exp;

            public ExpParticleSpawnData(Vector3 pos, float exp)
            {
                Pos = pos;
                Exp = exp;
            }
        }

        [Serializable]
        private struct ExpTresholdDevider
        {
            [SerializeField, FormerlySerializedAs("Treshold")] private float _treshold;
            [SerializeField, FormerlySerializedAs("Divider")] private float _divider;

            public float Treshold => _treshold;
            public float Divider => _divider;
        }

        [SerializeField] private Transform _expParticlesParent;
        [SerializeField] private float _particlesYOffset;
        [SerializeField] private ExpParticle _expParticlePrefab;
        [SerializeField] private ExpTresholdDevider[] _expTresholdDeviders;
        [SerializeField, Range(0, 30f)] private float _spawningCircleRadius;

        private const float CHECK_QUEUED_EXP_SPAWNS_DELAY = 0.2f;

        private Queue<ExpParticleSpawnData> _queuedExpSpawns = new();
        private IObjectPool<ExpParticle> _expParticlePool;

        public event EventHandler OnSpawnedEntityReleased;

        public uint CurrentlySpawnedObjectsCount { get; private set; }

        private void Awake()
        {
            _expParticlePool = new ObjectPool<ExpParticle>(
                createFunc: () => Instantiate(_expParticlePrefab, transform),
                actionOnGet: OnGet,
                actionOnRelease: OnRelease,
                defaultCapacity: 20,
                maxSize: 100
            );
        }

        private void OnEnable()
        {
            InvokeRepeating(
                nameof(SpawnParticlesBasedOnExpAmount),
                CHECK_QUEUED_EXP_SPAWNS_DELAY,
                CHECK_QUEUED_EXP_SPAWNS_DELAY
            );
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(SpawnParticlesBasedOnExpAmount));
        }

        private void OnGet(ExpParticle expParticle)
        {
            expParticle.OnGet();

            expParticle.OnCanBeReleased += ExpParticle_OnRelease;

            expParticle.gameObject.SetActive(true);

            CurrentlySpawnedObjectsCount++;
        }

        private void OnRelease(ExpParticle expParticle)
        {
            expParticle.OnRelease();

            expParticle.OnCanBeReleased -= ExpParticle_OnRelease;

            expParticle.gameObject.SetActive(false);

            OnSpawnedEntityReleased?.Invoke(this, EventArgs.Empty);

            CurrentlySpawnedObjectsCount--;
        }

        private void ExpParticle_OnRelease(object sender, EventArgs args)
        {
            if (sender is ExpParticle expParticle)
            {
                _expParticlePool.Release(expParticle);
            }
        }

        public void Spawn(Vector3 pos, float expValue, int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                _queuedExpSpawns.Enqueue(new ExpParticleSpawnData(pos, expValue));
            }
        }

        private void SpawnParticlesBasedOnExpAmount()
        {
            if (_expTresholdDeviders.Length == 0)
            {
                Debug.LogError("Exp treshold deviders not set for: " + transform.name);
            }

            if (_queuedExpSpawns.Count == 0)
            {
                return;
            }

            ExpTresholdDevider[] reversedExpTresholdDeviders = _expTresholdDeviders.Reverse().ToArray();

            while (_queuedExpSpawns.Count > 0)
            {
                ExpParticleSpawnData expParticleSpawnData = _queuedExpSpawns.Dequeue();
                float exp = expParticleSpawnData.Exp;
                Vector3 sphereCenterPos = expParticleSpawnData.Pos;

                ExpTresholdDevider expTresholdDevider =
                    reversedExpTresholdDeviders.FirstOrDefault(etd => etd.Treshold <= exp);

                float expPart = exp / expTresholdDevider.Divider;
                for (int i = 0; i < expTresholdDevider.Divider; i++)
                {
                    ExpParticle expParticle = SpawnExpParticleOnRandomPointInSphere(exp, sphereCenterPos);

                    expParticle.OnExpReachedTarget += ExpParticle_OnExpReachedTarget;
                }
            }
        }

        private ExpParticle SpawnExpParticleOnRandomPointInSphere(float exp, Vector3 sphereCenterPos)
        {
            Vector3 randomPos = RandomUtility.GetRandomPointInSphere(sphereCenterPos, _spawningCircleRadius);
            randomPos.y = _particlesYOffset;

            ExpParticle expParticle = _expParticlePool.Get();
            expParticle.transform.SetParent(_expParticlesParent, false);
            expParticle.transform.position = randomPos;
            expParticle.transform.rotation = Quaternion.identity;
            expParticle.SetSizeAndMaterialBasedOnExpAmount(exp);

            return expParticle;
        }

        private void ExpParticle_OnExpReachedTarget(object sender, EventArgs e)
        {
            if (sender is ExpParticle expParticle)
            {
                expParticle.CollectExp();

                expParticle.OnExpReachedTarget -= ExpParticle_OnExpReachedTarget;
            }
        }
    }
}
