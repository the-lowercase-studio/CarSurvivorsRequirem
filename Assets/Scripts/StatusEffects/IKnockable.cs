using UnityEngine;

namespace Assets.Scripts.StatusEffects
{
    public interface IKnockable
    {
        void ApplyKnockBack(Vector3 direction, float power, float timeToArriveAtLocation);
    }
}

