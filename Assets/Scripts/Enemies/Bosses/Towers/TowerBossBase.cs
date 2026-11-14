using System;
using System.Collections.Generic;
using DG.Tweening;
using Assets.ScriptableObjects.Enemies.Bosses;
using Assets.Scripts.Audio;
using Assets.Scripts.DamageNumbers;
using Assets.Scripts.Enemies.Bosses.Towers.Arena;
using Assets.Scripts.Enemies.Bosses.Towers.Constants;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Indicators;
using Assets.Scripts.LevelSystem.Exp;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.Player;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.Shapes;
using Assets.Scripts.Spawners.WorldSpace;
using Assets.Scripts.StatusEffects;
using Assets.Scripts.VFX;
using Reflex.Attributes;
using UnityEngine;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Enemies.Bosses.Towers
{
    public interface ITowerBoss : IBossCombatTarget, IKnockable
    {
        TowerBossConfigSO BaseConfig { get; }
        Transform TurretHousing { get; }
        float BarrelPivotHeight { get; }
        Vector3 PlayerPosition { get; }
        event Action<ITowerBoss> OnBossDefeated;
        void StartEncounter();
        void ResetEncounter();
        Quaternion GetTurretAimRotation(Vector3 worldTarget);
        void SetTurretAimRotation(Quaternion worldRotation);
        void SnapTurretAim(Vector3 worldTarget);
        void UpdateTurretAim(float deltaTime, float turnSpeed = 5f);
        float GetBarrelPitchDegrees();
        void SetBarrelPitch(float localPitchDegrees);
        void ResetBarrelPitch(float blendDuration = 0f);
    }

    [RequireComponent(typeof(Health))]
    public abstract class TowerBossBase : MonoBehaviour, ITowerBoss
    {
        [Inject] private IPlayerManager _playerManager = null;
        [Inject] private readonly IGridManager _gridManager = null;
        [Inject] private readonly Reflex.Core.Container _container = null;
        [Inject] private readonly IInWorldSpaceSpawner<ExpParticleSpawner, float> _expParticleSpawner = null;
        [Inject] private readonly ITimeFreezeState _timeFreezeState = null;

        [Header("Telegraphs & Rig")]
        [Tooltip("Prefab instantiated for circular ground warning telegraphs.")]
        [SerializeField] private CircularTelegraphIndicator _circularTelegraphPrefab;
        [Tooltip("Prefab instantiated for rectangular lane ground warning telegraphs.")]
        [SerializeField] private RectangularTelegraphIndicator _rectangularTelegraphPrefab;
        [Tooltip("Muzzle transform from which projectiles or beams originate.")]
        [SerializeField] private Transform _muzzleTransform;
        [Tooltip("Transform rotated around world Y to aim the turret.")]
        [SerializeField] private Transform _turretHousing;
        [Tooltip("Optional barrel transform animated for firing recoil.")]
        [SerializeField] private Transform _barrelTransform;
        [Tooltip("Audio clip player used for tower sound effects.")]
        [SerializeField] private AudioClipPlayer _audioClipPlayer;

        [Header("Visual & Feedback")]
        [Tooltip("Optional VFX player triggered when the tower takes damage.")]
        [SerializeField] private VFXPlayer _hitVfxPlayer;
        [Tooltip("Optional VFX player triggered upon entering the enraged phase.")]
        [SerializeField] private VFXPlayer _enrageVfxPlayer;
        [Tooltip("Optional VFX player triggered when firing attacks.")]
        [SerializeField] private VFXPlayer _fireVfxPlayer;
        [Tooltip("Optional VFX player unparented and played on boss defeat.")]
        [SerializeField] private VFXPlayer _deathVfxPlayer;
        [Tooltip("Renderers tinted via MaterialPropertyBlock during enrage.")]
        [SerializeField] private Renderer[] _renderersForEnrage;

        private readonly List<ITelegraphIndicator> _activeTelegraphs = new List<ITelegraphIndicator>();
        private IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig> _damageNumbersSpawner;
        private EncounterArenaController _arenaController;
        private Vector3 _initialHousingLocalPosition;
        private Quaternion _initialBarrelLocalRotation = Quaternion.identity;
        private MaterialPropertyBlock _materialPropertyBlock;
        private Tween _barrelRecoilTween;
        private Tween _barrelPitchTween;
        private Sequence _housingRecoilSequence;
        private bool _isEnraged;

        public event Action OnPresentationChanged;
        public event Action<ITowerBoss> OnBossDefeated;

        public abstract TowerBossConfigSO BaseConfig { get; }

        public BossHealthBarKind HealthBarKind
        {
            get { return BossHealthBarKind.Tower; }
        }

        public float EnrageHealthPercent
        {
            get
            {
                TowerBossConfigSO config = BaseConfig;
                return config != null ? config.EnrageHealthPercent : TowerBossConstants.DEFAULT_ENRAGE_PERCENT;
            }
        }

        public bool IsActive
        {
            get { return isActiveAndEnabled && Health != null && Health.IsAlive(); }
        }

        public IHealth Health { get; private set; }

        public Transform Transform
        {
            get { return transform; }
        }

        public Transform TurretHousing
        {
            get { return _turretHousing; }
        }

        public float BarrelPivotHeight
        {
            get
            {
                return _barrelTransform != null
                    ? Mathf.Max(0.5f, _barrelTransform.position.y - transform.position.y)
                    : FallbackMuzzleHeight;
            }
        }

        public bool IsEnraged
        {
            get { return _isEnraged; }
        }

        public Grid WorldGrid
        {
            get { return _gridManager != null ? _gridManager.WorldGrid : null; }
        }

        public Vector3 MuzzlePosition
        {
            get
            {
                return _muzzleTransform != null
                    ? _muzzleTransform.position
                    : transform.position + Vector3.up * FallbackMuzzleHeight;
            }
        }

        public Vector3 PlayerPosition
        {
            get
            {
                if (_playerManager != null && _playerManager.GameObject != null)
                {
                    return _playerManager.GameObject.transform.position;
                }

                return transform.position;
            }
        }

        protected IPlayerManager PlayerManager
        {
            get { return _playerManager; }
        }

        protected Reflex.Core.Container Container
        {
            get { return _container; }
        }

        protected ITimeFreezeState TimeFreezeState
        {
            get { return _timeFreezeState; }
        }

        protected CircularTelegraphIndicator CircularTelegraphPrefab
        {
            get { return _circularTelegraphPrefab; }
        }

        protected RectangularTelegraphIndicator RectangularTelegraphPrefab
        {
            get { return _rectangularTelegraphPrefab; }
        }

        protected Transform MuzzleTransform
        {
            get { return _muzzleTransform; }
        }

        protected AudioClipPlayer AudioClipPlayer
        {
            get { return _audioClipPlayer; }
        }

        protected VFXPlayer FireVfxPlayer
        {
            get { return _fireVfxPlayer; }
        }

        protected EncounterArenaController ArenaController
        {
            get { return _arenaController; }
        }

        protected virtual float FallbackMuzzleHeight
        {
            get { return 4f; }
        }

        protected virtual float DamageNumberYOffset
        {
            get { return 2f; }
        }

        protected virtual string EnrageSfxKey
        {
            get { return null; }
        }

        protected virtual bool ShouldUpdateTurretAim
        {
            get { return false; }
        }

        protected virtual bool IsIdleStateActive
        {
            get { return false; }
        }

        protected virtual void Awake()
        {
            Health = GetComponent<IHealth>();
            _arenaController = GetComponent<EncounterArenaController>();
            _materialPropertyBlock = new MaterialPropertyBlock();
            _initialHousingLocalPosition = _turretHousing != null ? _turretHousing.localPosition : Vector3.zero;
            _initialBarrelLocalRotation = _barrelTransform != null ? _barrelTransform.localRotation : Quaternion.identity;

            ResolveOptionalDependencies();
            InitializeTower();
        }

        protected virtual void Start()
        {
            ResolveOptionalDependencies();
            MarkFootprintImpassable();
        }

        protected virtual void OnEnable()
        {
            TowerBossConfigSO config = BaseConfig;
            if (Health != null && config != null)
            {
                Health.MaxHealth = config.MaxHealth;
                float needed = Health.MaxHealth - Health.CurrentHealth;
                if (needed > 0f)
                {
                    Health.IncreaseHealth(needed);
                }

                Health.OnHealthChanged += Health_OnHealthChanged;
                Health.OnNoHealth += Health_OnNoHealth;
            }

            _isEnraged = false;
            InitializeStateMachineForEnable();
            RaisePresentationChanged();
        }

        protected virtual void OnDisable()
        {
            if (Health != null)
            {
                Health.OnHealthChanged -= Health_OnHealthChanged;
                Health.OnNoHealth -= Health_OnNoHealth;
            }

            KillRecoilTweens();
            DismissAllTelegraphs();
            OnBossDisabled();
            RaisePresentationChanged();
        }

        protected virtual void OnDestroy()
        {
            KillRecoilTweens();
            DismissAllTelegraphs();
            OnBossDestroyed();
        }

        protected virtual void Update()
        {
            if (_timeFreezeState != null && _timeFreezeState.IsFrozen)
            {
                return;
            }

            UpdateTowerStateMachine();

            if (ShouldUpdateTurretAim)
            {
                UpdateTurretAim(Time.deltaTime);
            }
            else if (IsIdleStateActive && _arenaController == null)
            {
                if (_playerManager != null && _playerManager.GameObject != null)
                {
                    Vector3 diff = _playerManager.GameObject.transform.position - transform.position;
                    diff.y = 0f;
                    TowerBossConfigSO config = BaseConfig;
                    float arenaRadius = config != null ? config.ArenaRadius : TowerBossConstants.DEFAULT_ARENA_RADIUS;
                    if (diff.sqrMagnitude <= arenaRadius * arenaRadius)
                    {
                        StartEncounter();
                    }
                }
            }
        }

        protected virtual void FixedUpdate()
        {
            if (_timeFreezeState != null && _timeFreezeState.IsFrozen)
            {
                return;
            }

            FixedUpdateTowerStateMachine();
        }

        public abstract void StartEncounter();

        public void ResetEncounter()
        {
            TowerBossConfigSO config = BaseConfig;
            if (Health != null && config != null)
            {
                float needed = Health.MaxHealth - Health.CurrentHealth;
                if (needed > 0f)
                {
                    Health.IncreaseHealth(needed);
                }
            }

            _isEnraged = false;
            ResetMaterials();
            DismissAllTelegraphs();
            KillRecoilTweens();

            if (_turretHousing != null)
            {
                _turretHousing.localPosition = _initialHousingLocalPosition;
            }

            OnBossReset();
            RaisePresentationChanged();
        }

        public void TakeDamage(float damage)
        {
            if (Health == null || !Health.IsAlive())
            {
                return;
            }

            if (IsIdleStateActive)
            {
                if (_arenaController != null)
                {
                    _arenaController.NotifyDirectCombatEngaged();
                }
                else
                {
                    StartEncounter();
                }
            }

            Vector3 spawnPos = transform.position + Vector3.up * DamageNumberYOffset;

            if (_damageNumbersSpawner != null)
            {
                _damageNumbersSpawner.Spawn(
                    spawnPos,
                    new DamageNubmersSpawnerConfig(damage, ShapeModes.Hemisphere)
                );
            }

            Health.DecreaseHealth(damage);

            if (Health.IsAlive() && _hitVfxPlayer != null)
            {
                _hitVfxPlayer.Play(new VFXPlayConfig());
            }
        }

        public void TakeFullHpDamage()
        {
            if (Health != null)
            {
                TakeDamage(Health.MaxHealth);
            }
        }

        public void ApplyKnockBack(Vector3 direction, float power, float timeToArriveAtLocation)
        {
            // Stationary tower fortifications are immune to knockback.
        }

        public CircularTelegraphIndicator ShowCircularTelegraph(Vector3 position, float radius, float duration, Action onImpact = null, bool autoContractOnFillComplete = true)
        {
            if (_circularTelegraphPrefab == null)
            {
                return null;
            }

            PruneInactiveTelegraphs();

            CircularTelegraphIndicator indicator = Instantiate(_circularTelegraphPrefab);
            indicator.SetFreezable(true);
            _activeTelegraphs.Add(indicator);
            indicator.Show(position, radius, duration, WorldGrid, onImpact, autoContractOnFillComplete);
            return indicator;
        }

        public RectangularTelegraphIndicator ShowRectangularTelegraph(Vector3 origin, Vector3 direction, float length, float width, float duration, Action onImpact = null, bool autoContractOnFillComplete = false)
        {
            if (_rectangularTelegraphPrefab == null)
            {
                return null;
            }

            PruneInactiveTelegraphs();

            RectangularTelegraphIndicator indicator = Instantiate(_rectangularTelegraphPrefab);
            indicator.SetFreezable(true);
            _activeTelegraphs.Add(indicator);
            indicator.Show(origin, direction, length, width, duration, onImpact, autoContractOnFillComplete);
            return indicator;
        }

        public void DismissAllTelegraphs()
        {
            for (int i = _activeTelegraphs.Count - 1; i >= 0; i--)
            {
                ITelegraphIndicator indicator = _activeTelegraphs[i];
                if (indicator != null && indicator is UnityEngine.Object unityObj && unityObj != null)
                {
                    indicator.Dismiss();
                }
            }

            _activeTelegraphs.Clear();
        }

        public void PlayFireRecoil(string sfxKey, bool includeBarrelRecoil = true)
        {
            if (includeBarrelRecoil && _barrelTransform != null)
            {
                KillTween(ref _barrelPitchTween);

                _barrelRecoilTween?.Kill(true);
                _barrelRecoilTween = _barrelTransform.DOPunchRotation(new Vector3(-15f, 0f, 0f), 0.35f, 4, 0.5f).AsFreezable();
            }

            PlayHousingRecoil(0.35f, 0.1f, 0.4f, snapPrevious: true);
            PlayFireVfxAndAudio(sfxKey);
        }

        public void PlayBurstFireRecoil(string sfxKey, float basePitchDegrees, float barrelKickDegrees, float housingKickDistance, float duration)
        {
            float safeDuration = Mathf.Max(0.08f, duration);
            float kickDuration = safeDuration * 0.28f;
            float recoverDuration = safeDuration - kickDuration;

            if (_barrelTransform != null)
            {
                KillTween(ref _barrelPitchTween);
                KillTween(ref _barrelRecoilTween);

                Quaternion restRotation = _initialBarrelLocalRotation * Quaternion.Euler(basePitchDegrees, 0f, 0f);
                Quaternion kickRotation = _initialBarrelLocalRotation * Quaternion.Euler(basePitchDegrees + barrelKickDegrees, 0f, 0f);

                Sequence barrelSequence = DOTween.Sequence().AsFreezable();
                barrelSequence.Append(_barrelTransform.DOLocalRotateQuaternion(kickRotation, kickDuration).SetEase(Ease.OutQuad));
                barrelSequence.Append(_barrelTransform.DOLocalRotateQuaternion(restRotation, recoverDuration).SetEase(Ease.InOutSine));
                _barrelRecoilTween = barrelSequence;
            }

            PlayHousingRecoil(housingKickDistance, kickDuration, recoverDuration, snapPrevious: false);
            PlayFireVfxAndAudio(sfxKey);
        }

        public float GetBarrelPitchDegrees()
        {
            if (_barrelTransform == null)
            {
                return 0f;
            }

            Quaternion delta = Quaternion.Inverse(_initialBarrelLocalRotation) * _barrelTransform.localRotation;
            float angleX = delta.eulerAngles.x;
            return angleX > 180f ? angleX - 360f : angleX;
        }

        public void SetBarrelPitch(float localPitchDegrees)
        {
            if (_barrelTransform == null)
            {
                return;
            }

            KillTween(ref _barrelRecoilTween);
            KillTween(ref _barrelPitchTween);

            _barrelTransform.localRotation = _initialBarrelLocalRotation * Quaternion.Euler(localPitchDegrees, 0f, 0f);
        }

        public void ResetBarrelPitch(float blendDuration = 0f)
        {
            if (_barrelTransform == null)
            {
                return;
            }

            KillTween(ref _barrelPitchTween);
            KillTween(ref _barrelRecoilTween);

            if (blendDuration <= 0.01f)
            {
                _barrelTransform.localRotation = _initialBarrelLocalRotation;
                return;
            }

            _barrelPitchTween = _barrelTransform
                .DOLocalRotateQuaternion(_initialBarrelLocalRotation, blendDuration)
                .SetEase(Ease.InOutSine)
                .AsFreezable();
        }

        public Quaternion GetTurretAimRotation(Vector3 worldTarget)
        {
            if (_turretHousing == null)
            {
                return Quaternion.identity;
            }

            Vector3 targetDir = worldTarget - _turretHousing.position;
            targetDir.y = 0f;

            return targetDir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(targetDir, Vector3.up)
                : _turretHousing.rotation;
        }

        public void SetTurretAimRotation(Quaternion worldRotation)
        {
            if (_turretHousing != null)
            {
                _turretHousing.rotation = worldRotation;
            }
        }

        public void PlayAudioOneShot(string sfxKey)
        {
            if (_audioClipPlayer != null && !string.IsNullOrEmpty(sfxKey) && _audioClipPlayer.HasPlayableClip(sfxKey))
            {
                _audioClipPlayer.PlayOneShot(sfxKey);
            }
        }

        public void UpdateTurretAim(float deltaTime, float turnSpeed = 5f)
        {
            if (_turretHousing == null || (_timeFreezeState != null && _timeFreezeState.IsFrozen))
            {
                return;
            }

            Vector3 targetDir = PlayerPosition - _turretHousing.position;
            targetDir.y = 0f;

            if (targetDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(targetDir, Vector3.up);
                _turretHousing.rotation = Quaternion.Slerp(_turretHousing.rotation, targetRot, turnSpeed * deltaTime);
            }
        }

        public void SnapTurretAim(Vector3 worldTarget)
        {
            if (_turretHousing == null)
            {
                return;
            }

            Vector3 targetDir = worldTarget - _turretHousing.position;
            targetDir.y = 0f;

            if (targetDir.sqrMagnitude > 0.001f)
            {
                _turretHousing.rotation = Quaternion.LookRotation(targetDir, Vector3.up);
            }
        }

        protected abstract void InitializeTower();
        protected abstract void InitializeStateMachineForEnable();
        protected abstract void MarkFootprintImpassable();
        protected abstract void UpdateTowerStateMachine();
        protected abstract void FixedUpdateTowerStateMachine();
        protected abstract void OnBossReset();
        protected abstract void OnBossDefeatedCleanup();

        protected virtual void OnBossDisabled()
        {
        }

        protected virtual void OnBossDestroyed()
        {
        }

        protected virtual void OnEnrageTriggered()
        {
        }

        protected virtual void OnResolveAdditionalDependencies(Reflex.Core.Container container)
        {
        }

        protected void RaisePresentationChanged()
        {
            OnPresentationChanged?.Invoke();
        }

        private void ResolveOptionalDependencies()
        {
            if (_container == null)
            {
                return;
            }

            if (_damageNumbersSpawner == null && _container.HasBinding<IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>>())
            {
                _damageNumbersSpawner = _container.Resolve<IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>>();
            }

            if (_playerManager == null && _container.HasBinding<IPlayerManager>())
            {
                _playerManager = _container.Resolve<IPlayerManager>();
            }

            OnResolveAdditionalDependencies(_container);
        }

        private void PruneInactiveTelegraphs()
        {
            for (int i = _activeTelegraphs.Count - 1; i >= 0; i--)
            {
                ITelegraphIndicator ind = _activeTelegraphs[i];
                if (ind == null || (ind is UnityEngine.Object uObj && uObj == null))
                {
                    _activeTelegraphs.RemoveAt(i);
                }
            }
        }

        private void TriggerEnrage()
        {
            _isEnraged = true;

            PlayAudioOneShot(EnrageSfxKey);

            if (_enrageVfxPlayer != null)
            {
                _enrageVfxPlayer.Play(new VFXPlayConfig());
            }

            ApplyEnrageMaterials();
            OnEnrageTriggered();
            RaisePresentationChanged();
        }

        private void ApplyEnrageMaterials()
        {
            TowerBossConfigSO config = BaseConfig;
            if (_renderersForEnrage == null || config == null)
            {
                return;
            }

            for (int i = 0; i < _renderersForEnrage.Length; i++)
            {
                Renderer rend = _renderersForEnrage[i];
                if (rend == null)
                {
                    continue;
                }

                rend.GetPropertyBlock(_materialPropertyBlock);
                _materialPropertyBlock.SetColor(TowerBossConstants.BASE_COLOR_PROPERTY, config.EnrageColor);
                _materialPropertyBlock.SetColor(TowerBossConstants.EMISSION_COLOR_PROPERTY, config.EnrageEmissionColor);
                rend.SetPropertyBlock(_materialPropertyBlock);
            }
        }

        private void ResetMaterials()
        {
            if (_renderersForEnrage == null)
            {
                return;
            }

            for (int i = 0; i < _renderersForEnrage.Length; i++)
            {
                Renderer rend = _renderersForEnrage[i];
                if (rend == null)
                {
                    continue;
                }

                rend.SetPropertyBlock(null);
            }
        }

        private static void KillTween(ref Tween tween)
        {
            if (tween != null && tween.IsActive())
            {
                tween.Kill();
            }
            tween = null;
        }

        private void PlayHousingRecoil(float kickDistance, float kickDuration, float recoverDuration, bool snapPrevious)
        {
            if (_turretHousing == null)
            {
                return;
            }

            if (_housingRecoilSequence != null && _housingRecoilSequence.IsActive())
            {
                _housingRecoilSequence.Kill(snapPrevious);
            }

            _housingRecoilSequence = DOTween.Sequence().AsFreezable();
            Vector3 localForward = _turretHousing.localRotation * Vector3.forward;
            Vector3 recoilOffset = _initialHousingLocalPosition - localForward * kickDistance;
            _housingRecoilSequence.Append(_turretHousing.DOLocalMove(recoilOffset, kickDuration).SetEase(Ease.OutQuad));
            _housingRecoilSequence.Append(_turretHousing.DOLocalMove(_initialHousingLocalPosition, recoverDuration).SetEase(Ease.InOutSine));
        }

        private void PlayFireVfxAndAudio(string sfxKey)
        {
            if (_fireVfxPlayer != null)
            {
                _fireVfxPlayer.Play(new VFXPlayConfig());
            }

            PlayAudioOneShot(sfxKey);
        }

        private void KillRecoilTweens()
        {
            KillTween(ref _barrelRecoilTween);
            KillTween(ref _barrelPitchTween);

            if (_housingRecoilSequence != null && _housingRecoilSequence.IsActive())
            {
                _housingRecoilSequence.Kill();
            }
            _housingRecoilSequence = null;

            if (_turretHousing != null)
            {
                _turretHousing.localPosition = _initialHousingLocalPosition;
            }
        }

        private void Health_OnHealthChanged(object sender, EventArgs e)
        {
            TowerBossConfigSO config = BaseConfig;
            if (Health == null || !Health.IsAlive() || config == null)
            {
                return;
            }

            float healthPercentage = Health.CurrentHealth / Health.MaxHealth;
            if (healthPercentage <= config.EnrageHealthPercent && !_isEnraged)
            {
                TriggerEnrage();
            }
        }

        private void Health_OnNoHealth(object sender, EventArgs e)
        {
            OnBossDefeatedCleanup();

            if (_deathVfxPlayer != null)
            {
                _deathVfxPlayer.transform.SetParent(null);
                _deathVfxPlayer.Play(new VFXPlayConfig(destroyOnEnd: true));
            }

            TowerBossConfigSO config = BaseConfig;
            if (_expParticleSpawner != null && config != null)
            {
                _expParticleSpawner.Spawn(transform.position, config.ExpReward);
            }

            OnBossDefeated?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
