using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Golem.Animation
{
    public class GolemAnimationEventsForwarder : MonoBehaviour
    {
        [SerializeField] private GolemAnimator _golemAnimator;

        public void Call_OnLinearFistRelease()
        {
            _golemAnimator.Call_OnLinearFistRelease();
        }

        public void Call_OnSkyBarrageRelease()
        {
            _golemAnimator.Call_OnSkyBarrageRelease();
        }

        public void Call_OnLeapTakeoffComplete()
        {
            _golemAnimator.Call_OnLeapTakeoffComplete();
        }

        public void Call_OnLeapLandComplete()
        {
            _golemAnimator.Call_OnLeapLandComplete();
        }

        public void Call_OnStompImpact()
        {
            _golemAnimator.Call_OnStompImpact();
        }
    }
}
