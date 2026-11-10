using System;
using Assets.Scripts.Enemies.Bosses;
using Assets.Scripts.HealthSystem;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.HUD
{
    public interface IBossHUDPresenter
    {
        void Show(IBossHUDSource source, string bossName);
        void Hide();
    }

    public class BossHUDPresenter : MonoBehaviour, IBossHUDPresenter
    {
        [SerializeField] private GameObject _visual;
        [SerializeField] private BossHealthBarView _towerHealthBar;
        [SerializeField] private BossHealthBarView _golemHealthBar;
        [SerializeField] private TextMeshProUGUI _bossTitleText;
        [SerializeField] private float _healthTweenDuration = 0.2f;

        private IBossHUDSource _activeSource;
        private IHealth _activeHealth;
        private BossHealthBarView _activeView;
        private Tweener _healthTween;
        private float _displayedRatio;
        private float _targetRatio;
        private float _threshold;
        private bool _isEnraged;
        private int _bindingGeneration;
        private EventHandler _healthChangedHandler;
        private EventHandler _noHealthHandler;
        private Action _presentationChangedHandler;

        private void Awake()
        {
            if (_visual == null || _towerHealthBar == null || _golemHealthBar == null
                || _towerHealthBar == _golemHealthBar || _bossTitleText == null || _healthTweenDuration <= 0f)
            {
                throw new InvalidOperationException(name + ": assign both boss health layouts, Visual, title and a positive tween duration.");
            }
            _towerHealthBar.ValidateLayout(BossHealthBarKind.Tower);
            _golemHealthBar.ValidateLayout(BossHealthBarKind.Golem);
            _bossTitleText.raycastTarget = false;
            Hide();
        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnDestroy()
        {
            Hide();
        }

        public void Show(IBossHUDSource source, string bossName)
        {
            Hide();
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (!isActiveAndEnabled || !source.IsActive || source.Health == null || !source.Health.IsAlive())
            {
                return;
            }
            ValidateSource(source);
            switch (source.HealthBarKind)
            {
                case BossHealthBarKind.Tower:
                    _activeView = _towerHealthBar;
                    break;
                case BossHealthBarKind.Golem:
                    _activeView = _golemHealthBar;
                    break;
                default:
                    throw new InvalidOperationException("Unsupported boss health bar kind.");
            }
            _activeSource = source;
            _activeHealth = source.Health;
            _threshold = source.EnrageHealthPercent;
            _isEnraged = source.IsEnraged;
            _targetRatio = Mathf.Clamp01(_activeHealth.CurrentHealth / _activeHealth.MaxHealth);
            _displayedRatio = _targetRatio;
            _bossTitleText.text = bossName;
            int generation = _bindingGeneration;
            _healthChangedHandler = (sender, args) => { RefreshBinding(source, generation); };
            _noHealthHandler = (sender, args) => { RefreshBinding(source, generation); };
            _presentationChangedHandler = () => { RefreshBinding(source, generation); };
            _activeHealth.OnHealthChanged += _healthChangedHandler;
            _activeHealth.OnNoHealth += _noHealthHandler;
            source.OnPresentationChanged += _presentationChangedHandler;
            Render();
            _activeView.gameObject.SetActive(true);
            _visual.SetActive(true);
            _activeView.SetEnraged(_isEnraged);
        }

        public void Hide()
        {
            _bindingGeneration++;
            if (_activeSource != null)
            {
                _activeHealth.OnHealthChanged -= _healthChangedHandler;
                _activeHealth.OnNoHealth -= _noHealthHandler;
                _activeSource.OnPresentationChanged -= _presentationChangedHandler;
            }
            _activeSource = null;
            _activeHealth = null;
            _activeView = null;
            _healthChangedHandler = null;
            _noHealthHandler = null;
            _presentationChangedHandler = null;
            KillHealthTween();
            // Cleanup also runs when an incompletely authored component is destroyed in Edit Mode.
            if (_towerHealthBar != null) _towerHealthBar.ResetView();
            if (_golemHealthBar != null) _golemHealthBar.ResetView();
            if (_visual != null) _visual.SetActive(false);
        }

        private void RefreshBinding(IBossHUDSource source, int generation)
        {
            if (generation != _bindingGeneration || !ReferenceEquals(source, _activeSource))
            {
                return;
            }
            if (!source.IsActive || !_activeHealth.IsAlive() || _activeHealth.CurrentHealth <= 0f)
            {
                Hide();
                return;
            }
            ValidateSource(source);
            _threshold = source.EnrageHealthPercent;
            _isEnraged = source.IsEnraged;
            float ratio = Mathf.Clamp01(_activeHealth.CurrentHealth / _activeHealth.MaxHealth);
            _activeView.SetEnraged(_isEnraged);
            Render();
            if (ratio == _targetRatio)
            {
                return;
            }
            _targetRatio = ratio;
            if (_healthTween == null)
            {
                _healthTween = DOTween.To(GetDisplayedRatio, SetDisplayedRatio, ratio, _healthTweenDuration)
                    .SetEase(Ease.OutQuad).SetUpdate(false).SetAutoKill(false);
            }
            else
            {
                _healthTween.ChangeEndValue(ratio, _healthTweenDuration, true).Restart();
            }
        }

        private float GetDisplayedRatio()
        {
            return _displayedRatio;
        }

        private void SetDisplayedRatio(float value)
        {
            _displayedRatio = value;
            Render();
        }

        private void Render()
        {
            _activeView.Render(_displayedRatio, _threshold, _isEnraged);
        }

        private void KillHealthTween()
        {
            if (_healthTween != null)
            {
                _healthTween.Kill();
                _healthTween = null;
            }
        }

        private void ValidateSource(IBossHUDSource source)
        {
            float threshold = source.EnrageHealthPercent;
            float maxHealth = source.Health.MaxHealth;
            if (!(threshold > 0f && threshold < 1f) || !(maxHealth > 0f) || float.IsInfinity(maxHealth)
                || float.IsNaN(source.Health.CurrentHealth) || float.IsInfinity(source.Health.CurrentHealth))
            {
                Hide();
                throw new InvalidOperationException("Boss HUD requires finite health, positive maximum health and an enrage threshold strictly between zero and one.");
            }
        }
    }
}
