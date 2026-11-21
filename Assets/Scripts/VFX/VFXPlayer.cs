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

        public float LastScheduledDuration { get; private set; }

        public event EventHandler OnVFXFinished;

        public bool HasParticleSystems
        {
            get
            {
                if (_particleSystems == null)
                {
                    _particleSystems = GetComponentsInChildren<ParticleSystem>(includeInactive: true);
                }
                return _particleSystems.Length > 0;
            }
        }

        private void Awake()
        {
            _particleSystems = GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        }

        public static float CalculateEffectiveDuration(float particleDuration, float simulationSpeed)
        {
            if (float.IsNaN(particleDuration) || float.IsInfinity(particleDuration))
            {
                return 0f;
            }

            float safeDuration = Mathf.Max(0f, particleDuration);
            float safeSpeed = simulationSpeed > 0f && !float.IsNaN(simulationSpeed) && !float.IsInfinity(simulationSpeed)
                ? Mathf.Max(0.01f, simulationSpeed)
                : 1f;
            return safeDuration / safeSpeed;
        }

        public float GetLongestParticleDuration()
        {
            return _longestParticleDuration;
        }

        public void Play(VFXPlayConfig config)
        {
            _vfxPlayConfig = config;

            if (_particleSystems == null)
            {
                _particleSystems = GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            }

            float scale = config != null ? config.Scale : 1f;
            float simulationSpeed = config != null && config.SimulationSpeed > 0f
                && !float.IsNaN(config.SimulationSpeed) && !float.IsInfinity(config.SimulationSpeed)
                ? Mathf.Max(0.01f, config.SimulationSpeed)
                : 1f;

            _longestParticleDuration = 0f;
            _particlesStartedPlaying = false;
            foreach (ParticleSystem particleSystem in _particleSystems)
            {
                if (particleSystem is null)
                {
                    continue;
                }

                particleSystem.transform.localScale = Vector3.one * scale;

                ParticleSystem.MainModule main = particleSystem.main;
                main.simulationSpeed = simulationSpeed;

                float currentDuration = CalculateEffectiveDuration(main.duration, simulationSpeed);
                if (currentDuration > _longestParticleDuration)
                {
                    _longestParticleDuration = currentDuration;
                }

                _particlesStartedPlaying = true;
                particleSystem.Play();
            }

            CallParticlesFinishAfterDelayOrWithout();
        }

        public void StopPlayback()
        {
            CancelInvoke(nameof(OnAllParticlesFinished));
            _particlesStartedPlaying = false;
            _longestParticleDuration = 0f;
            LastScheduledDuration = 0f;
            _vfxPlayConfig = null;
            if (_particleSystems == null)
            {
                return;
            }
            foreach (ParticleSystem particleSystem in _particleSystems)
            {
                if (particleSystem != null)
                {
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void CallParticlesFinishAfterDelayOrWithout()
        {
            if (_particlesStartedPlaying)
            {
                if (IsInvoking(nameof(OnAllParticlesFinished)))
                {
                    CancelInvoke(nameof(OnAllParticlesFinished));
                }

                LastScheduledDuration = _longestParticleDuration;
                Invoke(nameof(OnAllParticlesFinished), _longestParticleDuration);
            }
            else
            {
                LastScheduledDuration = 0f;
                FinishPlayback();
            }
        }

        private void OnAllParticlesFinished()
        {
            if (!_particlesStartedPlaying)
            {
                return;
            }

            _particlesStartedPlaying = false;
            FinishPlayback();
        }

        private void FinishPlayback()
        {
            OnVFXFinished?.Invoke(this, EventArgs.Empty);

            if (_vfxPlayConfig != null && _vfxPlayConfig.DestroyOnEnd)
            {
                Destroy(gameObject);
            }
        }
    }
}

