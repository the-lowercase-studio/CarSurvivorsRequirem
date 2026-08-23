using Assets.Scripts.Audio;
using Assets.Scripts.Enemies.Bosses.Golem.Animation;
using Assets.Scripts.Enemies.Bosses.Golem.Arms;
using Assets.Scripts.Enemies.Bosses.Golem.Config;
using Assets.Scripts.Enemies.Bosses.Golem.Movement;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Indicators;
using Assets.Scripts.Navigation.GridSystem;
using UnityEngine;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Enemies.Bosses.Golem
{
    public interface IGolemBoss : IHealthy
    {
        GolemBossConfigSO Config { get; }
        IGolemMovementController Movement { get; }
        IGolemArmSocketController Arms { get; }
        IGolemAnimator Animator { get; }
        IAudioClipPlayer AudioClipPlayer { get; }
        CircularTelegraphIndicator CircularTelegraph { get; }
        RectangularTelegraphIndicator RectangularTelegraph { get; }
        Vector3 PlayerPosition { get; }
        float DistanceToPlayer { get; }
        Vector3 DirectionToPlayer { get; }
        int CurrentPhase { get; }
        bool IsEnraged { get; }
        float CurrentCooldownMultiplier { get; }
        float CurrentSpeedMultiplier { get; }
        float CurrentArmSpeedMultiplier { get; }
        Grid WorldGrid { get; }
        Transform Transform { get; }
        void TriggerStompDamage();
    }
}
