using Assets.Scripts.Extensions;
using DG.Tweening;
using System;
using UnityEngine;

namespace Assets.Scripts.Effects
{
    public class XYZRotationLoop : MonoBehaviour
    {
        [SerializeField] private bool _rotateX;
        [SerializeField] private bool _rotateY;
        [SerializeField] private bool _rotateZ;
        [SerializeField, Range(0, 360f)] private float _maxRotationOnAxis;
        [SerializeField] private bool _useLocalRotation;
        [SerializeField] private float _tweenIterationTime = 2.5f;
        [SerializeField] private bool _unscaleWithTime;
        private Vector3 _maxTweenRotation;
        private Tween _rotationTween;

        private void OnEnable()
        {
            SetMaxRotationTween();

            if (_rotationTween == null)
            {
                _rotationTween = _useLocalRotation
                    ? transform.StartLocalRotateLoopTween(_maxTweenRotation, _tweenIterationTime)
                    : transform.StartRotateLoopTween(_maxTweenRotation, _tweenIterationTime);

                _rotationTween.SetUpdate(_unscaleWithTime);
            }
            else
            {
                _rotationTween.Restart();
            }
        }

        private void OnDisable()
        {
            if (_rotationTween != null)
            {
                _rotationTween.Pause();
            }
        }

        private void OnDestroy()
        {
            if (_rotationTween != null)
            {
                _rotationTween.Kill();
                _rotationTween = null;
            }
        }

        private void SetMaxRotationTween()
        {
            _maxTweenRotation = new Vector3(
                _rotateX ? _maxRotationOnAxis : 0f,
                _rotateY ? _maxRotationOnAxis : 0f,
                _rotateZ ? _maxRotationOnAxis : 0f
            );
        }
    }
}
