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

        private void SetMaxRotationTween()
        {
            Tuple<bool, bool, bool> rotate = new Tuple<bool, bool, bool>(_rotateX, _rotateY, _rotateZ);

            _maxTweenRotation = rotate switch
            {
                (true, false, false) => new Vector3(_maxRotationOnAxis, 0, 0),
                (false, true, false) => new Vector3(0, _maxRotationOnAxis, 0),
                (false, false, true) => new Vector3(0, 0, _maxRotationOnAxis),
                (true, true, false) => new Vector3(_maxRotationOnAxis, _maxRotationOnAxis, 0),
                (true, false, true) => new Vector3(_maxRotationOnAxis, 0, _maxRotationOnAxis),
                (false, true, true) => new Vector3(0, _maxRotationOnAxis, _maxRotationOnAxis),
                _ => new Vector3(_maxRotationOnAxis, _maxRotationOnAxis, _maxRotationOnAxis)
            };
        }
    }
}
