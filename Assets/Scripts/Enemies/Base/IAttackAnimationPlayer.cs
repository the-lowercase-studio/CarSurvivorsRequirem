using System;

namespace Assets.Scripts.Enemies.Base
{
    public interface IAttackAnimationPlayer
    {
        public bool IsPlayingAttackAnimation { get; }

        public event EventHandler OnAttackAnimationStart;

        public event EventHandler OnAttackAnimationEnd;

        public event EventHandler OnAttackHitFrame;

        public void PlayAttackAnimation();
    }
}
