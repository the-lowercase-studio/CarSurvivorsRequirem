using System;
using UnityEngine;

namespace Assets.Scripts.Collisions
{
    public class CollisionEventArgs : EventArgs
    {
        public Collider Collider { get; }

        public CollisionEventArgs(Collider collider)
        {
            Collider = collider;
        }
    }
}
