using UnityEngine;

namespace Assets.Scripts.StatusEffects
{
    internal interface IKnockable
    {
        public void ApplyKnockBack(Vector3 direction, float power, float timeToArriveAtLocation);
    }
}
