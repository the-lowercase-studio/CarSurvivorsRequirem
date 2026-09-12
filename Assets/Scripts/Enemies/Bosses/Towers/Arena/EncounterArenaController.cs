using System;
using DG.Tweening;
using Assets.Scripts.Cameras;
using Assets.Scripts.Enemies.Bosses.Towers.Arena.Constants;
using Assets.Scripts.Enemies.Bosses.Towers.MortarTower;
using Assets.Scripts.Enemies.Bosses.Towers.MortarTower.Constants;
using Assets.Scripts.Player;
using Assets.Scripts.Spawners.Swarm;
using Assets.Scripts.UI.HUD;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Towers.Arena
{
    public interface IEncounterArenaController
    {
        bool IsEncounterActive { get; }
        bool IsEncounterCompleted { get; }
        bool IsLeashCountdownActive { get; }
        float RemainingLeashSeconds { get; }
        float ArenaRadius { get; }
        event Action OnPlayerEnteredArena;
        event Action OnPlayerExitedArena;
        event Action<float> OnLeashCountdownTick;
        event Action OnEncounterReset;
        event Action OnEncounterCompleted;
    }

    public class EncounterArenaController : MonoBehaviour, IEncounterArenaController
    {
        [Inject] private readonly IPlayerManager _playerManager = null;
        [Inject] private readonly ICinemachineCombatFollowOffsetController _cameraController = null;
        [Inject] private readonly IArenaLeashWarningPresenter _leashWarningPresenter = null;
        [Inject] private readonly IBossHUDPresenter _bossHUDPresenter = null;
        [Inject] private readonly ISwarmFreezer _swarmFreezer = null;

        [Tooltip("Reference to the stationary mortar tower boss.")]
        [SerializeField] private MortarTowerBoss _boss;
        [Tooltip("Visual transform representing the expanding/pulsing circular arena border.")]
        [SerializeField] private Transform _perimeterBorder;
        [Tooltip("Title shown on the Boss HUD health bar.")]
        [SerializeField] private string _bossDisplayName = "MORTAR TOWER";
        [Tooltip("Radius of the circular combat boundary in meters.")]
        [SerializeField] private float _arenaRadius = MortarTowerConstants.DEFAULT_ARENA_RADIUS;
        [Tooltip("Grace period in seconds for returning to the arena before encounter resets.")]
        [SerializeField] private float _leashGracePeriodSeconds = MortarTowerConstants.DEFAULT_LEASH_TIME;

        private bool _isEncounterActive;
        private bool _isEncounterCompleted;
        private bool _isLeashCountdownActive;
        private float _remainingLeashSeconds;
        private Tween _perimeterExpandTween;
        private Tween _perimeterPulseTween;

        public event Action OnPlayerEnteredArena;
        public event Action OnPlayerExitedArena;
        public event Action<float> OnLeashCountdownTick;
        public event Action OnEncounterReset;
        public event Action OnEncounterCompleted;

        public bool IsEncounterActive
        {
            get
            {
                return _isEncounterActive;
            }
        }

        public bool IsEncounterCompleted
        {
            get
            {
                return _isEncounterCompleted;
            }
        }

        public bool IsLeashCountdownActive
        {
            get
            {
                return _isLeashCountdownActive;
            }
        }

        public float RemainingLeashSeconds
        {
            get
            {
                return _remainingLeashSeconds;
            }
        }

        public float ArenaRadius
        {
            get
            {
                return _arenaRadius;
            }
        }

        private void OnEnable()
        {
            if (_boss != null)
            {
                _boss.OnBossDefeated += Boss_OnBossDefeated;
            }

            _isEncounterActive = false;
            _isEncounterCompleted = false;
            _isLeashCountdownActive = false;
            _remainingLeashSeconds = _leashGracePeriodSeconds;

            if (_perimeterBorder != null)
            {
                _perimeterBorder.localScale = Vector3.zero;
            }
        }

        private void OnDisable()
        {
            if (_boss != null)
            {
                _boss.OnBossDefeated -= Boss_OnBossDefeated;
            }

            if (_isEncounterActive)
            {
                if (_swarmFreezer != null)
                {
                    _swarmFreezer.IsSuppressed = false;
                }

                if (_cameraController != null)
                {
                    _cameraController.RestoreDefaultOffset(0.2f);
                }

                if (_bossHUDPresenter != null)
                {
                    _bossHUDPresenter.Hide();
                }

                if (_leashWarningPresenter != null)
                {
                    _leashWarningPresenter.Hide();
                }
            }

            _isEncounterActive = false;
            _isLeashCountdownActive = false;
            KillPerimeterTweens();
        }

        private void OnDestroy()
        {
            if (_boss != null)
            {
                _boss.OnBossDefeated -= Boss_OnBossDefeated;
            }

            KillPerimeterTweens();
        }

        private void Update()
        {
            if (_isEncounterCompleted)
            {
                return;
            }

            if (_playerManager == null || _playerManager.GameObject == null)
            {
                return;
            }

            Vector3 playerPos = _playerManager.GameObject.transform.position;
            Vector3 diff = playerPos - transform.position;
            diff.y = 0f;

            float sqrDistance = diff.sqrMagnitude;
            float sqrRadius = _arenaRadius * _arenaRadius;
            bool isPlayerInside = sqrDistance <= sqrRadius;

            if (isPlayerInside)
            {
                if (!_isEncounterActive)
                {
                    StartCombatEncounter();
                }
                else if (_isLeashCountdownActive)
                {
                    _isLeashCountdownActive = false;
                    _remainingLeashSeconds = _leashGracePeriodSeconds;

                    if (_leashWarningPresenter != null)
                    {
                        _leashWarningPresenter.Hide();
                    }

                    OnPlayerEnteredArena?.Invoke();
                }
            }
            else
            {
                if (_isEncounterActive)
                {
                    if (!_isLeashCountdownActive)
                    {
                        _isLeashCountdownActive = true;
                        _remainingLeashSeconds = _leashGracePeriodSeconds;

                        if (_leashWarningPresenter != null)
                        {
                            _leashWarningPresenter.ShowWarning(_remainingLeashSeconds);
                        }

                        OnPlayerExitedArena?.Invoke();
                    }
                    else
                    {
                        _remainingLeashSeconds -= Time.deltaTime;

                        if (_leashWarningPresenter != null)
                        {
                            _leashWarningPresenter.UpdateCountdown(_remainingLeashSeconds);
                        }

                        OnLeashCountdownTick?.Invoke(_remainingLeashSeconds);

                        if (_remainingLeashSeconds <= 0f)
                        {
                            ResetCombatEncounter();
                        }
                    }
                }
            }
        }

        private void StartCombatEncounter()
        {
            _isEncounterActive = true;
            _isLeashCountdownActive = false;
            _remainingLeashSeconds = _leashGracePeriodSeconds;

            if (_boss != null)
            {
                _boss.StartEncounter();

                if (_bossHUDPresenter != null && _boss.Health != null)
                {
                    _bossHUDPresenter.Show(_boss.Health, _bossDisplayName);
                }

                if (_cameraController != null)
                {
                    Vector3 combatOffset = _boss.Config != null ? _boss.Config.CombatFollowOffset : MortarTowerConstants.COMBAT_FOLLOW_OFFSET;
                    float duration = _boss.Config != null ? _boss.Config.CameraTransitionDuration : MortarTowerConstants.CAMERA_TWEEN_DURATION;
                    _cameraController.TransitionToCombatOffset(combatOffset, duration);
                }
            }

            if (_swarmFreezer != null)
            {
                _swarmFreezer.IsSuppressed = true;
            }

            AnimatePerimeterExpansion();
            OnPlayerEnteredArena?.Invoke();
        }

        private void ResetCombatEncounter()
        {
            _isEncounterActive = false;
            _isLeashCountdownActive = false;
            _remainingLeashSeconds = _leashGracePeriodSeconds;

            if (_leashWarningPresenter != null)
            {
                _leashWarningPresenter.Hide();
            }

            if (_bossHUDPresenter != null)
            {
                _bossHUDPresenter.Hide();
            }

            if (_swarmFreezer != null)
            {
                _swarmFreezer.IsSuppressed = false;
            }

            if (_cameraController != null)
            {
                float duration = _boss != null && _boss.Config != null ? _boss.Config.CameraTransitionDuration : MortarTowerConstants.CAMERA_TWEEN_DURATION;
                _cameraController.RestoreDefaultOffset(duration);
            }

            AnimatePerimeterCollapse();

            if (_boss != null)
            {
                _boss.ResetEncounter();
            }

            OnEncounterReset?.Invoke();
        }

        private void Boss_OnBossDefeated(IMortarTowerBoss boss)
        {
            _isEncounterCompleted = true;
            _isEncounterActive = false;
            _isLeashCountdownActive = false;

            if (_leashWarningPresenter != null)
            {
                _leashWarningPresenter.Hide();
            }

            if (_bossHUDPresenter != null)
            {
                _bossHUDPresenter.Hide();
            }

            if (_swarmFreezer != null)
            {
                _swarmFreezer.IsSuppressed = false;
            }

            if (_cameraController != null)
            {
                float duration = _boss != null && _boss.Config != null ? _boss.Config.CameraTransitionDuration : MortarTowerConstants.CAMERA_TWEEN_DURATION;
                _cameraController.RestoreDefaultOffset(duration);
            }

            AnimatePerimeterCollapse();
            OnEncounterCompleted?.Invoke();
        }

        private void AnimatePerimeterExpansion()
        {
            KillPerimeterTweens();

            if (_perimeterBorder == null)
            {
                return;
            }

            float diameter = _arenaRadius * 2f;
            Vector3 baseScale = new Vector3(diameter, 1f, diameter);

            _perimeterBorder.localScale = Vector3.zero;
            _perimeterBorder.gameObject.SetActive(true);

            _perimeterExpandTween = _perimeterBorder.DOScale(baseScale, ArenaConstants.DEFAULT_EXPAND_DURATION)
                .SetEase(Ease.OutBack)
                .OnComplete(() =>
                {
                    Vector3 pulseScale = baseScale * (1f + ArenaConstants.DEFAULT_PULSE_SCALE_DELTA);
                    _perimeterPulseTween = _perimeterBorder.DOScale(pulseScale, ArenaConstants.DEFAULT_PULSE_DURATION)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetEase(Ease.InOutSine);
                });
        }

        private void AnimatePerimeterCollapse()
        {
            KillPerimeterTweens();

            if (_perimeterBorder == null)
            {
                return;
            }

            _perimeterExpandTween = _perimeterBorder.DOScale(Vector3.zero, ArenaConstants.DEFAULT_SHRINK_DURATION)
                .SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    if (_perimeterBorder != null)
                    {
                        _perimeterBorder.gameObject.SetActive(false);
                    }
                });
        }

        private void KillPerimeterTweens()
        {
            if (_perimeterExpandTween != null && _perimeterExpandTween.IsActive())
            {
                _perimeterExpandTween.Kill();
            }
            _perimeterExpandTween = null;

            if (_perimeterPulseTween != null && _perimeterPulseTween.IsActive())
            {
                _perimeterPulseTween.Kill();
            }
            _perimeterPulseTween = null;
        }
    }
}
