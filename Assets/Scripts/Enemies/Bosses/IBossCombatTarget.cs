using Assets.Scripts.HealthSystem;
using Assets.Scripts.StatusEffects;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses
{
    public interface IBossCombatTarget : IBossHUDSource, IDamageable
    {
        Transform Transform { get; }
    }
}
