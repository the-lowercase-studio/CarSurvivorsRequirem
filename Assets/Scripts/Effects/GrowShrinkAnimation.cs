using UnityEngine;

namespace Assets.Scripts.Effects
{
    public class GrowShrinkAnimation : MonoBehaviour
    {
        [SerializeField] private float _animationScaleMultiplier = 1f;
        [SerializeField] private float _iterationDuration = 1f;

        private Vector3 _startScale = Vector3.one;

        private void OnEnable()
        {
            _startScale = transform.localScale;
        }

        private void Update()
        {
            if (_iterationDuration <= 0f)
            {
                return;
            }

            float wave = (Mathf.Sin((Time.time * Mathf.PI / _iterationDuration) - (Mathf.PI * 0.5f)) + 1f) * 0.5f;
            transform.localScale = Vector3.Lerp(_startScale, _startScale * _animationScaleMultiplier, wave);
        }

        private void OnDisable()
        {
            transform.localScale = _startScale;
        }
    }
}
