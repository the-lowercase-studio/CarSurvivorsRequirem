using System;
using Assets.Scripts.Input;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Input
{
    public class InputPromptView : MonoBehaviour
    {
        [Tooltip("Image component used to render the control glyph sprite.")]
        [SerializeField] private Image _iconImage;

        [Tooltip("TextMeshProUGUI label rendered when no sprite exists or text fallback is needed.")]
        [SerializeField] private TextMeshProUGUI _fallbackLabel;

        [Tooltip("Optional container toggled together with the fallback text label.")]
        [SerializeField] private GameObject _labelContainer;

        [Tooltip("Default action ID used when Bind(IInputPromptProvider) is called without explicit arguments.")]
        [SerializeField] private InputActionId _defaultActionId = InputActionId.None;

        [Tooltip("Optional composite part name (for example: up, down, left, right) when displaying a single part.")]
        [SerializeField] private string _compositePartName;

        [Tooltip("Include alternative bindings in the resolved prompt descriptor.")]
        [SerializeField] private bool _includeAlternatives;

        private IInputPromptProvider _promptProvider;
        private InputPromptRequest _boundRequest;
        private bool _hasBinding;
        private bool _isSubscribed;

        public InputActionId DefaultActionId
        {
            get { return _defaultActionId; }
        }

        public bool HasBinding
        {
            get { return _hasBinding; }
        }

        public Image IconImage
        {
            get { return _iconImage; }
        }

        public TextMeshProUGUI FallbackLabel
        {
            get { return _fallbackLabel; }
        }

        public void Bind(IInputPromptProvider promptProvider)
        {
            Bind(promptProvider, _defaultActionId, _compositePartName, _includeAlternatives);
        }

        public void Bind(
            IInputPromptProvider promptProvider,
            InputActionId actionId,
            string compositePartName = null,
            bool includeAlternatives = false)
        {
            if (promptProvider == null)
            {
                throw new ArgumentNullException(nameof(promptProvider));
            }

            if (actionId == InputActionId.None)
            {
                throw new ArgumentException("Cannot bind InputPromptView to InputActionId.None.", nameof(actionId));
            }

            ValidateSerializedReferences();

            if (_promptProvider != null && _promptProvider != promptProvider)
            {
                Unsubscribe();
            }

            _promptProvider = promptProvider;
            _boundRequest = new InputPromptRequest(actionId, compositePartName, includeAlternatives);
            _hasBinding = true;

            if (isActiveAndEnabled)
            {
                Subscribe();
                RefreshDisplay();
            }
        }

        public void Unbind()
        {
            Unsubscribe();
            _promptProvider = null;
            _boundRequest = default;
            _hasBinding = false;
            ClearVisuals();
        }

        public void RefreshDisplay()
        {
            if (!_hasBinding || _promptProvider == null)
            {
                ClearVisuals();
                return;
            }

            ValidateSerializedReferences();

            InputPromptDescriptor descriptor = _promptProvider.GetPrompt(_boundRequest);
            if (!descriptor.IsValid)
            {
                ClearVisuals();
                return;
            }

            if (descriptor.PrimarySprite != null)
            {
                _iconImage.sprite = descriptor.PrimarySprite;
                _iconImage.preserveAspect = true;
                _iconImage.enabled = true;

                _fallbackLabel.gameObject.SetActive(false);
                if (_labelContainer != null && _labelContainer != _fallbackLabel.gameObject)
                {
                    _labelContainer.SetActive(false);
                }
            }
            else
            {
                _iconImage.sprite = null;
                _iconImage.enabled = false;

                _fallbackLabel.text = descriptor.PrimaryLabel;
                _fallbackLabel.gameObject.SetActive(true);
                if (_labelContainer != null && _labelContainer != _fallbackLabel.gameObject)
                {
                    _labelContainer.SetActive(true);
                }
            }
        }

        private void Awake()
        {
            ValidateSerializedReferences();
        }

        private void OnEnable()
        {
            ValidateSerializedReferences();

            if (_hasBinding && _promptProvider != null)
            {
                Subscribe();
                RefreshDisplay();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            _promptProvider = null;
        }

        private void Subscribe()
        {
            if (_isSubscribed || _promptProvider == null)
            {
                return;
            }

            _promptProvider.OnPromptsChanged += HandlePromptsChanged;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed || _promptProvider == null)
            {
                _isSubscribed = false;
                return;
            }

            _promptProvider.OnPromptsChanged -= HandlePromptsChanged;
            _isSubscribed = false;
        }

        private void HandlePromptsChanged()
        {
            if (isActiveAndEnabled && _hasBinding)
            {
                RefreshDisplay();
            }
        }

        private void ClearVisuals()
        {
            if (_iconImage != null)
            {
                _iconImage.sprite = null;
                _iconImage.enabled = false;
            }

            if (_fallbackLabel != null)
            {
                _fallbackLabel.text = string.Empty;
                _fallbackLabel.gameObject.SetActive(false);
            }

            if (_labelContainer != null && (_fallbackLabel == null || _labelContainer != _fallbackLabel.gameObject))
            {
                _labelContainer.SetActive(false);
            }
        }

        private void ValidateSerializedReferences()
        {
            if (_iconImage == null)
            {
                throw new InvalidOperationException($"[{nameof(InputPromptView)}] _iconImage is unassigned on '{name}'.");
            }

            if (_fallbackLabel == null)
            {
                throw new InvalidOperationException($"[{nameof(InputPromptView)}] _fallbackLabel is unassigned on '{name}'.");
            }
        }
    }
}
