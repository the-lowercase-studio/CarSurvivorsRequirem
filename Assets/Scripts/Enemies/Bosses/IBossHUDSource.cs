using System;
using Assets.Scripts.HealthSystem;

namespace Assets.Scripts.Enemies.Bosses
{
    public enum BossHealthBarKind
    {
        Tower,
        Golem
    }

    public interface IBossHUDSource : IHealthy
    {
        BossHealthBarKind HealthBarKind { get; }
        float EnrageHealthPercent { get; }
        bool IsEnraged { get; }
        bool IsActive { get; }
        event Action OnPresentationChanged;
    }
}
