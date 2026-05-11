using Assets.Scripts.Audio;
using Assets.Scripts.Common.Types;
using Assets.Scripts.Extensions;
using Assets.Scripts.Navigation.FlowFieldSystem;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.Player;
using Assets.Scripts.Pooling;
using Assets.Scripts.Providers;
using Reflex.Attributes;
using System;
using System.Collections.Generic;
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

            public float Treshold => _treshold;
            public Material Material => _material;
            public FloatValueRange ScaleValueRange => _scaleValueRange;
        }

        [Inject] private readonly IPlayerManager _playerManager;

        [SerializeField] private float _movementSpeed;
        [SerializeField] private float _disapearingDuration = 0.1f;

        [SerializeField] private GameObject _visual;
        [SerializeField] private ExpParticleApearanceByTreshold[] _particleApearanceByTreshold;

        public GameObject GameObject => gameObject;

        public event EventHandler OnExpReachedTarget;

        public event EventHandler OnCanBeReleased;

        private float _expAmount;
        private bool _expCollected;

        private Vector3 _startScale;

        private IFlowFieldMovementController _flowFieldMovementController;

        private IAudioClipPlayer _audioClipPlayer;

        private void Awake()
        {
            _flowFieldMovementController = GetComponent<IFlowFieldMovementController>();
            _audioClipPlayer = GetComponentInChildren<IAudioClipPlayer>();
        }

        private void FixedUpdate()
        {
            _flowFieldMovementController.MoveOnFlowFieldGrid(_movementSpeed);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_expCollected && (1 << other.gameObject.layer) == EntityLayers.Player)
            {
                OnExpReachedTarget?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Start()
        {
            _startScale = transform.localScale;
        }

        public void ReturnToPool()
        {
            OnRelease();

            OnCanBeReleased?.Invoke(this, EventArgs.Empty);
        }

        public void OnGet()
        {
            _expCollected = false;

            _expAmount = 0;
        }

        public void OnRelease()
        {
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
                    MeshRenderer meshRenderer = _visual.GetComponent<MeshRenderer>();
                    meshRenderer.SetMaterials(new List<Material>() { _particleApearanceByTreshold[i].Material });
                    transform.localScale =
                        Vector3.one * _particleApearanceByTreshold[i].ScaleValueRange.GetRandomValueInRange();

                    break;
                }
            }
        }

        public void CollectExp(Action callback = null)
        {
            bool audioClipPlayFinished = false;
            _audioClipPlayer.Play("ExpCollected");

            _audioClipPlayer.OnAudioClipFinished += (s, e) => audioClipPlayFinished = true;

            transform.LifeEndingShrinkToZeroTween(_disapearingDuration, () =>
            {
                _playerManager.LevelController.AddExp(_expAmount);

                if (audioClipPlayFinished)
                {
                    callback?.Invoke();
                }
                else
                {
                    _audioClipPlayer.OnAudioClipFinished += (s, e) =>
                    {
                        OnCanBeReleased?.Invoke(this, EventArgs.Empty);
                        callback?.Invoke();
                    };
                }
            });
        }
    }
}
