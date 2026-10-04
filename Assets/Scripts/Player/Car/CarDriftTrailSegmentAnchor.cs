using UnityEngine;

namespace Assets.Scripts.Player.Car
{
    public sealed class CarDriftTrailSegmentAnchor : MonoBehaviour
    {
        private TrailRenderer[] _renderers;
        private Vector3[] _worldPositions;
        private bool[] _hasPositions;

        public void Initialize(TrailRenderer[] renderers)
        {
            _renderers = renderers;
            _worldPositions = new Vector3[renderers.Length];
            _hasPositions = new bool[renderers.Length];
        }

        public void SetPosition(int index, Vector3 supportedPoint)
        {
            _worldPositions[index] = supportedPoint;
            _hasPositions[index] = true;
            _renderers[index].transform.position = supportedPoint;
        }

        public void Release(int index)
        {
            _hasPositions[index] = false;
        }

        private void LateUpdate()
        {
            // Keep native live heads fixed even while the owning VFX controller is disabled.
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_hasPositions[i] && _renderers[i] != null)
                {
                    _renderers[i].transform.position = _worldPositions[i];
                }
            }
        }
    }
}
