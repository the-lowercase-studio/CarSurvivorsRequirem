using DG.Tweening;
using UnityEngine;

namespace Assets.Scripts.Enemies.Base
{
    public interface IMovementController
    {
        public float GetCurrentMovementSpeed();

        public bool IsOnGround();

        public void ResetVerticalVelocity();

        public Tween MoveToPositionInTimeIgnoringSpeed(Vector3 pos, float time);
    }
}
