using System;
using Assets.Scripts.Audio;
using Assets.Scripts.Enemies.Barrel.Constants;
using Assets.Scripts.Enemies.Base;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Indicators;
using Assets.Scripts.LevelSystem.Exp;
using Assets.Scripts.ObjectLifecycle.Actions;
using Assets.Scripts.Spawners.WorldSpace;
using Assets.Scripts.VFX;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Enemies.Barrel
{
    [RequireComponent(typeof(Enemy), typeof(Health), typeof(EnemyMovementController))]
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public class BarrelEnemyDeathHandler : MonoBehaviour, INeedToCompleteBeforeDisable
    {
        [Inject] private readonly IInWorldSpaceSpawner<ExpParticleSpawner, float> _expParticleSpawner = null;

        [SerializeField] private GameObject _visual;
        [SerializeField] private VFXPlayer _deathVfxPlayer;
        [SerializeField] private VFXPlayer _bloodVfxPlayer;
        [SerializeField] private AudioClipPlayer _audioClipPlayer;
        [SerializeField] private CircularTelegraphIndicator _warning;
        [SerializeField] private BarrelEnemyExplosionController _explosionController;
        [SerializeField, Min(0.1f), Tooltip("Maximum scaled seconds to wait for death presentation callbacks.")]
        private float _presentationTimeout = 5f;

        private Enemy _enemy;
        private Health _health;
        private EnemyMovementController _movement;
        private BoxCollider _collider;
        private Rigidbody _rigidbody;
        private int _lifetime;
        private bool _hasDeathStarted;
        private bool _hasCompleted;
        private bool _isWarningComplete;
        private bool _isVfxComplete;
        private bool _isAudioComplete;
        private float _deathStartedAt;

        public event EventHandler OnCompleted;

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _health = GetComponent<Health>();
            _movement = GetComponent<EnemyMovementController>();
            _collider = GetComponent<BoxCollider>();
            _rigidbody = GetComponent<Rigidbody>();
            if (_visual == null || _warning == null || _explosionController == null
                || !(_presentationTimeout > 0f) || float.IsInfinity(_presentationTimeout))
            {
                throw new InvalidOperationException("Barrel death requires visual, warning, explosion controller, and a finite presentation timeout.");
            }
        }

        private void OnEnable()
        {
            _lifetime++;
            _hasDeathStarted = false;
            _hasCompleted = false;
            _collider.enabled = true;
            _rigidbody.isKinematic = true;
            _visual.SetActive(true);
            _health.OnNoHealth += OnNoHealth;
            if (_deathVfxPlayer != null)
            {
                _deathVfxPlayer.OnVFXFinished += OnVfxFinished;
            }
            if (_audioClipPlayer != null)
            {
                _audioClipPlayer.OnAudioClipFinished += OnAudioFinished;
            }
        }

        private void LateUpdate()
        {
            if (!_hasDeathStarted || _hasCompleted || _explosionController.HasDetonated)
            {
                return;
            }

            if (Time.time - _deathStartedAt >= _presentationTimeout)
            {
                Debug.LogError("Barrel death presentation exceeded its watchdog; completing release.", this);
                Complete();
            }
            else if (_isWarningComplete && _isVfxComplete && _isAudioComplete)
            {
                // A lifecycle phase defers even synchronous cosmetic completions past health dispatch.
                Complete();
            }
        }

        private void OnDisable()
        {
            _lifetime++;
            _health.OnNoHealth -= OnNoHealth;
            if (_deathVfxPlayer != null)
            {
                _deathVfxPlayer.OnVFXFinished -= OnVfxFinished;
                _deathVfxPlayer.StopPlayback();
            }
            if (_bloodVfxPlayer != null)
            {
                _bloodVfxPlayer.StopPlayback();
            }
            if (_audioClipPlayer != null)
            {
                _audioClipPlayer.OnAudioClipFinished -= OnAudioFinished;
                _audioClipPlayer.StopPlayback();
            }
            _warning.HideReusable();
            _hasDeathStarted = false;
            _hasCompleted = false;
            _isWarningComplete = false;
            _isVfxComplete = false;
            _isAudioComplete = false;
            _deathStartedAt = 0f;
        }

        public void HideBody()
        {
            _explosionController.CancelForDeath();
            _movement.StopMovement();
            _collider.enabled = false;
            _rigidbody.isKinematic = true;
            _visual.SetActive(false);
        }

        public void CompleteDetonationAfterDispatch()
        {
            if (_hasDeathStarted && _explosionController.HasDetonated)
            {
                Complete();
            }
        }

        private void Complete()
        {
            if (_hasCompleted || !isActiveAndEnabled)
            {
                return;
            }
            _hasCompleted = true;
            OnCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void OnNoHealth(object sender, EventArgs e)
        {
            if (_hasDeathStarted)
            {
                return;
            }
            _hasDeathStarted = true;
            _deathStartedAt = Time.time;
            HideBody();
            int lifetime = _lifetime;
            _expParticleSpawner.Spawn(transform.position, _enemy.Config.ExpForKill);
            if (lifetime != _lifetime || !isActiveAndEnabled || _explosionController.HasDetonated)
            {
                return;
            }

            _isWarningComplete = !_warning.gameObject.activeInHierarchy;
            _isVfxComplete = _deathVfxPlayer == null || !_deathVfxPlayer.HasParticleSystems;
            _isAudioComplete = _audioClipPlayer == null
                || !_audioClipPlayer.HasPlayableClip(BarrelEnemyConstants.DEATH_AUDIO);
            if (!_isWarningComplete)
            {
                _warning.ContractAndHideReusable(() =>
                {
                    if (lifetime == _lifetime && _hasDeathStarted)
                    {
                        _isWarningComplete = true;
                    }
                });
            }
            if (!_isVfxComplete)
            {
                _deathVfxPlayer.Play(new VFXPlayConfig());
            }
            if (!_isAudioComplete)
            {
                _audioClipPlayer.Play(BarrelEnemyConstants.DEATH_AUDIO);
            }
        }

        private void OnVfxFinished(object sender, EventArgs e)
        {
            if (_hasDeathStarted)
            {
                _isVfxComplete = true;
            }
        }

        private void OnAudioFinished(object sender, EventArgs e)
        {
            if (_hasDeathStarted)
            {
                _isAudioComplete = true;
            }
        }
    }
}
