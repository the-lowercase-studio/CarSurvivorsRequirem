using UnityEngine;

namespace Assets.Scripts.VFX
{
    public class PooledExplosionEffect : MonoBehaviour
    {
        [SerializeField] private VFXPlayer _vfxPlayer;
        [SerializeField, Tooltip("All particle systems in the nested explosion, cached during authoring.")]
        private ParticleSystem[] _particleSystems;

        private readonly VFXPlayConfig _playConfig = new VFXPlayConfig();
        private bool _isPlaying;
        private float _startedAt;

        private void OnDisable()
        {
            StopPlayback();
        }

        public bool ValidateConfiguration()
        {
            if (_vfxPlayer == null || _particleSystems == null || _particleSystems.Length == 0)
            {
                Debug.LogError("Pooled explosion requires an authored VFXPlayer and particle references.", this);
                return false;
            }
            foreach (ParticleSystem particle in _particleSystems)
            {
                if (particle == null || particle.main.loop || particle.main.useUnscaledTime
                    || particle.main.stopAction != ParticleSystemStopAction.None || particle.main.playOnAwake)
                {
                    Debug.LogError("Pooled explosion particles must be assigned, non-looping, scaled, manually played, and have no stop action.", this);
                    return false;
                }
            }
            return true;
        }

        public void PreparePlayback(Vector3 position, float radius)
        {
            transform.SetPositionAndRotation(position, Quaternion.identity);
            transform.localScale = Vector3.one;
            _playConfig.Scale = radius;
            foreach (ParticleSystem particle in _particleSystems)
            {
                particle.transform.localScale = Vector3.one * radius;
            }
        }

        public void Play()
        {
            // Activate after placement; this also initializes the nested VFXPlayer's cache.
            gameObject.SetActive(true);
            _vfxPlayer.StopPlayback();
            _startedAt = Time.time;
            _isPlaying = true;
            _vfxPlayer.Play(_playConfig);
        }

        public bool HasFinished(float watchdogTimeout)
        {
            if (!_isPlaying || !gameObject.activeInHierarchy)
            {
                return true;
            }
            bool isAlive = false;
            foreach (ParticleSystem particle in _particleSystems)
            {
                isAlive |= particle.IsAlive(true);
            }
            if (!isAlive)
            {
                return true;
            }
            if (Time.time - _startedAt >= watchdogTimeout)
            {
                Debug.LogError("Pooled explosion exceeded its scaled watchdog; forcing one return.", this);
                return true;
            }
            return false;
        }

        public void StopPlayback()
        {
            if (!_isPlaying)
            {
                return;
            }
            _isPlaying = false;
            _vfxPlayer.StopPlayback();
            _startedAt = 0f;
        }
    }
}
