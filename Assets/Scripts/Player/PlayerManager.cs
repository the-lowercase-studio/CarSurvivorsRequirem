using Assets.Scripts.Audio;
using Assets.Scripts.Player.Car;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.LevelSystem;
using Assets.Scripts.Providers;
using Assets.Scripts.Skills;
using UnityEngine;

namespace Assets.Scripts.Player
{
    public interface IPlayerManager : IHealthy, IGameObjectProvider
    {
        IAudioClipPlayer AudioClipPlayer { get; }
        ICarController CarController { get; }
        ILevelController LevelController { get; }
        ISkillsRegistry SkillsRegistry { get; }
    }

    [RequireComponent(typeof(RegenativeHealth), typeof(LevelController))]
    public class PlayerManager : MonoBehaviour, IPlayerManager
    {
        public IHealth Health { get; private set; }
        public ILevelController LevelController { get; private set; }
        public ISkillsRegistry SkillsRegistry { get; private set; }
        public ICarController CarController { get; private set; }
        public IAudioClipPlayer AudioClipPlayer { get; private set; }

        public GameObject GameObject => gameObject;

        private void Awake()
        {
            Health = GetComponent<IHealth>();
            LevelController = GetComponent<ILevelController>();
            SkillsRegistry = GetComponentInChildren<ISkillsRegistry>();
            CarController = GetComponent<ICarController>();
            AudioClipPlayer = GetComponentInChildren<IAudioClipPlayer>();
        }
    }
}
