using Assets.Scripts.Input;
using UnityEngine;

namespace Assets.ScriptableObjects.Input
{
    [CreateAssetMenu(fileName = "InputRuntimeConfig", menuName = "Scriptable Objects/Input/Input Runtime Config")]
    public class InputRuntimeConfig : ScriptableObject
    {
        [Header("Prompt Presentation")]
        [Tooltip("Keyboard glyph theme selected when presenting Keyboard&Mouse bindings.")]
        [SerializeField] private KeyboardGlyphTheme _keyboardTheme = KeyboardGlyphTheme.Light;

        [Tooltip("Optional presentation override for connected controller prompts. Auto detects layout from hardware.")]
        [SerializeField] private InputPresentationOverride _controllerFamilyOverride = InputPresentationOverride.Auto;

        [Header("Device Activity Thresholds (Hints Only)")]
        [Tooltip("Minimum stick magnitude required to switch presentation hints to a gamepad.")]
        [Range(0.05f, 0.95f)]
        [SerializeField] private float _stickActivityEnterThreshold = 0.35f;

        [Tooltip("Stick magnitude at or below which stick activity detection rearms.")]
        [Range(0.05f, 0.95f)]
        [SerializeField] private float _stickActivityRearmThreshold = 0.20f;

        [Tooltip("Minimum stick vector change required for renewed analog hint switching while held.")]
        [Range(0.01f, 0.95f)]
        [SerializeField] private float _stickActivityDeltaThreshold = 0.10f;

        [Tooltip("Minimum net mouse displacement in screen pixels within the activity window to switch hints to Keyboard&Mouse.")]
        [Min(1f)]
        [SerializeField] private float _mouseActivityPixelThreshold = 4f;

        [Tooltip("Unscaled time window in seconds for measuring net mouse displacement.")]
        [Min(0.01f)]
        [SerializeField] private float _mouseActivityWindowSeconds = 0.10f;

        public KeyboardGlyphTheme KeyboardTheme
        {
            get { return _keyboardTheme; }
        }

        public InputPresentationOverride ControllerFamilyOverride
        {
            get { return _controllerFamilyOverride; }
        }

        public float StickActivityEnterThreshold
        {
            get { return _stickActivityEnterThreshold; }
        }

        public float StickActivityRearmThreshold
        {
            get { return Mathf.Min(_stickActivityRearmThreshold, _stickActivityEnterThreshold); }
        }

        public float StickActivityDeltaThreshold
        {
            get { return _stickActivityDeltaThreshold; }
        }

        public float MouseActivityPixelThreshold
        {
            get { return _mouseActivityPixelThreshold; }
        }

        public float MouseActivityWindowSeconds
        {
            get { return _mouseActivityWindowSeconds; }
        }
    }
}
