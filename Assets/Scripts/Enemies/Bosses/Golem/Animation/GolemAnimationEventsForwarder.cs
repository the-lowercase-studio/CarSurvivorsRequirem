using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Golem.Animation
{
    public class GolemAnimationEventsForwarder : MonoBehaviour
    {
        [SerializeField] private GolemAnimator _golemAnimator;

        private void Awake()
        {
            EnsureAnimatorReference();
        }

        public void Call_OnLinearFistRelease()
        {
            EnsureAnimatorReference();
            _golemAnimator?.Call_OnLinearFistRelease();
        }

        public void Call_OnSkyBarrageRelease()
        {
            EnsureAnimatorReference();
            _golemAnimator?.Call_OnSkyBarrageRelease();
        }

        public void Call_OnLeapTakeoffComplete()
        {
            EnsureAnimatorReference();
            _golemAnimator?.Call_OnLeapTakeoffComplete();
        }

        public void Call_OnLeapLandComplete()
        {
            EnsureAnimatorReference();
            _golemAnimator?.Call_OnLeapLandComplete();
        }

        public void Call_OnStompImpact()
        {
            EnsureAnimatorReference();
            _golemAnimator?.Call_OnStompImpact();
        }

        private void EnsureAnimatorReference()
        {
            if (_golemAnimator == null)
            {
                _golemAnimator = GetComponent<GolemAnimator>() ?? GetComponentInParent<GolemAnimator>() ?? GetComponentInChildren<GolemAnimator>();
            }
        }
    }
}
