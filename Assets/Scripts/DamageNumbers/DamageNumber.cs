using System;
using Assets.Scripts.DamageNumbers.Constants;
using Assets.Scripts.Initializers;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.DamageNumbers
{
    public struct DamageNumberConfig
    {
        public float Damage;
        public DamageNumberApearance DamagePopupApearance;

        public DamageNumberConfig(float damage, DamageNumberApearance damagePopupApearance)
        {
            Damage = damage;
            DamagePopupApearance = damagePopupApearance;
        }
    }

    public class DamageNumber : MonoBehaviour, IInitializable<DamageNumberConfig>
    {
        [SerializeField] private TextMeshPro _textMeshPro;

        private bool _isInitialized;
        private DamageNumberConfig _config;
        private Vector3 _startPosition;
        private Vector3 _targetPosition;
        private Vector3 _targetScale;
        private float _duration;
        private float _elapsedTime;
        private bool _isAnimating;

        public event EventHandler OnLifeEnd;

        private void Update()
        {
            if (!_isAnimating)
            {
                return;
            }

            _elapsedTime += Time.deltaTime;
            float duration = Mathf.Max(0.0001f, _duration);
            float t = Mathf.Clamp01(_elapsedTime / duration);

            float positionT = -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
            transform.position = Vector3.Lerp(_startPosition, _targetPosition, positionT);

            transform.localScale = _targetScale * Mathf.Sin(t * Mathf.PI);

            if (t >= 1f)
            {
                _isAnimating = false;
                transform.position = _targetPosition;
                transform.localScale = Vector3.zero;
                OnLifeEnd?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnDisable()
        {
            _isAnimating = false;
        }

        public void Initialize(DamageNumberConfig config)
        {
            Initialize(config, transform.position, transform.position, DamageNumberConstants.RESIZING_ANIMATION_SPEED * 2f);
        }

        public void Initialize(DamageNumberConfig config, Vector3 startPos, Vector3 targetPos, float duration)
        {
            _config = config;
            _startPosition = startPos;
            _targetPosition = targetPos;
            _duration = duration;
            _elapsedTime = 0f;

            SetTextApearance(config);

            var (_, growScaleMultiplier, _) = _config.DamagePopupApearance;
            _targetScale = Vector3.one * growScaleMultiplier;

            transform.position = startPos;
            transform.localScale = Vector3.zero;
            _isAnimating = true;
            _isInitialized = true;
        }

        public bool IsInitialized()
        {
            return _isInitialized;
        }

        private void SetTextApearance(DamageNumberConfig config)
        {
            var (fontSize, _, color) = config.DamagePopupApearance;
            _textMeshPro.SetText("{0:0}", config.Damage);
            _textMeshPro.color = color;
            _textMeshPro.fontSize = fontSize;
        }
    }
}

