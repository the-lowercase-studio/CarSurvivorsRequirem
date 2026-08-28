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
        [Inject] private readonly IInWorldSpaceSpawner<ExpParticleSpawner, float> _expParticleSpawner;

        [SerializeField] private GameObject _visual;
        [SerializeField] private VFXPlayer _deathVfxPlayer;
        private Enemy _enemy;
        private byte _startEffectsToFinish = 2;
        private byte _effectsToFinish;
        private Collider _collider;
        private Rigidbody _rb;

        public event EventHandler OnCompleted;

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _collider = GetComponent<Collider>();
            _rb = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            _rb.isKinematic = true;
            _collider.enabled = true;
            _effectsToFinish = _startEffectsToFinish;

            _deathVfxPlayer.OnVFXFinished += OnDeathEffectFinishedPlaying;
            _enemy.AudioClipPlayer.OnAudioClipFinished += OnDeathEffectFinishedPlaying;

            _enemy.Health.OnNoHealth += Health_OnNoHealth;
        }

        private void OnDisable()
        {
            _deathVfxPlayer.OnVFXFinished -= OnDeathEffectFinishedPlaying;
            _enemy.AudioClipPlayer.OnAudioClipFinished -= OnDeathEffectFinishedPlaying;

            _enemy.Health.OnNoHealth -= Health_OnNoHealth;
        }

        private void Health_OnNoHealth(object sender, EventArgs e)
        {
            _rb.isKinematic = true;
            _collider.enabled = false;

            _visual.SetActive(false);

            _deathVfxPlayer.Play(new VFXPlayConfig());

            SpawnExp();

            _enemy.AudioClipPlayer.Play("Death");
        }

        private void OnDeathEffectFinishedPlaying(object sender, EventArgs e)
        {
            _effectsToFinish--;

            if (_effectsToFinish == 0)
            {
                OnCompleted?.Invoke(this, EventArgs.Empty);
            }
        }

        private void SpawnExp()
        {
            _expParticleSpawner.Spawn(transform.position, _enemy.Config.ExpForKill);
        }
    }
}
