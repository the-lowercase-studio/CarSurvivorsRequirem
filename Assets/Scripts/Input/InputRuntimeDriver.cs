using System;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Input
{
    [DefaultExecutionOrder(-500)]
    public class InputRuntimeDriver : MonoBehaviour
    {
        [Inject] private readonly IInputRuntime _inputRuntime = null;

        private bool _isInitialized;

        private void Awake()
        {
            if (_inputRuntime == null)
            {
                throw new InvalidOperationException($"[{nameof(InputRuntimeDriver)}] IInputRuntime was not injected on '{name}'.");
            }

            if (!_inputRuntime.TryAcquireDriver())
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
            _inputRuntime.Initialize();
            _isInitialized = true;
        }

        private void Update()
        {
            if (!_isInitialized)
            {
                return;
            }

            _inputRuntime.SampleDeviceActivity();
            _inputRuntime.DispatchPendingCommands();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!_isInitialized)
            {
                return;
            }

            _inputRuntime.SetApplicationFocus(hasFocus);
        }

        private void OnDestroy()
        {
            if (!_isInitialized)
            {
                return;
            }

            _isInitialized = false;
            _inputRuntime.Suspend();
        }
    }
}
