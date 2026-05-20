using Assets.Scripts.LevelSystem;
using Assets.Scripts.Player;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.Common.EventArgs;
using Reflex.Attributes;

namespace Assets.Scripts.UI.Level
{
    public interface IPlayerLevelPresenter
    {
        event EventHandler OnExpSliderVisualEndValueReached;
    }

    public class PlayerLevelPresenter : MonoBehaviour, IPlayerLevelPresenter
    {
        private const float BASE_EXP_INCREASE_ANIM_SPEED = 1f;
        private const float FASTEST_EXP_INCREASE_ANIM_SPEED = 0.6f;
        private const float DELAY_BETWEEN_TWEENS_ANIMATION_CHECK = 0.02f;

        [Inject] private readonly IPlayerManager _playerManager;

        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private Slider _expSlider;

        public event EventHandler OnExpSliderVisualEndValueReached;

        private ILevelController _playerLevelController;

        private LevelData _currentlyVisibleLevelData;
        private readonly Queue<ExpVisualEvent> _expVisualQueue = new();
        private ExpVisualEvent? _lastQueuedExpInSameLevelIncreaseEvent = null;

        private Coroutine _expIncreaseCoroutine;
        private Coroutine _levelUpCoroutine;

        private struct ExpVisualEvent
        {
            public LevelData LevelData;
            public bool IsLevelUp;

            public ExpVisualEvent(LevelData data, bool isLevelUp)
            {
                LevelData = data;
                IsLevelUp = isLevelUp;
            }
        }

        private void Start()
        {
            _playerLevelController = _playerManager.LevelController;

            _currentlyVisibleLevelData = _playerLevelController.LevelData;
            _expSlider.value = _currentlyVisibleLevelData.Exp;
            _expSlider.maxValue = _currentlyVisibleLevelData.MaxExp;

            _playerLevelController.OnExpChange += LevelController_OnExpChange;
            _playerLevelController.OnLvlUp += LevelController_OnLvlChange;

            InvokeRepeating(nameof(HandleTweensAnimations), 0, DELAY_BETWEEN_TWEENS_ANIMATION_CHECK);
        }

        private void LevelController_OnExpChange(object sender, ValueEventArgs<LevelData> e)
        {
            if (e.Value.Lvl == _currentlyVisibleLevelData.Lvl)
            {
                _lastQueuedExpInSameLevelIncreaseEvent = new ExpVisualEvent(e.Value, false);
            }
        }

        private void LevelController_OnLvlChange(object sender, ValueEventArgs<LevelData> e)
        {
            _expVisualQueue.Enqueue(new ExpVisualEvent(e.Value, true));
        }

        private void HandleTweensAnimations()
        {
            if (_expVisualQueue.Count > 0 && !IsPlayingLevelUpCoroutine())
            {
                KillExpIncreaseCoroutineIfPlaying();
                PlayLevelUpCoroutine();
            }
            else if (_lastQueuedExpInSameLevelIncreaseEvent.HasValue && CanPlayLastExpIncreaseCoroutine())
            {
                KillExpIncreaseCoroutineIfPlaying();
                PlayLastExpIncreaseCoroutine();
                _lastQueuedExpInSameLevelIncreaseEvent = null;
            }
        }

        private bool IsPlayingLevelUpCoroutine()
        {
            return _levelUpCoroutine != null;
        }

        private void KillExpIncreaseCoroutineIfPlaying()
        {
            if (_expIncreaseCoroutine != null)
            {
                StopCoroutine(_expIncreaseCoroutine);
                _expIncreaseCoroutine = null;
            }
        }

        private void PlayLevelUpCoroutine()
        {
            ExpVisualEvent expEvent = _expVisualQueue.Dequeue();
            LevelData nextLevelData = expEvent.LevelData;

            float startExp = _expSlider.value;
            float endExp = _currentlyVisibleLevelData.MaxExp;

            if (Mathf.Approximately(startExp, endExp))
            {
                HandleLevelUpTransition(nextLevelData);
                return;
            }

            _levelUpCoroutine = StartCoroutine(AnimateSliderExpGainCoroutine(
                startExp, endExp, CalculateSliderExpGainAnimSpeed(true), true, () =>
                {
                    _levelUpCoroutine = null;
                    HandleLevelUpTransition(nextLevelData);
                }));
        }

        private void HandleLevelUpTransition(LevelData nextLevelData)
        {
            _currentlyVisibleLevelData = nextLevelData;

            _expSlider.value = 0;
            _expSlider.maxValue = _currentlyVisibleLevelData.MaxExp;

            UpdateLevelText();

            OnExpSliderVisualEndValueReached?.Invoke(this, EventArgs.Empty);

            if (_expVisualQueue.Count > 0 && _expVisualQueue.Peek().IsLevelUp)
            {
                PlayLevelUpCoroutine();
            }
            else
            {
                KillExpIncreaseCoroutineIfPlaying();
                if (_lastQueuedExpInSameLevelIncreaseEvent.HasValue && _lastQueuedExpInSameLevelIncreaseEvent.Value.LevelData.Lvl == _currentlyVisibleLevelData.Lvl)
                {
                    PlayLastExpIncreaseCoroutine();
                    _lastQueuedExpInSameLevelIncreaseEvent = null;
                }
                else if (_currentlyVisibleLevelData.Exp > 0)
                {
                    _expIncreaseCoroutine = StartCoroutine(AnimateSliderExpGainCoroutine(
                        0, _currentlyVisibleLevelData.Exp, CalculateSliderExpGainAnimSpeed(false), false, null));
                }
            }
        }

        private void PlayLastExpIncreaseCoroutine()
        {
            if (_lastQueuedExpInSameLevelIncreaseEvent.HasValue)
            {
                var expEvent = _lastQueuedExpInSameLevelIncreaseEvent.Value;
                _expIncreaseCoroutine = StartCoroutine(AnimateSliderExpGainCoroutine(
                    _expSlider.value, expEvent.LevelData.Exp, CalculateSliderExpGainAnimSpeed(false), false, null));
                _currentlyVisibleLevelData = expEvent.LevelData;
            }
        }

        private bool CanPlayLastExpIncreaseCoroutine()
        {
            return !IsPlayingLevelUpCoroutine()
                && _lastQueuedExpInSameLevelIncreaseEvent.HasValue
                && _lastQueuedExpInSameLevelIncreaseEvent.Value.LevelData.Lvl == _currentlyVisibleLevelData.Lvl;
        }

        private IEnumerator AnimateSliderExpGainCoroutine(float from, float to, float duration, bool isLevelUp, Action onComplete)
        {
            float elapsed = 0f;
            float startValue = from;
            float endValue = to;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _expSlider.value = Mathf.Lerp(startValue, endValue, EaseUtils.EaseOutQuad(t));
                yield return null;
            }

            _expSlider.value = endValue;

            onComplete?.Invoke();
        }

        private float CalculateSliderExpGainAnimSpeed(bool isLevelUp)
        {
            if (isLevelUp)
            {
                int levelsToGo = _expVisualQueue.Count + 1;
                return Mathf.Min(FASTEST_EXP_INCREASE_ANIM_SPEED, BASE_EXP_INCREASE_ANIM_SPEED / Mathf.Max(1, levelsToGo));
            }
            else
            {
                return BASE_EXP_INCREASE_ANIM_SPEED;
            }
        }

        private void UpdateLevelText()
            => _levelText.text = $"{_currentlyVisibleLevelData.Lvl} Lvl";
    }
}
