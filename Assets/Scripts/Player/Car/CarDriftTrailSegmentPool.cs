using Assets.Scripts.Player.Constants;
using UnityEngine;

namespace Assets.Scripts.Player.Car
{
    public sealed class CarDriftTrailSegmentPool
    {
        private enum SegmentState
        {
            Free,
            Active,
            Fading
        }

        private readonly TrailRenderer[] _renderers;
        private readonly SegmentState[] _states;
        private readonly float[] _retirementDeadlines;
        private readonly float _minVertexDistance;
        private readonly CarDriftTrailSegmentAnchor _anchor;
        private int _activeIndex = -1;
        private Vector3 _lastAppendedPoint;

        public CarDriftTrailSegmentPool(TrailRenderer template)
        {
            int capacity = CarVfxConstants.DRIFT_SEGMENTS_PER_EMITTER;
            _renderers = new TrailRenderer[capacity];
            _states = new SegmentState[capacity];
            _retirementDeadlines = new float[capacity];
            _minVertexDistance = template.minVertexDistance;
            template.emitting = false;
            template.autodestruct = false;
            template.Clear();
            template.enabled = false;
            _renderers[0] = template;

            for (int i = 1; i < capacity; i++)
            {
                // The authored emitter contains only Transform and TrailRenderer.
                TrailRenderer renderer = Object.Instantiate(template, template.transform.parent, false);
                renderer.name = template.name + " Drift Segment " + i;
                renderer.emitting = false;
                renderer.autodestruct = false;
                renderer.Clear();
                renderer.enabled = false;
                _renderers[i] = renderer;
            }

            // Suppress native sampling with a finite value to avoid mesh arithmetic overflow.
            // AppendPoint still filters vertices using the cached authored spacing.
            for (int i = 0; i < capacity; i++)
            {
                _renderers[i].minVertexDistance = CarVfxConstants.DRIFT_NATIVE_SAMPLE_SUPPRESSION_DISTANCE;
            }

            GameObject anchorOwner = template.transform.parent == null ? template.gameObject : template.transform.parent.gameObject;
            _anchor = anchorOwner.AddComponent<CarDriftTrailSegmentAnchor>();
            _anchor.Initialize(_renderers);
        }

        public bool TryBeginSegment(Vector3 supportedPoint)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_states[i] != SegmentState.Free)
                {
                    continue;
                }

                TrailRenderer renderer = _renderers[i];
                _anchor.SetPosition(i, supportedPoint);
                renderer.emitting = true;
                renderer.Clear();
                _states[i] = SegmentState.Active;
                _activeIndex = i;
                renderer.AddPosition(supportedPoint);
                renderer.enabled = true;
                _lastAppendedPoint = supportedPoint;
                return true;
            }

            return false;
        }

        public void AppendPoint(Vector3 supportedPoint)
        {
            TrailRenderer renderer = _renderers[_activeIndex];
            // Unity renders a live head at the emitter transform, independently of stored points.
            _anchor.SetPosition(_activeIndex, supportedPoint);
            if (renderer.positionCount > 0
                && (supportedPoint - _lastAppendedPoint).sqrMagnitude < _minVertexDistance * _minVertexDistance)
            {
                return;
            }

            renderer.AddPosition(supportedPoint);
            renderer.enabled = true;
            _lastAppendedPoint = supportedPoint;
        }

        public void EndSegment(float scaledTime)
        {
            if (_activeIndex < 0)
            {
                return;
            }

            _states[_activeIndex] = SegmentState.Fading;
            _renderers[_activeIndex].emitting = false;
            _retirementDeadlines[_activeIndex] = scaledTime + _renderers[_activeIndex].time;
            _activeIndex = -1;
        }

        public void TickExpiry(float scaledTime)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                TrailRenderer renderer = _renderers[i];
                if (_states[i] == SegmentState.Free)
                {
                    continue;
                }

                // Culled trails can retain expired native points until rendered again.
                // Retiring later than the last append makes this deadline conservative.
                if (_states[i] == SegmentState.Fading && scaledTime > _retirementDeadlines[i])
                {
                    renderer.Clear();
                    renderer.enabled = false;
                    _states[i] = SegmentState.Free;
                    _anchor.Release(i);
                }
                else if (renderer.positionCount == 0)
                {
                    renderer.enabled = false;
                }
            }
        }

        public void Dispose()
        {
            _activeIndex = -1;
            if (_anchor != null)
            {
                Object.Destroy(_anchor);
            }
            for (int i = 1; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    Object.Destroy(_renderers[i].gameObject);
                }
            }
        }
    }
}
