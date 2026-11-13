using System;
using System.Collections.Generic;
using Assets.Scripts.Input;
using UnityEngine;

namespace Assets.ScriptableObjects.Input
{
    [Serializable]
    public struct InputGlyphEntry
    {
        [Tooltip("Presentation device family this glyph belongs to.")]
        [SerializeField] private InputDeviceFamily _family;

        [Tooltip("Keyboard theme when Family is KeyboardMouse.")]
        [SerializeField] private KeyboardGlyphTheme _keyboardTheme;

        [Tooltip("Canonical control path without device prefix (for example: e, digit1, buttonSouth, start, leftStick).")]
        [SerializeField] private string _canonicalControlPath;

        [Tooltip("Optional sprite for this control; when omitted, DisplayLabel is used.")]
        [SerializeField] private Sprite _sprite;

        [Tooltip("Human-readable label shown when artwork is unavailable or text fallback is requested.")]
        [SerializeField] private string _displayLabel;

        [Tooltip("Mark true if missing Sprite is an intentional text-only entry instead of missing artwork.")]
        [SerializeField] private bool _isSpriteOptional;

        public InputDeviceFamily Family
        {
            get { return _family; }
        }

        public KeyboardGlyphTheme KeyboardTheme
        {
            get { return _keyboardTheme; }
        }

        public string CanonicalControlPath
        {
            get { return _canonicalControlPath; }
        }

        public Sprite Sprite
        {
            get { return _sprite; }
        }

        public string DisplayLabel
        {
            get { return _displayLabel; }
        }

        public bool IsSpriteOptional
        {
            get { return _isSpriteOptional; }
        }

        public InputGlyphEntry(
            InputDeviceFamily family,
            KeyboardGlyphTheme keyboardTheme,
            string canonicalControlPath,
            Sprite sprite,
            string displayLabel,
            bool isSpriteOptional = false)
        {
            _family = family;
            _keyboardTheme = keyboardTheme;
            _canonicalControlPath = canonicalControlPath;
            _sprite = sprite;
            _displayLabel = displayLabel;
            _isSpriteOptional = isSpriteOptional;
        }
    }

    [CreateAssetMenu(fileName = "InputGlyphCatalog", menuName = "Scriptable Objects/Input/Input Glyph Catalog")]
    public class InputGlyphCatalog : ScriptableObject
    {
        private readonly struct GlyphLookupKey : IEquatable<GlyphLookupKey>
        {
            private readonly InputDeviceFamily _family;
            private readonly KeyboardGlyphTheme _theme;
            private readonly string _controlPath;

            public GlyphLookupKey(InputDeviceFamily family, KeyboardGlyphTheme theme, string controlPath)
            {
                _family = family;
                _theme = family == InputDeviceFamily.KeyboardMouse ? theme : KeyboardGlyphTheme.Light;
                _controlPath = controlPath ?? string.Empty;
            }

            public bool Equals(GlyphLookupKey other)
            {
                return _family == other._family
                    && _theme == other._theme
                    && string.Equals(_controlPath, other._controlPath, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object obj)
            {
                return obj is GlyphLookupKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                int pathHash = StringComparer.OrdinalIgnoreCase.GetHashCode(_controlPath);
                return HashCode.Combine((int)_family, (int)_theme, pathHash);
            }
        }

        [Tooltip("Editable designer entries pairing presentation family, keyboard theme, canonical control path, sprite, and label.")]
        [SerializeField] private InputGlyphEntry[] _entries = Array.Empty<InputGlyphEntry>();

        private Dictionary<GlyphLookupKey, InputGlyphEntry> _lookupByKey;
        private bool _isInitialized;

        public IReadOnlyList<InputGlyphEntry> Entries
        {
            get { return _entries; }
        }

        public void Initialize()
        {
            List<string> errors = new List<string>();
            BuildCache(errors);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException($"[{nameof(InputGlyphCatalog)}] Invalid catalog '{name}': {string.Join("; ", errors)}");
            }
        }

        public bool TryGetGlyph(
            InputDeviceFamily family,
            KeyboardGlyphTheme keyboardTheme,
            string canonicalControlPath,
            out InputGlyphEntry entry)
        {
            if (!_isInitialized)
            {
                Initialize();
            }

            if (string.IsNullOrEmpty(canonicalControlPath))
            {
                entry = default;
                return false;
            }

            GlyphLookupKey key = new GlyphLookupKey(family, keyboardTheme, canonicalControlPath);
            return _lookupByKey.TryGetValue(key, out entry);
        }

        public void Validate(List<string> errors, List<string> warnings)
        {
            if (errors == null || warnings == null)
            {
                throw new ArgumentNullException(errors == null ? nameof(errors) : nameof(warnings));
            }

            BuildCache(errors, warnings);
        }

        private void OnEnable()
        {
            _isInitialized = false;
        }

        private void BuildCache(List<string> errors, List<string> warnings = null)
        {
            int count = _entries != null ? _entries.Length : 0;
            if (_lookupByKey == null)
            {
                _lookupByKey = new Dictionary<GlyphLookupKey, InputGlyphEntry>(count);
            }
            else
            {
                _lookupByKey.Clear();
            }

            if (_entries == null || _entries.Length == 0)
            {
                errors.Add("Catalog contains no glyph entries.");
                _isInitialized = false;
                return;
            }

            for (int i = 0; i < _entries.Length; i++)
            {
                InputGlyphEntry entry = _entries[i];

                if (entry.Family == InputDeviceFamily.Unavailable)
                {
                    errors.Add($"Entry {i} uses reserved Family '{InputDeviceFamily.Unavailable}'.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.CanonicalControlPath))
                {
                    errors.Add($"Entry {i} ({entry.Family}) has an empty CanonicalControlPath.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.DisplayLabel))
                {
                    errors.Add($"Entry {i} ({entry.Family}/{entry.CanonicalControlPath}) has an empty DisplayLabel.");
                    continue;
                }

                GlyphLookupKey key = new GlyphLookupKey(entry.Family, entry.KeyboardTheme, entry.CanonicalControlPath);
                if (_lookupByKey.ContainsKey(key))
                {
                    errors.Add($"Duplicate glyph key ({entry.Family}, {entry.KeyboardTheme}, '{entry.CanonicalControlPath}') at index {i}.");
                    continue;
                }

                if (entry.Sprite == null && !entry.IsSpriteOptional && warnings != null)
                {
                    warnings.Add($"Entry {i} ({entry.Family}/{entry.CanonicalControlPath}) has no Sprite assigned; DisplayLabel '{entry.DisplayLabel}' will be rendered.");
                }

                _lookupByKey.Add(key, entry);
            }

            _isInitialized = errors.Count == 0;
        }
    }
}
