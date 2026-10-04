using UnityEngine;

namespace Assets.Scripts.Enemies.Base
{
    public interface IMovementController
    {
        bool CanBeTeleported { get; }
        void ResetAfterRelocation();
        public float GetCurrentMovementSpeed();

        public bool IsOnGround();

        public void ResetVerticalVelocity();

        public void MoveToPositionInTimeIgnoringSpeed(Vector3 pos, float time);
    }
}
