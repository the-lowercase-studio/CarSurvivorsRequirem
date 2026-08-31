using System;
using System.Collections.Generic;
using Assets.Scripts.Audio;
using Assets.Scripts.Audio.Constants;
using Assets.Scripts.Common.Types;
using Assets.Scripts.Extensions;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.Navigation.FlowFieldSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Pooling;
using Assets.Scripts.Providers;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.LevelSystem.Exp
{
    public interface IExpParticle : IGameObjectProvider
    {
        public event EventHandler OnExpReachedTarget;

        public void SetSizeAndMaterialBasedOnExpAmount(float exp);

        public void CollectExp(Action callback);
    }

    [RequireComponent(typeof(FlowFieldMovementController))]
    public class ExpParticle : MonoBehaviour, IExpParticle, IPoolable
    {
        [Serializable]
        private struct ExpParticleApearanceByTreshold
        {
            [SerializeField] private float _treshold;
            [SerializeField] private Material _material;
            [SerializeField] private FloatValueRange _scaleValueRange;

            public float Treshold
            {
                get
                {
                    return _treshold;
                }
            }

            public Material Material
            {
                get
                {
                    return _material;
                }
            }

            public FloatValueRange ScaleValueRange
            {
                get
                {
                    return _scaleValueRange;
                }
            }
        }

        [Inject] private readonly IPlayerManager _playerManager = null;

        [SerializeField] private float _movementSpeed;
        [SerializeField] private float _disapearingDuration = 0.1f;
        [SerializeField] private GameObject _visual;
        [SerializeField] private MeshRenderer _meshRenderer;
        [SerializeField] private ExpParticleApearanceByTreshold[] _particleApearanceByTreshold;

        private float _expAmount;
        private bool _expCollected;
        private Vector3 _startScale;
        private IFlowFieldMovementController _flowFieldMovementController;
        private IAudioClipPlayer _audioClipPlayer;
        private bool _isCollecting;
        private float _collectElapsed;
        private Vector3 _collectStartScale;
        private Action _pendingCallback;

        public GameObject GameObject
        {
            get
            {
                return gameObject;
            }
        }

        public event EventHandler OnExpReachedTarget;
        public event EventHandler OnCanBeReleased;

        private void Awake()
        {
            _flowFieldMovementController = GetComponent<IFlowFieldMovementController>();
            _audioClipPlayer = GetComponentInChildren<IAudioClipPlayer>();
        }

        private void Start()
        {
            _startScale = transform.localScale;
        }

        private void Update()
        {
            if (_isCollecting)
            {
                _collectElapsed += Time.deltaTime;
                float duration = Mathf.Max(0.0001f, _disapearingDuration);
                float t = Mathf.Clamp01(_collectElapsed / duration);
                transform.localScale = Vector3.Lerp(_collectStartScale, Vector3.zero, t);

                if (t >= 1f)
                {
                    _isCollecting = false;
                    transform.localScale = Vector3.zero;
                    HandleCollectShrinkComplete();
                }
            }
        }

        private void FixedUpdate()
        {
            if (!_isCollecting)
            {
                _flowFieldMovementController.MoveOnFlowFieldGrid(_movementSpeed);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_expCollected && (1 << other.gameObject.layer) == EntityLayers.Player)
            {
                OnExpReachedTarget?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ReturnToPool()
        {
            OnRelease();

            OnCanBeReleased?.Invoke(this, EventArgs.Empty);
        }

        public void OnGet()
        {
            _expCollected = false;
            _isCollecting = false;
            _collectElapsed = 0f;
            _expAmount = 0;
            _pendingCallback = null;
        }

        public void OnRelease()
        {
            _isCollecting = false;
            _collectElapsed = 0f;
            _pendingCallback = null;
            transform.localScale = _startScale;
        }

        public void SetSizeAndMaterialBasedOnExpAmount(float exp)
        {
            if (_particleApearanceByTreshold.Length == 0)
            {
                Debug.LogError("Particle apearance by treshold is not set set for: " + transform.name);
            }

            _expAmount = exp;

            for (int i = _particleApearanceByTreshold.Length - 1; i >= 0; i--)
            {
                if (_particleApearanceByTreshold[i].Treshold <= exp)
                {
                    _meshRenderer.sharedMaterial = _particleApearanceByTreshold[i].Material;

                    transform.localScale =
                        Vector3.one * _particleApearanceByTreshold[i].ScaleValueRange.GetRandomValueInRange();

                    break;
                }
            }
        }

        public void CollectExp(Action callback = null)
        {
            if (_expCollected)
            {
                return;
            }

            _expCollected = true;
            _pendingCallback = callback;
            _isCollecting = true;
            _collectElapsed = 0f;
            _collectStartScale = transform.localScale;

            if (_audioClipPlayer != null)
            {
                _audioClipPlayer.Play(AudioConstants.EXP_COLLECTED_CLIP_NAME);
            }
        }

        private void HandleCollectShrinkComplete()
        {
            _playerManager.LevelController.AddExp(_expAmount);

            Action cb = _pendingCallback;
            _pendingCallback = null;

            OnCanBeReleased?.Invoke(this, EventArgs.Empty);
            cb?.Invoke();
        }
    }
}

