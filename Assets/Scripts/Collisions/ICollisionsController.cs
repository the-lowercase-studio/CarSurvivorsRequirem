using System;

namespace Assets.Scripts.Collisions
{
    public interface ICollisionsController
    {
        event EventHandler<CollisionEventArgs> OnCollisionWithOtherEnemy;

        event EventHandler<CollisionEventArgs> OnCollisionWithPlayer;
    }
}
