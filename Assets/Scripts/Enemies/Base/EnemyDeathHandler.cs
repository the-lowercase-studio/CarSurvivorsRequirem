using Assets.Scripts.LevelSystem.Exp;
using Assets.Scripts.ObjectLifecycle.Actions;
using Assets.Scripts.Spawners.WorldSpace;
using Assets.Scripts.VFX;
using Reflex.Attributes;
using System;
using UnityEngine;

namespace Assets.Scripts.Enemies.Base
{
    [RequireComponent(typeof(Enemy))]
    public class EnemyDeathHandler : MonoBehaviour, INeedToCompleteBeforeDisable
    {
        [Inject] private readonly IInWorldSpaceSpawner<ExpParticleSpawner, float> _expParticleSpawner = null;

        [SerializeField] private GameObject _visual;
        [SerializeField] private VFXPlayer _deathVfxPlayer;
        [SerializeField, Min(0.1f), Tooltip("Maximum scaled seconds to wait for death presentation callbacks.")]
        private float _presentationTimeout = 5f;
        private Enemy _enemy;
        private byte _effectsToFinish;
        private bool _hasDeathStarted;
        private bool _hasCompleted;
        private float _deathStartedAt;
        private Collider _collider;
        private Rigidbody _rb;

        public event EventHandler OnCompleted;

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _collider = GetComponent<Collider>();
            _rb = GetComponent<Rigidbody>();
            if (!(_presentationTimeout > 0f) || float.IsInfinity(_presentationTimeout))
            {
                throw new InvalidOperationException("EnemyDeathHandler requires a finite positive presentation timeout.");
            }
        }

        private void OnEnable()
        {
            _rb.isKinematic = true;
            _collider.enabled = true;
            _hasDeathStarted = false;
            _hasCompleted = false;
            _deathStartedAt = 0f;

            byte pendingEffects = 0;
            if (_deathVfxPlayer != null)
            {
                _deathVfxPlayer.OnVFXFinished += OnDeathEffectFinishedPlaying;
                pendingEffects++;
            }

            if (_enemy.AudioClipPlayer != null)
            {
                _enemy.AudioClipPlayer.OnAudioClipFinished += OnDeathEffectFinishedPlaying;
                pendingEffects++;
            }

            _effectsToFinish = pendingEffects;
            _enemy.Health.OnNoHealth += Health_OnNoHealth;
        }

        private void LateUpdate()
        {
            if (!_hasDeathStarted || _hasCompleted)
            {
                return;
            }

            if (_effectsToFinish == 0)
            {
                Complete();
                return;
            }

            if (Time.time - _deathStartedAt >= _presentationTimeout)
            {
                Debug.LogError("Enemy death presentation exceeded its watchdog; completing release.", this);
                Complete();
            }
        }

        private void OnDisable()
        {
            if (_deathVfxPlayer != null)
            {
                _deathVfxPlayer.OnVFXFinished -= OnDeathEffectFinishedPlaying;
            }

            if (_enemy.AudioClipPlayer != null)
            {
                _enemy.AudioClipPlayer.OnAudioClipFinished -= OnDeathEffectFinishedPlaying;
            }

            _enemy.Health.OnNoHealth -= Health_OnNoHealth;
            _hasDeathStarted = false;
            _hasCompleted = false;
            _deathStartedAt = 0f;
        }

        private void Health_OnNoHealth(object sender, EventArgs e)
        {
            if (_hasDeathStarted)
            {
                return;
            }

            _hasDeathStarted = true;
            _hasCompleted = false;
            _deathStartedAt = Time.time;

            _rb.isKinematic = true;
            _collider.enabled = false;

            _visual.SetActive(false);

            if (_deathVfxPlayer != null)
            {
                _deathVfxPlayer.Play(new VFXPlayConfig());
            }

            SpawnExp();

            if (_enemy.AudioClipPlayer != null)
            {
                _enemy.AudioClipPlayer.Play("Death");
            }
        }

        private void OnDeathEffectFinishedPlaying(object sender, EventArgs e)
        {
            if (!_hasDeathStarted || _hasCompleted)
            {
                return;
            }

            if (_effectsToFinish > 0)
            {
                _effectsToFinish--;
            }

            if (_effectsToFinish == 0)
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

        private void SpawnExp()
        {
            _expParticleSpawner.Spawn(transform.position, _enemy.Config.ExpForKill);
        }
    }
}
