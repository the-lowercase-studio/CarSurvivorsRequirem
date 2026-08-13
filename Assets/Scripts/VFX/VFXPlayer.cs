using System;
using UnityEngine;

namespace Assets.Scripts.VFX
{
    public class VFXPlayConfig
    {
        public bool DestroyOnEnd;
        public float Scale;
        public float SimulationSpeed;

        public VFXPlayConfig(float scale = 1f, float simulationSpeed = 1f, bool destroyOnEnd = false)
        {
            Scale = scale;
            SimulationSpeed = simulationSpeed;
            DestroyOnEnd = destroyOnEnd;
        }
    }

    public interface IVFXPlayer
    {
        public void Play(VFXPlayConfig config);

        public float GetLongestParticleDuration();

        public event EventHandler OnVFXFinished;
    }

    public class VFXPlayer : MonoBehaviour, IVFXPlayer
    {
        private ParticleSystem[] _particleSystems;
        private bool _particlesStartedPlaying;
        private float _longestParticleDuration;
        private VFXPlayConfig _vfxPlayConfig;

        public event EventHandler OnVFXFinished;

        private void Awake()
        {
            _particleSystems = GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        }

        public float GetLongestParticleDuration()
        {
            return _longestParticleDuration;
        }

        public void Play(VFXPlayConfig config)
        {
            _vfxPlayConfig = config;

            _longestParticleDuration = 0;
            foreach (ParticleSystem particleSystem in _particleSystems)
            {
                if (particleSystem is null)
                {
                    continue;
                }

                particleSystem.transform.localScale = Vector3.one * config.Scale;

                ParticleSystem.MainModule main = particleSystem.main;
                main.simulationSpeed = config.SimulationSpeed;

                float currentDuration = main.duration;
                if (main.duration > _longestParticleDuration)
                {
                    _longestParticleDuration = currentDuration;
                }

                _particlesStartedPlaying = true;
                particleSystem.Play();
            }

            CallParticlesFinishAfterDelayOrWithout();
        }

        private void CallParticlesFinishAfterDelayOrWithout()
        {
            if (_particlesStartedPlaying)
            {
                if (IsInvoking(nameof(OnAllParticlesFinished)))
                {
                    CancelInvoke(nameof(OnAllParticlesFinished));
                }

                InvokeRepeating(nameof(OnAllParticlesFinished), _longestParticleDuration, _longestParticleDuration);
            }
            else
            {
                OnAllParticlesFinished();
            }
        }

        private void OnAllParticlesFinished()
        {
            if (!_particlesStartedPlaying)
            {
                return;
            }

            OnVFXFinished?.Invoke(this, EventArgs.Empty);
            _particlesStartedPlaying = false;

            if (_vfxPlayConfig.DestroyOnEnd)
            {
                Destroy(gameObject);
            }
        }
    }
}

