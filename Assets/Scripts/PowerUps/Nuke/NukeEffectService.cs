using System;
using System.Collections.Generic;
using Assets.Scripts.Audio;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Base;
using Assets.Scripts.Enemies.Bosses;
using Assets.Scripts.PowerUps.Config;
using Assets.Scripts.PowerUps.Constants;
using Assets.Scripts.PowerUps.Presentation;
using Assets.Scripts.Spawners.Enemies;
using Assets.Scripts.VFX;
using DG.Tweening;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts.PowerUps.Nuke
{
    public class NukeEffectService : MonoBehaviour
    {
        [Inject] private readonly ICollectibleDropNotifier _collectibleDropNotifier = null;
        [Inject] private readonly ICollectibleDropSuppressor _dropSuppressor = null;
        [Inject] private readonly IActiveEnemiesProvider _activeEnemiesProvider = null;
        [Inject] private readonly IBossEncounterService _bossEncounterService = null;
        [Inject] private readonly Camera _camera = null;

        [SerializeField] private NukeConfigSO _config;
        [SerializeField] private Volume _blastVolume;
        [SerializeField] private ScreenOverlayPresenter _dirtOverlayPresenter;
        [SerializeField] private VFXPlayer _blastVfxPlayer;
        [SerializeField] private AudioClipPlayer _audioClipPlayer;

        private readonly Plane[] _frustumPlanes = new Plane[6];
        private readonly List<IBossCombatTarget> _engagedBossesBuffer = new List<IBossCombatTarget>(4);
        private List<Enemy> _activeEnemiesBuffer;
        private Sequence _presentationSequence;
        private bool _isPendingExecution;

        private void Awake()
        {
            if (_config == null)
            {
                throw new InvalidOperationException("NukeEffectService requires an authored NukeConfigSO.");
            }

            _config.ValidateConfiguration();
            _activeEnemiesBuffer = new List<Enemy>(_config.SnapshotCapacity);

            if (_blastVolume != null)
            {
                _blastVolume.weight = 0f;
            }

            if (_dirtOverlayPresenter != null)
            {
                _dirtOverlayPresenter.ResetOverlay();
            }
        }

        private void Start()
        {
            if (_collectibleDropNotifier != null)
            {
                _collectibleDropNotifier.OnPowerUpCollected += HandlePowerUpCollected;
            }
        }

        private void Update()
        {
            if (!_isPendingExecution)
            {
                return;
            }

            _isPendingExecution = false;
            ExecuteNuke();
        }

        private void OnDisable()
        {
            _isPendingExecution = false;
            KillPresentation();
        }

        private void OnDestroy()
        {
            if (_collectibleDropNotifier != null)
            {
                _collectibleDropNotifier.OnPowerUpCollected -= HandlePowerUpCollected;
            }

            KillPresentation();
        }

        private void HandlePowerUpCollected(PowerUpType powerUpType)
        {
            if (powerUpType == PowerUpType.Nuke)
            {
                _isPendingExecution = true;
            }
        }

        private void ExecuteNuke()
        {
            if (_camera == null)
            {
                return;
            }

            GeometryUtility.CalculateFrustumPlanes(_camera, _frustumPlanes);

            if (_dropSuppressor != null && _activeEnemiesProvider != null)
            {
                _dropSuppressor.BeginDropSuppression();
                try
                {
                    _activeEnemiesBuffer.Clear();
                    _activeEnemiesProvider.CopyActiveEnemies(_activeEnemiesBuffer);

                    Vector3 enemyPadding = Vector3.one * (_config.EnemyVisibilityPadding * 2f);
                    for (int i = 0; i < _activeEnemiesBuffer.Count; i++)
                    {
                        Enemy enemy = _activeEnemiesBuffer[i];
                        if (enemy == null || enemy.Health == null || !enemy.Health.IsAlive())
                        {
                            continue;
                        }

                        Bounds bounds = new Bounds(enemy.transform.position, enemyPadding);
                        if (GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds))
                        {
                            enemy.TakeFullHpDamage();
                        }
                    }
                }
                finally
                {
                    _dropSuppressor.EndDropSuppression();
                }
            }

            if (_bossEncounterService != null)
            {
                _engagedBossesBuffer.Clear();
                _bossEncounterService.CollectEngagedBosses(_engagedBossesBuffer);

                Vector3 bossPadding = Vector3.one * (_config.BossVisibilityPadding * 2f);
                float damagePercent = _config.BossDamagePercent / 100f;

                for (int i = 0; i < _engagedBossesBuffer.Count; i++)
                {
                    IBossCombatTarget boss = _engagedBossesBuffer[i];
                    if (boss == null || boss.Health == null || !boss.Health.IsAlive())
                    {
                        continue;
                    }

                    Bounds bounds = new Bounds(boss.Transform.position, bossPadding);
                    if (GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds))
                    {
                        float damage = boss.Health.MaxHealth * damagePercent;
                        boss.TakeDamage(damage);
                    }
                }
            }

            StartPresentation();
        }

        private void StartPresentation()
        {
            KillPresentation();

            if (_audioClipPlayer != null)
            {
                _audioClipPlayer.PlayOneShot(PowerUpConstants.SFX_NUKE_BLAST);
            }

            if (_blastVfxPlayer != null)
            {
                _blastVfxPlayer.Play(new VFXPlayConfig());
            }

            _presentationSequence = DOTween.Sequence();

            if (_blastVolume != null)
            {
                _blastVolume.weight = 0f;
                _presentationSequence.Append(DOTween.To(
                    () => _blastVolume.weight,
                    weight => _blastVolume.weight = weight,
                    1f,
                    _config.FlashInDuration).SetEase(Ease.OutQuad));

                _presentationSequence.Append(DOTween.To(
                    () => _blastVolume.weight,
                    weight => _blastVolume.weight = weight,
                    0f,
                    _config.BlastTotalDuration - _config.FlashInDuration).SetEase(Ease.InQuad));
            }

            if (_dirtOverlayPresenter != null)
            {
                _dirtOverlayPresenter.ResetOverlay();
                Sequence overlaySequence = DOTween.Sequence();

                overlaySequence.Append(DOTween.To(
                    () => _dirtOverlayPresenter.CanvasGroup.alpha,
                    alpha => _dirtOverlayPresenter.SetAlpha(alpha),
                    _config.DirtOverlayPeakAlpha,
                    _config.FlashInDuration).SetEase(Ease.OutQuad));

                overlaySequence.Append(DOTween.To(
                    () => _dirtOverlayPresenter.CanvasGroup.alpha,
                    alpha => _dirtOverlayPresenter.SetAlpha(alpha),
                    0f,
                    _config.BlastTotalDuration - _config.FlashInDuration).SetEase(Ease.InQuad));

                _presentationSequence.Join(overlaySequence);
            }
        }

        private void KillPresentation()
        {
            if (_presentationSequence != null && _presentationSequence.IsActive())
            {
                _presentationSequence.Kill();
            }

            _presentationSequence = null;

            if (_blastVolume != null)
            {
                _blastVolume.weight = 0f;
            }

            if (_dirtOverlayPresenter != null)
            {
                _dirtOverlayPresenter.ResetOverlay();
            }
        }
    }
}
