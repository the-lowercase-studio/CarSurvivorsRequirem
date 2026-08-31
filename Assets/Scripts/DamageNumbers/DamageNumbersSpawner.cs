using System;
using Assets.Scripts.Common.Types;
using Assets.Scripts.DamageNumbers.Constants;
using Assets.Scripts.Effects;
using Assets.Scripts.ObjectLifecycle.Actions;
using Assets.Scripts.Shapes;
using Assets.Scripts.Spawners.WorldSpace;
using Assets.Scripts.Utils;
using UnityEngine;
using UnityEngine.Pool;

namespace Assets.Scripts.DamageNumbers
{
    public class DamageNubmersSpawnerConfig
    {
        public float Damage;
        public ShapeModes SpawnShapeMode;

        public DamageNubmersSpawnerConfig(float damage, ShapeModes spawnShapeMode)
        {
            Damage = damage;
            SpawnShapeMode = spawnShapeMode;
        }

        public void Deconstruct(out float damage, out ShapeModes spawnShapeMode)
        {
            damage = Damage;
            spawnShapeMode = SpawnShapeMode;
        }
    }

    public class DamageNumbersSpawner : MonoBehaviour,
        IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>,
        IEnableDisableFunctionalityTrigger<DamageNumbersSpawner>
    {
        [Serializable]
        private struct VisualApearanceByDamageTreshold
        {
            [SerializeField] private float _treshold;
            [SerializeField] private DamageNumberApearance _damagePopupApearance;

            public float Treshold
            {
                get
                {
                    return _treshold;
                }
            }

            public DamageNumberApearance DamagePopupApearance
            {
                get
                {
                    return _damagePopupApearance;
                }
            }

            public VisualApearanceByDamageTreshold(float treshold, DamageNumberApearance damagePopupApearance)
            {
                _treshold = treshold;
                _damagePopupApearance = damagePopupApearance;
            }
        }

        [SerializeField] private float _damagePopupVisibilityDuration;
        [SerializeField] private DamageNumber _damagePopupPrefab;
        [SerializeField] private VisualApearanceByDamageTreshold[] _visualApearanceByDamageTresholds;
        [SerializeField] private FloatValueRange _popupsSpeedRange;
        [SerializeField] private float _popupsMovementRange = DamageNumberConstants.DEFAULT_POPUPS_MOVEMENT_RANGE;

        private Camera _mainCamera;
        private bool _isPopupsEnabled = true;
        private IObjectPool<DamageNumber> _damageNumberPool;

        public event EventHandler OnSpawnedEntityReleased;

        public uint CurrentlySpawnedObjectsCount { get; private set; }

        private void Awake()
        {
            if (_damagePopupPrefab == null)
            {
                Debug.LogError($"[{nameof(DamageNumbersSpawner)}] DamagePopupPrefab is not assigned on '{name}'!");
            }

            if (_visualApearanceByDamageTresholds == null || _visualApearanceByDamageTresholds.Length == 0)
            {
                Debug.LogError($"[{nameof(DamageNumbersSpawner)}] VisualApearanceByDamageTresholds is empty on '{name}'!");
            }

            _damageNumberPool = new ObjectPool<DamageNumber>(
                createFunc: CreateDamageNumber,
                actionOnGet: (obj) => obj.gameObject.SetActive(true),
                actionOnRelease: (obj) => obj.gameObject.SetActive(false),
                actionOnDestroy: (obj) => Destroy(obj.gameObject),
                collectionCheck: false,
                defaultCapacity: DamageNumberConstants.DEFAULT_POOL_CAPACITY,
                maxSize: DamageNumberConstants.MAX_POOL_SIZE
            );
        }

        public void EnableFunctionality()
        {
            _isPopupsEnabled = true;
        }

        public void DisableFunctionality()
        {
            _isPopupsEnabled = false;
        }

        public void Initialize(Camera mainCamera)
        {
            _mainCamera = mainCamera;
        }

        public void Spawn(Vector3 pos, DamageNubmersSpawnerConfig specificConfig, int count = 1)
        {
            if (!_isPopupsEnabled)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                DamageNumber damageNumber = _damageNumberPool.Get();
                damageNumber.transform.position = pos;
                damageNumber.transform.rotation = Quaternion.identity;

                var (damage, spawnShapeMode) = specificConfig;

                VisualApearanceByDamageTreshold? visualApearanceByDamageTreshold
                    = FindCorrectVisualApearanceByTreshold(damage);

                if (visualApearanceByDamageTreshold is null)
                {
                    _damageNumberPool.Release(damageNumber);
                    return;
                }

                Vector3 dest = GetDestinationBasedOnSpawnShapeMode(pos, spawnShapeMode);
                damageNumber.Initialize(new DamageNumberConfig(damage, visualApearanceByDamageTreshold.Value.DamagePopupApearance), pos, dest, _damagePopupVisibilityDuration);

                damageNumber.OnLifeEnd += DamageNumber_OnLifeEnd;

                CurrentlySpawnedObjectsCount++;
            }
        }

        private DamageNumber CreateDamageNumber()
        {
            DamageNumber damageNumber = Instantiate(_damagePopupPrefab, transform);
            InitializeCameraFacingEffects(damageNumber);

            return damageNumber;
        }

        private void InitializeCameraFacingEffects(DamageNumber damageNumber)
        {
            if (_mainCamera == null)
            {
                return;
            }

            FaceMainCameraDirection[] cameraFacingEffects =
                damageNumber.GetComponentsInChildren<FaceMainCameraDirection>(true);

            foreach (FaceMainCameraDirection cameraFacingEffect in cameraFacingEffects)
            {
                cameraFacingEffect.Initialize(_mainCamera);
            }
        }

        private void DamageNumber_OnLifeEnd(object sender, EventArgs args)
        {
            if (sender is DamageNumber damageNumber)
            {
                CurrentlySpawnedObjectsCount--;
                damageNumber.OnLifeEnd -= DamageNumber_OnLifeEnd;
                _damageNumberPool.Release(damageNumber);
                OnSpawnedEntityReleased?.Invoke(damageNumber, EventArgs.Empty);
            }
        }

        private Vector3 GetDestinationBasedOnSpawnShapeMode(Vector3 startPos, ShapeModes spawnShapeMode)
        {
            return spawnShapeMode switch
            {
                ShapeModes.Sphere => RandomUtility.GetRandomPointOnSphereSurface(startPos, _popupsMovementRange),
                ShapeModes.Hemisphere => RandomUtility.GetRandomPointOnHemisphereSurface(startPos, _popupsMovementRange),
                _ => transform.position,
            };
        }

        private VisualApearanceByDamageTreshold? FindCorrectVisualApearanceByTreshold(float damage)
        {
            for (int i = _visualApearanceByDamageTresholds.Length - 1; i >= 0; i--)
            {
                if (_visualApearanceByDamageTresholds[i].Treshold <= damage)
                {
                    return _visualApearanceByDamageTresholds[i];
                }
            }

            return null;
        }
    }
}

