using Assets.ScriptableObjects;
using Assets.Scripts.Audio;
using Assets.Scripts.Extensions;
using Assets.Scripts.Projectiles;
using Assets.Scripts.Projectiles.Constants;
using Assets.Scripts.Spawners.WorldSpace;
using Assets.Scripts.VFX;
using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.Pool;

namespace Assets.Scripts.Skills.PlayerSkills.Minigun
{
    public class MinigunTurret : Turret<TurretConfigSO>,
        IInWorldSpaceSpawner<MinigunTurret, ProjectileSpawnConfig>
    {
        [SerializeField] private bool _inverseRotation;

        private Tween _rotationTween;
        private IVFXPlayer _muzzleFlashVFXPlayer;
        private IAudioClipPlayer _audioClipPlayer;
        private IObjectPool<Projectile> _projectilePool;

        public event EventHandler OnSpawnedEntityReleased;

        public uint CurrentlySpawnedObjectsCount { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            _muzzleFlashVFXPlayer = GetComponentInChildren<IVFXPlayer>();
            _audioClipPlayer = GetComponentInChildren<IAudioClipPlayer>();

            _projectilePool = new ObjectPool<Projectile>(
                createFunc: () => Instantiate(_turretsProejctile, _projectilesParent),
                actionOnGet: OnGetProjectile,
                actionOnRelease: OnReleaseProjectile,
                defaultCapacity: ProjectileConstants.DEFAULT_PROJECTILE_POOL_CAPACITY,
                maxSize: ProjectileConstants.MAX_PROJECTILE_POOL_SIZE
            );
        }

        public override void Initialize(TurretConfigSO config)
        {
            base.Initialize(config);

            _visual.localRotation = Quaternion.Euler(0, (_inverseRotation ? _config.RotationAngle : -_config.RotationAngle) * 0.5f, 0);

            StartInYAngleRotation();
        }

        private void StartInYAngleRotation()
        {
            if (_rotationTween != null)
            {
                _rotationTween.Kill();
            }

            _rotationTween = _visual.StartYAngleLocalRotationLoopTween(_config.RotationAngle, _config.RotationDuration, _inverseRotation);
        }

        public void Spawn(Vector3 pos, ProjectileSpawnConfig projectileSpawnConfig, int count = 1)
        {
            if (projectileSpawnConfig == null)
            {
                projectileSpawnConfig = GetProjectileSpawnConfigBasedOnGunTip();
            }

            for (int i = 0; i < count; i++)
            {
                Shoot(projectileSpawnConfig);
            }
        }

        public override void Shoot(float shootPreparingAnimationSpeed = 1f)
        {
            Shoot(GetProjectileSpawnConfigBasedOnGunTip());
        }

        private void Shoot(ProjectileSpawnConfig projectileSpawnConfig)
        {
            Projectile projectile = SpawnProjectile(projectileSpawnConfig);

            projectile.Initialize(_config.ProjectileStatsSO);

            _muzzleFlashVFXPlayer.Play(new());

            _audioClipPlayer.Play("Shoot");
        }

        private Projectile SpawnProjectile(ProjectileSpawnConfig projectileSpawnConfig)
        {
            Projectile projectile = _projectilePool.Get();
            projectile.transform.position = projectileSpawnConfig.Position;
            projectile.transform.rotation = projectileSpawnConfig.Rotation;
            projectile.SetMovementDirection(projectileSpawnConfig.MovementDirection);

            return projectile;
        }

        private void OnGetProjectile(Projectile projectile)
        {
            projectile.OnGet();

            projectile.OnLifeEnd -= OnProjectileLifeEnded;
            projectile.OnLifeEnd += OnProjectileLifeEnded;

            projectile.gameObject.SetActive(true);

            CurrentlySpawnedObjectsCount++;
        }

        private void OnReleaseProjectile(Projectile projectile)
        {
            projectile.OnRelease();

            projectile.OnLifeEnd -= OnProjectileLifeEnded;
            projectile.OnCanBeReleased -= OnProjectileLifeEnded;

            projectile.gameObject.SetActive(false);

            OnSpawnedEntityReleased?.Invoke(projectile, EventArgs.Empty);

            CurrentlySpawnedObjectsCount--;
        }

        private void OnProjectileLifeEnded(object sender, System.EventArgs e)
        {
            if (sender is Projectile projectile)
            {
                _projectilePool.Release(projectile);
            }
        }

        private ProjectileSpawnConfig GetProjectileSpawnConfigBasedOnGunTip()
        {
            return new ProjectileSpawnConfig
            {
                Position = _gunTip.position,
                Rotation = _gunTip.rotation,
                MovementDirection = _gunTip.forward,
                ProjectileConfigSO = _config.ProjectileStatsSO
            };
        }
    }
}

