using System;
using System.Collections.Generic;
using System.Text;
using Assets.Scripts.GameFlow;
using Assets.Scripts.Player;
using Assets.Scripts.UI.DevConsole.Constants;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Scripts.UI.DevConsole
{
    public interface IDevConsolePresenter
    {
        bool IsSupported { get; }
        bool IsVisible { get; }
        void Toggle();
        void Show();
        void Hide();
    }

    public class DevConsolePresenter : MonoBehaviour, IDevConsolePresenter
    {
        [Inject] private readonly IDevConsoleService _devConsoleService = null;
        [Inject] private readonly IPlayerManager _playerManager = null;

        [Header("UI References")]
        [SerializeField] private GameObject _rootVisual;
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private TMP_Text _logText;
        [SerializeField] private ScrollRect _scrollRect;

        [Header("Configuration")]
        [SerializeField] private int _maxLogLines = DevConsoleConstants.DEFAULT_MAX_LOG_LINES;
        [SerializeField] private int _maxHistoryCount = DevConsoleConstants.DEFAULT_MAX_HISTORY_COUNT;

        private static bool IsConsoleSupported
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return Debug.isDebugBuild;
#else
                return false;
#endif
            }
        }

        private readonly List<string> _commandHistory = new();
        private readonly List<string> _visibleLogLines = new();
        private readonly StringBuilder _logBuilder = new();
        private int _historyIndex = -1;
        private bool _wasGamePausedBeforeOpen;
        private bool _isVisible;
        private bool _isSubscribedToService;
        private bool _needsRefocus;

        public bool IsSupported
        {
            get
            {
                return IsConsoleSupported;
            }
        }

        public bool IsVisible
        {
            get
            {
                return _isVisible;
            }
        }

        private void Awake()
        {
            if (!IsConsoleSupported)
            {
                if (_rootVisual != null)
                {
                    _rootVisual.SetActive(false);
                    Destroy(_rootVisual);
                }

                enabled = false;
                gameObject.SetActive(false);
                return;
            }

            if (_rootVisual == null)
            {
                throw new InvalidOperationException("DevConsolePresenter requires _rootVisual to be assigned.");
            }

            if (_inputField == null)
            {
                throw new InvalidOperationException("DevConsolePresenter requires _inputField to be assigned.");
            }

            if (_logText == null)
            {
                throw new InvalidOperationException("DevConsolePresenter requires _logText to be assigned.");
            }

            if (_scrollRect == null)
            {
                throw new InvalidOperationException("DevConsolePresenter requires _scrollRect to be assigned.");
            }

            _rootVisual.SetActive(false);
            _isVisible = false;
        }

        private void OnEnable()
        {
            if (!IsConsoleSupported)
            {
                return;
            }

            _inputField.onSubmit.AddListener(OnInputSubmit);
            SubscribeServiceEvents();
        }

        private void Start()
        {
            if (!IsConsoleSupported)
            {
                return;
            }

            SubscribeServiceEvents();
            if (_devConsoleService != null && _devConsoleService.LogHistory.Count > 0)
            {
                _visibleLogLines.Clear();
                for (int i = 0; i < _devConsoleService.LogHistory.Count; i++)
                {
                    _visibleLogLines.Add(_devConsoleService.LogHistory[i]);
                }

                RebuildLogText();
                ScrollToBottom();
            }
        }

        private void Update()
        {
            if (!IsConsoleSupported)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.backquoteKey.wasPressedThisFrame)
            {
                Toggle();
                return;
            }

            if (!_isVisible)
            {
                return;
            }

            if (_inputField.text.Length > 0 && (_inputField.text[0] == '`' || _inputField.text[0] == '~'))
            {
                _inputField.text = _inputField.text.TrimStart('`', '~');
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Hide();
                return;
            }

            if (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
            {
                SubmitCurrentInput();
                return;
            }

            if (Keyboard.current != null)
            {
                if (Keyboard.current.upArrowKey.wasPressedThisFrame)
                {
                    NavigateHistory(-1);
                }
                else if (Keyboard.current.downArrowKey.wasPressedThisFrame)
                {
                    NavigateHistory(1);
                }
            }
        }

        private void LateUpdate()
        {
            if (!IsConsoleSupported)
            {
                return;
            }

            if (_isVisible && _needsRefocus)
            {
                _needsRefocus = false;
                _inputField.ActivateInputField();
                _inputField.Select();
            }
        }

        private void OnDisable()
        {
            if (!IsConsoleSupported)
            {
                return;
            }

            _inputField.onSubmit.RemoveListener(OnInputSubmit);
            UnsubscribeServiceEvents();
        }

        private void OnDestroy()
        {
            if (!IsConsoleSupported)
            {
                return;
            }

            UnsubscribeServiceEvents();
        }

        public void Toggle()
        {
            if (!IsConsoleSupported)
            {
                return;
            }

            if (_isVisible)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        public void Show()
        {
            if (!IsConsoleSupported)
            {
                return;
            }

            if (_isVisible)
            {
                return;
            }

            _wasGamePausedBeforeOpen = (Time.timeScale == 0f);
            GameTime.Pause();

            InputActionMap playerMap = InputSystem.actions?.FindActionMap("Player");
            if (playerMap != null)
            {
                playerMap.Disable();
            }

            _rootVisual.SetActive(true);
            _isVisible = true;
            _historyIndex = _commandHistory.Count;
            _inputField.text = string.Empty;
            _needsRefocus = true;
            ScrollToBottom();
        }

        public void Hide()
        {
            if (!IsConsoleSupported)
            {
                return;
            }

            if (!_isVisible)
            {
                return;
            }

            _needsRefocus = false;
            _rootVisual.SetActive(false);
            _isVisible = false;
            _inputField.DeactivateInputField();

            InputActionMap playerMap = InputSystem.actions?.FindActionMap("Player");
            if (playerMap != null)
            {
                playerMap.Enable();
            }

            bool isPlayerAlive = _playerManager != null && _playerManager.Health != null && _playerManager.Health.IsAlive();
            if (isPlayerAlive && !_wasGamePausedBeforeOpen)
            {
                GameTime.Resume();
            }
        }

        private void SubscribeServiceEvents()
        {
            if (_devConsoleService != null && !_isSubscribedToService)
            {
                _devConsoleService.OnLogAppended += HandleLogAppended;
                _devConsoleService.OnLogsCleared += HandleLogsCleared;
                _isSubscribedToService = true;
            }
        }

        private void UnsubscribeServiceEvents()
        {
            if (_devConsoleService != null && _isSubscribedToService)
            {
                _devConsoleService.OnLogAppended -= HandleLogAppended;
                _devConsoleService.OnLogsCleared -= HandleLogsCleared;
                _isSubscribedToService = false;
            }
        }

        private void SubmitCurrentInput()
        {
            if (!_isVisible)
            {
                return;
            }

            OnInputSubmit(_inputField.text);
        }

        private void OnInputSubmit(string rawInput)
        {
            if (!_isVisible)
            {
                return;
            }

            string sanitized = rawInput?.Replace("`", string.Empty)?.Replace("~", string.Empty);
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                _inputField.text = string.Empty;
                _needsRefocus = true;
                return;
            }

            string trimmed = sanitized.Trim();
            if (_commandHistory.Count == 0 || !string.Equals(_commandHistory[_commandHistory.Count - 1], trimmed, StringComparison.Ordinal))
            {
                _commandHistory.Add(trimmed);
                if (_commandHistory.Count > _maxHistoryCount)
                {
                    _commandHistory.RemoveAt(0);
                }
            }

            _historyIndex = _commandHistory.Count;

            _devConsoleService?.ExecuteCommand(trimmed);

            _inputField.text = string.Empty;
            _needsRefocus = true;
        }

        private void NavigateHistory(int direction)
        {
            if (_commandHistory.Count == 0)
            {
                return;
            }

            int newIndex = _historyIndex + direction;
            if (newIndex < 0)
            {
                newIndex = 0;
            }
            else if (newIndex > _commandHistory.Count)
            {
                newIndex = _commandHistory.Count;
            }

            _historyIndex = newIndex;
            if (_historyIndex >= 0 && _historyIndex < _commandHistory.Count)
            {
                _inputField.text = _commandHistory[_historyIndex];
                _inputField.caretPosition = _inputField.text.Length;
            }
            else
            {
                _inputField.text = string.Empty;
            }
        }

        private void HandleLogAppended(string line)
        {
            _visibleLogLines.Add(line);
            while (_visibleLogLines.Count > _maxLogLines)
            {
                _visibleLogLines.RemoveAt(0);
            }

            RebuildLogText();
            ScrollToBottom();
        }

        private void HandleLogsCleared()
        {
            _visibleLogLines.Clear();
            _logText.text = string.Empty;
        }

        private void RebuildLogText()
        {
            _logBuilder.Clear();
            for (int i = 0; i < _visibleLogLines.Count; i++)
            {
                if (i > 0)
                {
                    _logBuilder.Append('\n');
                }

                _logBuilder.Append(_visibleLogLines[i]);
            }

            _logText.text = _logBuilder.ToString();
        }

        private void ScrollToBottom()
        {
            if (_scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                _scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }
}
