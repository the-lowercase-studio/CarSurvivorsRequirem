using Assets.Scripts.Providers;
using System;

namespace Assets.Scripts.Skills.ObjectsImpactingSkills.Crate
{
    public interface ICollectible : IGameObjectProvider
    {
        public event EventHandler OnCollected;
    }
}
