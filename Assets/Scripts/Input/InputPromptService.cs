using System;
using System.Collections.Generic;
using System.Text;
using Assets.ScriptableObjects.Input;
using Assets.Scripts.Input.Constants;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Input
{
    public interface IInputPromptProvider
    {
        event Action OnPromptsChanged;

        InputPromptDescriptor GetPrompt(InputActionId actionId);
        InputPromptDescriptor GetPrompt(InputPromptRequest request);
    }

    public sealed class InputPromptService : IInputPromptProvider, IDisposable
    {
        private readonly IInputActionSource _actionSource;
        private readonly IInputDeviceContext _deviceContext;
        private readonly InputGlyphCatalog _glyphCatalog;
        private readonly InputRuntimeConfig _runtimeConfig;

        private readonly Dictionary<InputPromptRequest, InputPromptDescriptor> _descriptorCache =
            new Dictionary<InputPromptRequest, InputPromptDescriptor>(32);
        private readonly StringBuilder _labelBuilder = new StringBuilder(32);

        private bool _isDisposed;

        public event Action OnPromptsChanged;

        public InputPromptService(
            IInputActionSource actionSource,
            IInputDeviceContext deviceContext,
            InputGlyphCatalog glyphCatalog,
            InputRuntimeConfig runtimeConfig)
        {
            _actionSource = actionSource ?? throw new ArgumentNullException(nameof(actionSource));
            _deviceContext = deviceContext ?? throw new ArgumentNullException(nameof(deviceContext));
            _glyphCatalog = glyphCatalog != null
                ? glyphCatalog
                : throw new ArgumentNullException(nameof(glyphCatalog));
            _runtimeConfig = runtimeConfig != null
                ? runtimeConfig
                : throw new ArgumentNullException(nameof(runtimeConfig));

            _glyphCatalog.Initialize();
            _deviceContext.OnDeviceChanged += HandleDeviceChanged;
        }

        public InputPromptDescriptor GetPrompt(InputActionId actionId)
        {
            return GetPrompt(new InputPromptRequest(actionId));
        }

        public InputPromptDescriptor GetPrompt(InputPromptRequest request)
        {
            if (_isDisposed)
            {
                return InputPromptDescriptor.CreateUnavailable(InputDeviceFamily.Unavailable, InputConstants.UNAVAILABLE_LABEL);
            }

            if (_descriptorCache.TryGetValue(request, out InputPromptDescriptor cached))
            {
                return cached;
            }

            InputPromptDescriptor built = BuildDescriptor(request);
            _descriptorCache[request] = built;
            return built;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _deviceContext.OnDeviceChanged -= HandleDeviceChanged;
            _descriptorCache.Clear();
        }

        private void HandleDeviceChanged()
        {
            if (_isDisposed)
            {
                return;
            }

            _descriptorCache.Clear();
            OnPromptsChanged?.Invoke();
        }

        private InputPromptDescriptor BuildDescriptor(InputPromptRequest request)
        {
            if (!_actionSource.TryGetAction(request.ActionId, out InputAction action) || action == null)
            {
                return new InputPromptDescriptor(
                    isValid: false,
                    isBound: false,
                    isAvailable: false,
                    family: _deviceContext.EffectiveFamily,
                    primarySprite: null,
                    primaryLabel: InputConstants.UNAVAILABLE_LABEL,
                    controlPath: string.Empty);
            }

            if (!_deviceContext.IsDeviceAvailable)
            {
                return InputPromptDescriptor.CreateUnavailable(InputDeviceFamily.Unavailable, InputConstants.DISCONNECTED_LABEL);
            }

            InputScheme activeScheme = _deviceContext.ActiveScheme;
            InputDeviceFamily family = _deviceContext.EffectiveFamily;
            KeyboardGlyphTheme theme = _runtimeConfig.KeyboardTheme;
            string targetGroup = activeScheme == InputScheme.Gamepad
                ? InputConstants.GAMEPAD_GROUP
                : InputConstants.KEYBOARD_MOUSE_GROUP;

            var bindings = action.bindings;

            if (!string.IsNullOrEmpty(request.CompositePartName))
            {
                return BuildCompositePartDescriptor(action, bindings, request, targetGroup, family, theme);
            }

            int primarySimpleIndex = -1;
            List<InputPromptPartDescriptor> simpleAlternatives = null;

            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding binding = bindings[i];
                if (binding.isComposite || binding.isPartOfComposite)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(binding.effectivePath) || !BindingMatchesGroup(binding.groups, targetGroup))
                {
                    continue;
                }

                if (primarySimpleIndex < 0)
                {
                    primarySimpleIndex = i;
                }
                else if (request.IncludeAlternatives)
                {
                    if (simpleAlternatives == null)
                    {
                        simpleAlternatives = new List<InputPromptPartDescriptor>(4);
                    }

                    ResolveSingleBinding(action, i, binding, family, theme, out Sprite altSprite, out string altLabel, out string altPath);
                    simpleAlternatives.Add(new InputPromptPartDescriptor(binding.name, altSprite, altLabel, altPath));
                }
            }

            if (primarySimpleIndex >= 0)
            {
                InputBinding primaryBinding = bindings[primarySimpleIndex];
                ResolveSingleBinding(
                    action,
                    primarySimpleIndex,
                    primaryBinding,
                    family,
                    theme,
                    out Sprite primarySprite,
                    out string primaryLabel,
                    out string primaryPath);

                return new InputPromptDescriptor(
                    isValid: true,
                    isBound: true,
                    isAvailable: true,
                    family: family,
                    primarySprite: primarySprite,
                    primaryLabel: primaryLabel,
                    controlPath: primaryPath,
                    parts: null,
                    alternatives: simpleAlternatives);
            }

            List<InputPromptPartDescriptor> primaryParts = null;
            List<InputPromptPartDescriptor> alternativeParts = null;

            for (int i = 0; i < bindings.Count; i++)
            {
                if (!bindings[i].isComposite)
                {
                    continue;
                }

                int partIndex = i + 1;
                List<InputPromptPartDescriptor> collected = null;

                while (partIndex < bindings.Count && bindings[partIndex].isPartOfComposite)
                {
                    InputBinding partBinding = bindings[partIndex];
                    if (!string.IsNullOrEmpty(partBinding.effectivePath) && BindingMatchesGroup(partBinding.groups, targetGroup))
                    {
                        if (collected == null)
                        {
                            collected = new List<InputPromptPartDescriptor>(4);
                        }

                        ResolveSingleBinding(
                            action,
                            partIndex,
                            partBinding,
                            family,
                            theme,
                            out Sprite partSprite,
                            out string partLabel,
                            out string partPath);

                        collected.Add(new InputPromptPartDescriptor(partBinding.name, partSprite, partLabel, partPath));
                    }

                    partIndex++;
                }

                if (collected != null && collected.Count > 0)
                {
                    if (primaryParts == null)
                    {
                        primaryParts = collected;
                    }
                    else if (request.IncludeAlternatives)
                    {
                        if (alternativeParts == null)
                        {
                            alternativeParts = new List<InputPromptPartDescriptor>(8);
                        }

                        for (int p = 0; p < collected.Count; p++)
                        {
                            alternativeParts.Add(collected[p]);
                        }
                    }
                }

                i = partIndex - 1;
            }

            if (primaryParts != null && primaryParts.Count > 0)
            {
                _labelBuilder.Clear();
                for (int i = 0; i < primaryParts.Count; i++)
                {
                    if (i > 0)
                    {
                        _labelBuilder.Append(" / ");
                    }

                    _labelBuilder.Append(primaryParts[i].Label);
                }

                return new InputPromptDescriptor(
                    isValid: true,
                    isBound: true,
                    isAvailable: true,
                    family: family,
                    primarySprite: primaryParts[0].Sprite,
                    primaryLabel: _labelBuilder.ToString(),
                    controlPath: primaryParts[0].ControlPath,
                    parts: primaryParts,
                    alternatives: alternativeParts);
            }

            return new InputPromptDescriptor(
                isValid: true,
                isBound: false,
                isAvailable: true,
                family: family,
                primarySprite: null,
                primaryLabel: InputConstants.UNBOUND_LABEL,
                controlPath: string.Empty);
        }

        private InputPromptDescriptor BuildCompositePartDescriptor(
            InputAction action,
            UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputBinding> bindings,
            InputPromptRequest request,
            string targetGroup,
            InputDeviceFamily family,
            KeyboardGlyphTheme theme)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding binding = bindings[i];
                if (!binding.isPartOfComposite)
                {
                    continue;
                }

                if (!string.Equals(binding.name, request.CompositePartName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(binding.effectivePath) || !BindingMatchesGroup(binding.groups, targetGroup))
                {
                    continue;
                }

                ResolveSingleBinding(action, i, binding, family, theme, out Sprite sprite, out string label, out string path);
                return new InputPromptDescriptor(
                    isValid: true,
                    isBound: true,
                    isAvailable: true,
                    family: family,
                    primarySprite: sprite,
                    primaryLabel: label,
                    controlPath: path);
            }

            return new InputPromptDescriptor(
                isValid: true,
                isBound: false,
                isAvailable: true,
                family: family,
                primarySprite: null,
                primaryLabel: InputConstants.UNBOUND_LABEL,
                controlPath: string.Empty);
        }

        private void ResolveSingleBinding(
            InputAction action,
            int bindingIndex,
            InputBinding binding,
            InputDeviceFamily family,
            KeyboardGlyphTheme theme,
            out Sprite sprite,
            out string label,
            out string canonicalPath)
        {
            string effectivePath = binding.effectivePath;
            canonicalPath = ExtractCanonicalControlPath(effectivePath);

            string displayString = action.GetBindingDisplayString(
                bindingIndex,
                InputBinding.DisplayStringOptions.DontIncludeInteractions);

            if (_glyphCatalog.TryGetGlyph(family, theme, canonicalPath, out InputGlyphEntry entry))
            {
                if (family == InputDeviceFamily.KeyboardMouse && entry.Sprite != null && IsKeyboardSpriteInaccurateForLayout(canonicalPath, entry.DisplayLabel, displayString))
                {
                    sprite = null;
                    label = !string.IsNullOrWhiteSpace(displayString) ? displayString : entry.DisplayLabel;
                    return;
                }

                sprite = entry.Sprite;
                if (family == InputDeviceFamily.KeyboardMouse)
                {
                    label = !string.IsNullOrWhiteSpace(displayString) ? displayString : entry.DisplayLabel;
                }
                else
                {
                    label = entry.DisplayLabel;
                }

                return;
            }

            sprite = null;
            label = !string.IsNullOrWhiteSpace(displayString) ? displayString : canonicalPath;
        }

        private static bool IsKeyboardSpriteInaccurateForLayout(string canonicalPath, string catalogLabel, string displayString)
        {
            if (string.IsNullOrWhiteSpace(displayString) || string.IsNullOrWhiteSpace(catalogLabel))
            {
                return false;
            }

            bool isLayoutSensitive = canonicalPath.Length == 1 && char.IsLetter(canonicalPath[0])
                || string.Equals(canonicalPath, "backquote", StringComparison.OrdinalIgnoreCase);

            if (!isLayoutSensitive)
            {
                return false;
            }

            return !string.Equals(catalogLabel.Trim(), displayString.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractCanonicalControlPath(string effectivePath)
        {
            if (string.IsNullOrWhiteSpace(effectivePath))
            {
                return string.Empty;
            }

            string path = effectivePath.Trim();
            if (path.StartsWith("<", StringComparison.Ordinal))
            {
                int closingBracketIndex = path.IndexOf(">/", StringComparison.Ordinal);
                if (closingBracketIndex >= 0 && closingBracketIndex + 2 < path.Length)
                {
                    path = path.Substring(closingBracketIndex + 2);
                }
            }

            if (path.StartsWith("/", StringComparison.Ordinal))
            {
                path = path.Substring(1);
            }

            return path;
        }

        private static bool BindingMatchesGroup(string bindingGroups, string targetGroup)
        {
            if (string.IsNullOrEmpty(bindingGroups) || string.IsNullOrEmpty(targetGroup))
            {
                return false;
            }

            int start = 0;
            int length = bindingGroups.Length;
            while (start < length)
            {
                int separator = bindingGroups.IndexOf(';', start);
                int end = separator >= 0 ? separator : length;
                int tokenLength = end - start;

                if (tokenLength == targetGroup.Length
                    && string.Compare(bindingGroups, start, targetGroup, 0, tokenLength, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return true;
                }

                start = end + 1;
            }

            return false;
        }
    }
}
