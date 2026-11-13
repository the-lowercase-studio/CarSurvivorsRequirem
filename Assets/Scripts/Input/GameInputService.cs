using System;
using System.Collections.Generic;
using Assets.ScriptableObjects.Input;
using Assets.Scripts.Common.EventArgs;
using Assets.Scripts.GameFlow;
using Assets.Scripts.Input.Constants;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.XInput;
#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX || UNITY_WSA
using UnityEngine.InputSystem.Switch;
#endif

namespace Assets.Scripts.Input
{
    public interface IGameplayInput
    {
        Vector2 MoveInput { get; }
        bool IsBrakePressed { get; }
        float ReverseInput { get; }
        bool WasInteractPressedThisFrame { get; }

        event EventHandler OnBrakeStarted;
        event EventHandler OnBrakeCanceled;
    }

    public interface IInputCommands
    {
        event Action<InputCommand> OnCommand;
    }

    public interface IInputContexts
    {
        InputContextScopeKind EffectiveContext { get; }
        uint SceneEpoch { get; }
        uint ContextGeneration { get; }
        bool IsUiNavigationEligible { get; }
        bool IsRewardChoiceEligible { get; }
        bool IsBackEligible { get; }
        bool IsGameplayEligible { get; }
        bool IsInteractEligible { get; }

        event Action OnContextChanged;

        void RegisterSceneReady();
        InputScopeToken AcquireScope(InputContextScopeKind kind, int ownerId);
        void ReleaseScope(InputScopeToken token);
        void SetLiveRewardEligibleSlots(int slotCount);
        bool IsSlotEligible(int slotIndex);
        void ConsumeActiveControlsUntilRelease();
        void ReapplyPolicy();
    }

    public interface IInputDeviceContext
    {
        InputScheme ActiveScheme { get; }
        InputDeviceFamily EffectiveFamily { get; }
        KeyboardGlyphTheme KeyboardTheme { get; }
        bool HasKeyboardOrMouse { get; }
        bool HasSupportedGamepad { get; }
        bool IsDeviceAvailable { get; }
        Gamepad ActiveGamepad { get; }
        Keyboard ActiveKeyboard { get; }

        event Action OnDeviceChanged;
    }

    public interface IInputActionSource
    {
        InputActionAsset ActionAsset { get; }

        bool TryGetAction(InputActionId actionId, out InputAction action);
        InputAction GetRequiredAction(InputActionId actionId);
    }

    public interface IInputRuntime
    {
        bool TryAcquireDriver();
        void Initialize();
        void DispatchPendingCommands();
        void SampleDeviceActivity();
        void SetApplicationFocus(bool hasFocus);
        void Suspend();
    }

    public sealed class GameInputService :
        IGameplayInput,
        IInputCommands,
        IInputContexts,
        IInputDeviceContext,
        IInputActionSource,
        IInputRuntime,
        IDisposable
    {
        private readonly struct ActiveScopeEntry
        {
            public InputScopeToken Token { get; }

            public ActiveScopeEntry(InputScopeToken token)
            {
                Token = token;
            }
        }

        private sealed class GamepadStickActivityState
        {
            public int DeviceId;
            public bool LeftRearmed;
            public bool RightRearmed;
            public bool LeftTriggerRearmed;
            public bool RightTriggerRearmed;
            public Vector2 LastLeftStick;
            public Vector2 LastRightStick;
        }

        private readonly InputActionAsset _inputActions;
        private readonly InputRuntimeConfig _runtimeConfig;
        private readonly IGameSceneLoader _gameSceneLoader;

        private readonly Dictionary<InputActionId, InputAction> _actionsById = new Dictionary<InputActionId, InputAction>(20);
        private readonly List<ActiveScopeEntry> _activeScopes = new List<ActiveScopeEntry>(8);
        private readonly List<InputCommand> _pendingCommands = new List<InputCommand>(InputConstants.INITIAL_COMMAND_BUFFER_CAPACITY);
        private readonly List<InputCommand> _dispatchBuffer = new List<InputCommand>(InputConstants.INITIAL_COMMAND_BUFFER_CAPACITY);
        private readonly List<InputControl> _controlsAwaitingRelease = new List<InputControl>(8);
        private readonly List<GamepadStickActivityState> _stickStates = new List<GamepadStickActivityState>(4);
        private readonly HashSet<int> _loggedUnsupportedJoysticks = new HashSet<int>();

        private InputAction _moveAction;
        private InputAction _brakeAction;
        private InputAction _reverseAction;
        private InputAction _interactAction;
        private InputAction _pauseAction;
        private InputAction _toggleConsoleAction;
        private InputAction _uiNavigateAction;
        private InputAction _uiSubmitAction;
        private InputAction _uiCancelAction;
        private InputAction _uiPointAction;
        private InputAction _uiClickAction;
        private InputAction _uiScrollWheelAction;
        private InputAction _selectChoice1Action;
        private InputAction _selectChoice2Action;
        private InputAction _selectChoice3Action;
        private InputAction _devConsoleCloseAction;
        private InputAction _devConsoleSubmitAction;
        private InputAction _devConsoleHistoryPrevAction;
        private InputAction _devConsoleHistoryNextAction;

        private bool _isInitialized;
        private bool _isDisposed;
        private bool _isSuspended;
        private bool _hasActiveDriver;
        private bool _isApplicationFocused = true;
        private bool _isLoading;
        private bool _isSceneReady;
        private bool _isBrakePressed;
        private bool _isSwitchFaceButtonLayoutActive;

        private uint _sceneEpoch = 1;
        private uint _contextGeneration = 1;
        private uint _nextTokenId = 1;
        private uint _inputUpdateCounter = 1;

        private int _eligibleRewardSlotCount;
        private int _lastModalTransitionFrame = -1;

        private InputScheme _activeScheme = InputScheme.KeyboardMouse;
        private InputDeviceFamily _effectiveFamily = InputDeviceFamily.KeyboardMouse;
        private Gamepad _activeGamepad;
        private Keyboard _activeKeyboard;
        private bool _hasKeyboardOrMouse;
        private bool _hasSupportedGamepad;

        private Vector2 _mouseWindowAnchor;
        private float _mouseWindowStartTime;
        private bool _hasMouseWindowAnchor;

        public Vector2 MoveInput
        {
            get
            {
                if (!CanReadGameplayState() || _moveAction == null || !_moveAction.enabled)
                {
                    return Vector2.zero;
                }

                return _moveAction.ReadValue<Vector2>();
            }
        }

        public bool IsBrakePressed
        {
            get
            {
                if (!CanReadGameplayState() || !_isBrakePressed)
                {
                    return false;
                }

                return true;
            }
        }

        public float ReverseInput
        {
            get
            {
                if (!CanReadGameplayState() || _reverseAction == null || !_reverseAction.enabled)
                {
                    return 0f;
                }

                return Mathf.Clamp01(_reverseAction.ReadValue<float>());
            }
        }

        public bool WasInteractPressedThisFrame
        {
            get
            {
                if (!CanReadGameplayState() || !IsInteractEligible || _interactAction == null || !_interactAction.enabled)
                {
                    return false;
                }

                if (_lastModalTransitionFrame == Time.frameCount)
                {
                    return false;
                }

                if (!_interactAction.WasPressedThisFrame())
                {
                    return false;
                }

                if (IsActionTriggeredByHeldControlAwaitingRelease(_interactAction))
                {
                    return false;
                }

                MarkTriggeredControlsForRelease(_interactAction);
                return true;
            }
        }

        public InputContextScopeKind EffectiveContext
        {
            get { return ResolveEffectiveContext(); }
        }

        public uint SceneEpoch
        {
            get { return _sceneEpoch; }
        }

        public uint ContextGeneration
        {
            get { return _contextGeneration; }
        }

        public bool IsUiNavigationEligible
        {
            get
            {
                InputContextScopeKind effective = ResolveEffectiveContext();
                return effective == InputContextScopeKind.Menu
                    || effective == InputContextScopeKind.Pause
                    || effective == InputContextScopeKind.PauseChildPanel
                    || effective == InputContextScopeKind.Death;
            }
        }

        public bool IsRewardChoiceEligible
        {
            get
            {
                return ResolveEffectiveContext() == InputContextScopeKind.LiveReward && _eligibleRewardSlotCount > 0;
            }
        }

        public bool IsBackEligible
        {
            get
            {
                InputContextScopeKind effective = ResolveEffectiveContext();
                return effective == InputContextScopeKind.Menu
                    || effective == InputContextScopeKind.Pause
                    || effective == InputContextScopeKind.PauseChildPanel
                    || effective == InputContextScopeKind.DevConsole;
            }
        }

        public bool IsGameplayEligible
        {
            get
            {
                InputContextScopeKind effective = ResolveEffectiveContext();
                return effective == InputContextScopeKind.None || effective == InputContextScopeKind.LiveReward;
            }
        }

        public bool IsInteractEligible
        {
            get
            {
                return ResolveEffectiveContext() == InputContextScopeKind.None;
            }
        }

        public InputScheme ActiveScheme
        {
            get { return _activeScheme; }
        }

        public InputDeviceFamily EffectiveFamily
        {
            get { return _effectiveFamily; }
        }

        public KeyboardGlyphTheme KeyboardTheme
        {
            get { return _runtimeConfig.KeyboardTheme; }
        }

        public bool HasKeyboardOrMouse
        {
            get { return _hasKeyboardOrMouse; }
        }

        public bool HasSupportedGamepad
        {
            get { return _hasSupportedGamepad; }
        }

        public bool IsDeviceAvailable
        {
            get { return _effectiveFamily != InputDeviceFamily.Unavailable; }
        }

        public Gamepad ActiveGamepad
        {
            get { return _activeGamepad; }
        }

        public Keyboard ActiveKeyboard
        {
            get { return _activeKeyboard; }
        }

        public InputActionAsset ActionAsset
        {
            get { return _inputActions; }
        }

        public event EventHandler OnBrakeStarted;
        public event EventHandler OnBrakeCanceled;
        public event Action<InputCommand> OnCommand;
        public event Action OnContextChanged;
        public event Action OnDeviceChanged;

        public GameInputService(
            InputActionAsset inputActions,
            InputRuntimeConfig runtimeConfig,
            IGameSceneLoader gameSceneLoader)
        {
            _inputActions = inputActions != null
                ? inputActions
                : throw new ArgumentNullException(nameof(inputActions));
            _runtimeConfig = runtimeConfig != null
                ? runtimeConfig
                : throw new ArgumentNullException(nameof(runtimeConfig));
            _gameSceneLoader = gameSceneLoader ?? throw new ArgumentNullException(nameof(gameSceneLoader));
        }

        public bool TryAcquireDriver()
        {
            if (_isDisposed || _hasActiveDriver)
            {
                return false;
            }

            _hasActiveDriver = true;
            return true;
        }

        public void Initialize()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(GameInputService));
            }

            if (_isInitialized)
            {
                return;
            }

            _isSuspended = false;
            _isApplicationFocused = Application.isFocused;

            ResolveAndValidateActions();
            DisableAllActionMaps();
            SubscribeActionCallbacks();

            InputSystem.onDeviceChange += HandleDeviceChange;
            _gameSceneLoader.OnStartLoadingScene += HandleStartLoadingScene;
            _gameSceneLoader.OnSceneLoaded += HandleSceneLoaded;

            _isInitialized = true;

            RefreshDeviceAvailability(switchIfUnavailable: true);
            SyncSwitchFaceButtonOverrides();
            LogExistingUnsupportedJoysticks();
            ResetMouseActivityAnchor();
            ClearStickRearmStates();
            ReapplyPolicy();
            SyncCursorVisibility();
        }

        public void RegisterSceneReady()
        {
            if (!_isInitialized || _isDisposed)
            {
                return;
            }

            _isSceneReady = true;
            _contextGeneration++;
            ReapplyPolicy();
            OnContextChanged?.Invoke();
        }

        public InputScopeToken AcquireScope(InputContextScopeKind kind, int ownerId)
        {
            if (kind == InputContextScopeKind.None)
            {
                throw new ArgumentException("Cannot acquire InputContextScopeKind.None as a modal scope.", nameof(kind));
            }

            if (!_isInitialized || _isDisposed)
            {
                return default;
            }

            uint id = _nextTokenId++;
            if (_nextTokenId == 0)
            {
                _nextTokenId = 1;
            }

            InputScopeToken token = new InputScopeToken(id, _sceneEpoch, kind, ownerId);
            _activeScopes.Add(new ActiveScopeEntry(token));
            _contextGeneration++;

            CapturePressedDigitalControlsForRelease();
            ReapplyPolicy();
            OnContextChanged?.Invoke();
            return token;
        }

        public void ReleaseScope(InputScopeToken token)
        {
            if (!token.IsValid || token.SceneEpoch != _sceneEpoch || _isDisposed)
            {
                return;
            }

            int foundIndex = -1;
            for (int i = 0; i < _activeScopes.Count; i++)
            {
                if (_activeScopes[i].Token.Equals(token))
                {
                    foundIndex = i;
                    break;
                }
            }

            if (foundIndex < 0)
            {
                return;
            }

            _activeScopes.RemoveAt(foundIndex);
            _contextGeneration++;

            CapturePressedDigitalControlsForRelease();
            ReapplyPolicy();
            OnContextChanged?.Invoke();
        }

        public void SetLiveRewardEligibleSlots(int slotCount)
        {
            int clamped = Mathf.Clamp(slotCount, 0, InputConstants.MAX_UPGRADE_SLOTS);
            if (_eligibleRewardSlotCount == clamped)
            {
                return;
            }

            _eligibleRewardSlotCount = clamped;
            if (_isInitialized && !_isDisposed)
            {
                ReapplyPolicy();
            }
        }

        public bool IsSlotEligible(int slotIndex)
        {
            if (!IsRewardChoiceEligible)
            {
                return false;
            }

            return slotIndex >= 0 && slotIndex < _eligibleRewardSlotCount;
        }

        public void ConsumeActiveControlsUntilRelease()
        {
            if (!_isInitialized || _isDisposed)
            {
                return;
            }

            CapturePressedDigitalControlsForRelease();
            ReapplyPolicy();
            SyncCursorVisibility();
        }

        public void ReapplyPolicy()
        {
            if (!_isInitialized || _isDisposed)
            {
                return;
            }

            if (_isSuspended || !_isApplicationFocused)
            {
                DisableAllActionMaps();
                CancelActiveBrakeIfNeeded();
                return;
            }

            InputContextScopeKind effective = ResolveEffectiveContext();
            bool hasDevice = IsDeviceAvailable;

            bool gameplayAllowed = hasDevice && (effective == InputContextScopeKind.None || effective == InputContextScopeKind.LiveReward);
            bool interactAllowed = hasDevice && effective == InputContextScopeKind.None;
            bool pauseAllowed = hasDevice && (effective == InputContextScopeKind.None || effective == InputContextScopeKind.LiveReward);
            bool consoleToggleAllowed = IsConsoleSupported()
                && hasDevice
                && effective != InputContextScopeKind.Loading
                && effective != InputContextScopeKind.Menu;
            bool uiNavAllowed = hasDevice && IsUiNavigationEligible;
            bool uiPointerAllowed = hasDevice && (uiNavAllowed || effective == InputContextScopeKind.LiveReward);
            bool uiCancelAllowed = hasDevice && (effective == InputContextScopeKind.Menu
                || effective == InputContextScopeKind.Pause
                || effective == InputContextScopeKind.PauseChildPanel);
            bool rewardAllowed = hasDevice && effective == InputContextScopeKind.LiveReward;
            bool devConsoleAllowed = IsConsoleSupported() && hasDevice && effective == InputContextScopeKind.DevConsole;

            SetActionEnabled(_moveAction, gameplayAllowed);
            SetActionEnabled(_brakeAction, gameplayAllowed);
            SetActionEnabled(_reverseAction, gameplayAllowed);
            SetActionEnabled(_interactAction, interactAllowed);

            if (!gameplayAllowed)
            {
                CancelActiveBrakeIfNeeded();
            }

            SetActionEnabled(_pauseAction, pauseAllowed);
            SetActionEnabled(_toggleConsoleAction, consoleToggleAllowed);

            SetActionEnabled(_uiNavigateAction, uiNavAllowed);
            SetActionEnabled(_uiSubmitAction, uiNavAllowed);
            SetActionEnabled(_uiCancelAction, uiCancelAllowed);
            SetActionEnabled(_uiPointAction, uiPointerAllowed);
            SetActionEnabled(_uiClickAction, uiPointerAllowed);
            SetActionEnabled(_uiScrollWheelAction, uiNavAllowed);

            SetActionEnabled(_selectChoice1Action, rewardAllowed && _eligibleRewardSlotCount >= 1);
            SetActionEnabled(_selectChoice2Action, rewardAllowed && _eligibleRewardSlotCount >= 2);
            SetActionEnabled(_selectChoice3Action, rewardAllowed && _eligibleRewardSlotCount >= 3);

            SetActionEnabled(_devConsoleCloseAction, devConsoleAllowed);
            SetActionEnabled(_devConsoleSubmitAction, devConsoleAllowed);
            SetActionEnabled(_devConsoleHistoryPrevAction, devConsoleAllowed);
            SetActionEnabled(_devConsoleHistoryNextAction, devConsoleAllowed);
        }

        public bool TryGetAction(InputActionId actionId, out InputAction action)
        {
            return _actionsById.TryGetValue(actionId, out action);
        }

        public InputAction GetRequiredAction(InputActionId actionId)
        {
            if (!_actionsById.TryGetValue(actionId, out InputAction action) || action == null)
            {
                throw new InvalidOperationException($"[{nameof(GameInputService)}] Required action '{actionId}' is not resolved.");
            }

            return action;
        }

        public void DispatchPendingCommands()
        {
            if (!_isInitialized || _isDisposed)
            {
                _pendingCommands.Clear();
                return;
            }

            _inputUpdateCounter++;

            if (_pendingCommands.Count == 0)
            {
                UpdateControlsAwaitingRelease();
                return;
            }

            _dispatchBuffer.Clear();
            for (int i = 0; i < _pendingCommands.Count; i++)
            {
                _dispatchBuffer.Add(_pendingCommands[i]);
            }
            _pendingCommands.Clear();

            SortCommandsByPriority(_dispatchBuffer);

            bool modalTransitionDispatchedThisBatch = false;
            for (int i = 0; i < _dispatchBuffer.Count; i++)
            {
                InputCommand command = _dispatchBuffer[i];

                if (command.SceneEpoch != _sceneEpoch || command.ContextGeneration != _contextGeneration)
                {
                    continue;
                }

                if (modalTransitionDispatchedThisBatch && !IsModalTransitionCommand(command.Kind))
                {
                    continue;
                }

                if (!IsCommandStillEligible(command))
                {
                    continue;
                }

                if (IsModalTransitionCommand(command.Kind))
                {
                    modalTransitionDispatchedThisBatch = true;
                    _lastModalTransitionFrame = Time.frameCount;
                }

                OnCommand?.Invoke(command);
            }

            _dispatchBuffer.Clear();
            UpdateControlsAwaitingRelease();
        }

        public void SampleDeviceActivity()
        {
            if (!_isInitialized || _isDisposed || !_isApplicationFocused || _isSuspended)
            {
                return;
            }

            bool deviceChanged = false;

            if (DetectMeaningfulKeyboardOrMouseActivity())
            {
                Keyboard currentKeyboard = Keyboard.current;
                if (currentKeyboard != null)
                {
                    _activeKeyboard = currentKeyboard;
                }

                if (_activeScheme != InputScheme.KeyboardMouse || _effectiveFamily != InputDeviceFamily.KeyboardMouse)
                {
                    _activeScheme = InputScheme.KeyboardMouse;
                    _effectiveFamily = InputDeviceFamily.KeyboardMouse;
                    deviceChanged = true;
                }
            }
            else if (DetectMeaningfulGamepadActivity(out Gamepad activatedGamepad))
            {
                InputDeviceFamily detectedFamily = ResolveGamepadPresentationFamily(activatedGamepad);
                if (_activeScheme != InputScheme.Gamepad
                    || _activeGamepad != activatedGamepad
                    || _effectiveFamily != detectedFamily)
                {
                    _activeScheme = InputScheme.Gamepad;
                    _activeGamepad = activatedGamepad;
                    _effectiveFamily = detectedFamily;
                    deviceChanged = true;
                }
            }

            if (deviceChanged)
            {
                SyncSwitchFaceButtonOverrides();
                SyncCursorVisibility();
                OnDeviceChanged?.Invoke();
            }
        }

        public void SetApplicationFocus(bool hasFocus)
        {
            if (!_isInitialized || _isDisposed)
            {
                _isApplicationFocused = hasFocus;
                return;
            }

            if (_isApplicationFocused == hasFocus)
            {
                return;
            }

            _isApplicationFocused = hasFocus;
            _pendingCommands.Clear();

            if (!hasFocus)
            {
                CancelActiveBrakeIfNeeded();
                CapturePressedDigitalControlsForRelease();
                ClearStickRearmStates();
                ResetMouseActivityAnchor();
            }
            else
            {
                CapturePressedDigitalControlsForRelease();
                ClearStickRearmStates();
                ResetMouseActivityAnchor();
                RefreshDeviceAvailability(switchIfUnavailable: true);
                SyncSwitchFaceButtonOverrides();
            }

            ReapplyPolicy();
            SyncCursorVisibility();
        }

        public void Suspend()
        {
            if (!_isInitialized || _isDisposed)
            {
                return;
            }

            _hasActiveDriver = false;
            _isSuspended = true;
            _pendingCommands.Clear();
            CancelActiveBrakeIfNeeded();
            DisableAllActionMaps();
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _hasActiveDriver = false;

            if (_isInitialized)
            {
                UnsubscribeActionCallbacks();
                InputSystem.onDeviceChange -= HandleDeviceChange;
                _gameSceneLoader.OnStartLoadingScene -= HandleStartLoadingScene;
                _gameSceneLoader.OnSceneLoaded -= HandleSceneLoaded;
                ClearSwitchFaceButtonOverrides();
                DisableAllActionMaps();
            }

            _activeScopes.Clear();
            _pendingCommands.Clear();
            _dispatchBuffer.Clear();
            _controlsAwaitingRelease.Clear();
            _stickStates.Clear();
            Cursor.visible = true;
            _actionsById.Clear();
            _loggedUnsupportedJoysticks.Clear();
        }

        private void ResolveAndValidateActions()
        {
            _actionsById.Clear();

            _moveAction = FindAndRegisterAction(InputActionId.PlayerMove, InputConstants.PLAYER_MOVE_ACTION, InputConstants.EXPECTED_VECTOR2_CONTROL_TYPE);
            _brakeAction = FindAndRegisterAction(InputActionId.PlayerBrake, InputConstants.PLAYER_BRAKE_ACTION, null);
            _reverseAction = FindAndRegisterAction(InputActionId.PlayerReverse, InputConstants.PLAYER_REVERSE_ACTION, InputConstants.EXPECTED_AXIS_CONTROL_TYPE);
            _interactAction = FindAndRegisterAction(InputActionId.PlayerInteract, InputConstants.PLAYER_INTERACT_ACTION, null);

            _pauseAction = FindAndRegisterAction(InputActionId.GlobalPause, InputConstants.GLOBAL_PAUSE_ACTION, null);
            _toggleConsoleAction = FindAndRegisterAction(InputActionId.GlobalToggleConsole, InputConstants.GLOBAL_TOGGLE_CONSOLE_ACTION, null);

            _uiNavigateAction = FindAndRegisterAction(InputActionId.UiNavigate, InputConstants.UI_NAVIGATE_ACTION, InputConstants.EXPECTED_VECTOR2_CONTROL_TYPE);
            _uiSubmitAction = FindAndRegisterAction(InputActionId.UiSubmit, InputConstants.UI_SUBMIT_ACTION, null);
            _uiCancelAction = FindAndRegisterAction(InputActionId.UiCancel, InputConstants.UI_CANCEL_ACTION, null);
            _uiPointAction = FindAndRegisterAction(InputActionId.UiPoint, InputConstants.UI_POINT_ACTION, InputConstants.EXPECTED_VECTOR2_CONTROL_TYPE);
            _uiClickAction = FindAndRegisterAction(InputActionId.UiClick, InputConstants.UI_CLICK_ACTION, null);
            _uiScrollWheelAction = FindAndRegisterAction(InputActionId.UiScrollWheel, InputConstants.UI_SCROLL_WHEEL_ACTION, InputConstants.EXPECTED_VECTOR2_CONTROL_TYPE);

            _selectChoice1Action = FindAndRegisterAction(InputActionId.UpgradeSelectChoice1, InputConstants.UPGRADE_SELECT_CHOICE_1_ACTION, null);
            _selectChoice2Action = FindAndRegisterAction(InputActionId.UpgradeSelectChoice2, InputConstants.UPGRADE_SELECT_CHOICE_2_ACTION, null);
            _selectChoice3Action = FindAndRegisterAction(InputActionId.UpgradeSelectChoice3, InputConstants.UPGRADE_SELECT_CHOICE_3_ACTION, null);

            _devConsoleCloseAction = FindAndRegisterAction(InputActionId.DevConsoleClose, InputConstants.DEV_CONSOLE_CLOSE_ACTION, null);
            _devConsoleSubmitAction = FindAndRegisterAction(InputActionId.DevConsoleSubmit, InputConstants.DEV_CONSOLE_SUBMIT_ACTION, null);
            _devConsoleHistoryPrevAction = FindAndRegisterAction(InputActionId.DevConsoleHistoryPrevious, InputConstants.DEV_CONSOLE_HISTORY_PREVIOUS_ACTION, null);
            _devConsoleHistoryNextAction = FindAndRegisterAction(InputActionId.DevConsoleHistoryNext, InputConstants.DEV_CONSOLE_HISTORY_NEXT_ACTION, null);
        }

        private InputAction FindAndRegisterAction(InputActionId actionId, string actionPath, string expectedControlType)
        {
            InputAction action = _inputActions.FindAction(actionPath, throwIfNotFound: false);
            if (action == null)
            {
                throw new InvalidOperationException($"[{nameof(GameInputService)}] Required action '{actionPath}' was not found in '{_inputActions.name}'.");
            }

            if (!string.IsNullOrEmpty(expectedControlType)
                && !string.Equals(action.expectedControlType, expectedControlType, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"[{nameof(GameInputService)}] Action '{actionPath}' expected control type '{expectedControlType}', but found '{action.expectedControlType}'.");
            }

            _actionsById.Add(actionId, action);
            return action;
        }

        private void SubscribeActionCallbacks()
        {
            _brakeAction.started += HandleBrakeStarted;
            _brakeAction.canceled += HandleBrakeCanceled;

            _pauseAction.performed += HandlePausePerformed;
            _toggleConsoleAction.performed += HandleToggleConsolePerformed;
            _uiCancelAction.performed += HandleUiCancelPerformed;

            _selectChoice1Action.performed += HandleSelectChoice1Performed;
            _selectChoice2Action.performed += HandleSelectChoice2Performed;
            _selectChoice3Action.performed += HandleSelectChoice3Performed;

            _devConsoleCloseAction.performed += HandleDevConsoleClosePerformed;
            _devConsoleSubmitAction.performed += HandleDevConsoleSubmitPerformed;
            _devConsoleHistoryPrevAction.performed += HandleDevConsoleHistoryPreviousPerformed;
            _devConsoleHistoryNextAction.performed += HandleDevConsoleHistoryNextPerformed;
        }

        private void UnsubscribeActionCallbacks()
        {
            if (_brakeAction != null)
            {
                _brakeAction.started -= HandleBrakeStarted;
                _brakeAction.canceled -= HandleBrakeCanceled;
            }

            if (_pauseAction != null)
            {
                _pauseAction.performed -= HandlePausePerformed;
            }

            if (_toggleConsoleAction != null)
            {
                _toggleConsoleAction.performed -= HandleToggleConsolePerformed;
            }

            if (_uiCancelAction != null)
            {
                _uiCancelAction.performed -= HandleUiCancelPerformed;
            }

            if (_selectChoice1Action != null)
            {
                _selectChoice1Action.performed -= HandleSelectChoice1Performed;
            }

            if (_selectChoice2Action != null)
            {
                _selectChoice2Action.performed -= HandleSelectChoice2Performed;
            }

            if (_selectChoice3Action != null)
            {
                _selectChoice3Action.performed -= HandleSelectChoice3Performed;
            }

            if (_devConsoleCloseAction != null)
            {
                _devConsoleCloseAction.performed -= HandleDevConsoleClosePerformed;
            }

            if (_devConsoleSubmitAction != null)
            {
                _devConsoleSubmitAction.performed -= HandleDevConsoleSubmitPerformed;
            }

            if (_devConsoleHistoryPrevAction != null)
            {
                _devConsoleHistoryPrevAction.performed -= HandleDevConsoleHistoryPreviousPerformed;
            }

            if (_devConsoleHistoryNextAction != null)
            {
                _devConsoleHistoryNextAction.performed -= HandleDevConsoleHistoryNextPerformed;
            }
        }

        private void HandleBrakeStarted(InputAction.CallbackContext context)
        {
            if (!CanReadGameplayState())
            {
                return;
            }

            InputControl control = context.control;
            if (IsControlAwaitingRelease(control))
            {
                return;
            }

            if (!_isBrakePressed)
            {
                _isBrakePressed = true;
                OnBrakeStarted?.Invoke(this, EventArgs.Empty);
            }
        }

        private void HandleBrakeCanceled(InputAction.CallbackContext context)
        {
            if (!_isBrakePressed)
            {
                return;
            }

            if (_brakeAction != null && _brakeAction.enabled && _brakeAction.IsPressed())
            {
                return;
            }

            _isBrakePressed = false;
            OnBrakeCanceled?.Invoke(this, EventArgs.Empty);
        }

        private void HandlePausePerformed(InputAction.CallbackContext context)
        {
            EnqueueCommand(InputCommandKind.Pause, InputActionId.GlobalPause, -1, context.control);
        }

        private void HandleToggleConsolePerformed(InputAction.CallbackContext context)
        {
            EnqueueCommand(InputCommandKind.ToggleConsole, InputActionId.GlobalToggleConsole, -1, context.control);
        }

        private void HandleUiCancelPerformed(InputAction.CallbackContext context)
        {
            EnqueueCommand(InputCommandKind.UiCancel, InputActionId.UiCancel, -1, context.control);
        }

        private void HandleSelectChoice1Performed(InputAction.CallbackContext context)
        {
            EnqueueCommand(InputCommandKind.UpgradeSelectChoice, InputActionId.UpgradeSelectChoice1, 0, context.control);
        }

        private void HandleSelectChoice2Performed(InputAction.CallbackContext context)
        {
            EnqueueCommand(InputCommandKind.UpgradeSelectChoice, InputActionId.UpgradeSelectChoice2, 1, context.control);
        }

        private void HandleSelectChoice3Performed(InputAction.CallbackContext context)
        {
            EnqueueCommand(InputCommandKind.UpgradeSelectChoice, InputActionId.UpgradeSelectChoice3, 2, context.control);
        }

        private void HandleDevConsoleClosePerformed(InputAction.CallbackContext context)
        {
            EnqueueCommand(InputCommandKind.DevConsoleClose, InputActionId.DevConsoleClose, -1, context.control);
        }

        private void HandleDevConsoleSubmitPerformed(InputAction.CallbackContext context)
        {
            EnqueueCommand(InputCommandKind.DevConsoleSubmit, InputActionId.DevConsoleSubmit, -1, context.control);
        }

        private void HandleDevConsoleHistoryPreviousPerformed(InputAction.CallbackContext context)
        {
            EnqueueCommand(InputCommandKind.DevConsoleHistoryPrevious, InputActionId.DevConsoleHistoryPrevious, -1, context.control);
        }

        private void HandleDevConsoleHistoryNextPerformed(InputAction.CallbackContext context)
        {
            EnqueueCommand(InputCommandKind.DevConsoleHistoryNext, InputActionId.DevConsoleHistoryNext, -1, context.control);
        }

        private void EnqueueCommand(InputCommandKind kind, InputActionId actionId, int slotIndex, InputControl control)
        {
            if (!_isInitialized || _isDisposed || _isSuspended || !_isApplicationFocused)
            {
                return;
            }

            if (control != null && IsControlAwaitingRelease(control))
            {
                return;
            }

            if (!IsCommandEligibleAtPerformance(kind, slotIndex))
            {
                return;
            }

            int deviceId = control != null && control.device != null ? control.device.deviceId : InputDevice.InvalidDeviceId;
            string controlPath = control != null ? control.path : string.Empty;

            if (control != null && ShouldRequireReleaseAfterCommand(kind))
            {
                AddControlAwaitingRelease(control);
            }

            InputCommand command = new InputCommand(
                kind,
                actionId,
                slotIndex,
                deviceId,
                controlPath,
                _inputUpdateCounter,
                Time.frameCount,
                _sceneEpoch,
                _contextGeneration);

            _pendingCommands.Add(command);
        }

        private bool IsCommandEligibleAtPerformance(InputCommandKind kind, int slotIndex)
        {
            InputContextScopeKind effective = ResolveEffectiveContext();
            if (effective == InputContextScopeKind.Loading)
            {
                return false;
            }

            switch (kind)
            {
                case InputCommandKind.Pause:
                    return effective == InputContextScopeKind.None || effective == InputContextScopeKind.LiveReward;
                case InputCommandKind.ToggleConsole:
                    return IsConsoleSupported() && effective != InputContextScopeKind.Menu;
                case InputCommandKind.UiCancel:
                    return effective == InputContextScopeKind.Menu
                        || effective == InputContextScopeKind.Pause
                        || effective == InputContextScopeKind.PauseChildPanel;
                case InputCommandKind.UpgradeSelectChoice:
                    return effective == InputContextScopeKind.LiveReward && slotIndex >= 0 && slotIndex < _eligibleRewardSlotCount;
                case InputCommandKind.DevConsoleClose:
                case InputCommandKind.DevConsoleSubmit:
                case InputCommandKind.DevConsoleHistoryPrevious:
                case InputCommandKind.DevConsoleHistoryNext:
                    return IsConsoleSupported() && effective == InputContextScopeKind.DevConsole;
                default:
                    return false;
            }
        }

        private bool IsCommandStillEligible(InputCommand command)
        {
            return IsCommandEligibleAtPerformance(command.Kind, command.SlotIndex);
        }

        private static bool ShouldRequireReleaseAfterCommand(InputCommandKind kind)
        {
            return kind == InputCommandKind.Pause
                || kind == InputCommandKind.ToggleConsole
                || kind == InputCommandKind.UiCancel
                || kind == InputCommandKind.UpgradeSelectChoice
                || kind == InputCommandKind.DevConsoleClose;
        }

        private static bool IsModalTransitionCommand(InputCommandKind kind)
        {
            return kind == InputCommandKind.ToggleConsole
                || kind == InputCommandKind.DevConsoleClose
                || kind == InputCommandKind.Pause
                || kind == InputCommandKind.UiCancel;
        }

        private static int GetCommandPriority(InputCommand command)
        {
            switch (command.Kind)
            {
                case InputCommandKind.ToggleConsole:
                    return 0;
                case InputCommandKind.DevConsoleClose:
                    return 1;
                case InputCommandKind.Pause:
                    return 2;
                case InputCommandKind.UiCancel:
                    return 3;
                case InputCommandKind.DevConsoleSubmit:
                    return 10;
                case InputCommandKind.DevConsoleHistoryPrevious:
                    return 11;
                case InputCommandKind.DevConsoleHistoryNext:
                    return 12;
                case InputCommandKind.UpgradeSelectChoice:
                    return 20 + Math.Max(0, command.SlotIndex);
                default:
                    return 100;
            }
        }

        private static void SortCommandsByPriority(List<InputCommand> commands)
        {
            for (int i = 1; i < commands.Count; i++)
            {
                InputCommand current = commands[i];
                int currentPriority = GetCommandPriority(current);
                int j = i - 1;

                while (j >= 0 && GetCommandPriority(commands[j]) > currentPriority)
                {
                    commands[j + 1] = commands[j];
                    j--;
                }

                commands[j + 1] = current;
            }
        }

        private InputContextScopeKind ResolveEffectiveContext()
        {
            if (_isLoading || !_isSceneReady)
            {
                return InputContextScopeKind.Loading;
            }

            InputContextScopeKind highest = InputContextScopeKind.None;
            int highestPrecedence = 0;

            for (int i = 0; i < _activeScopes.Count; i++)
            {
                InputContextScopeKind kind = _activeScopes[i].Token.Kind;
                int precedence = GetScopePrecedence(kind);
                if (precedence > highestPrecedence)
                {
                    highestPrecedence = precedence;
                    highest = kind;
                }
            }

            return highest;
        }

        private static int GetScopePrecedence(InputContextScopeKind kind)
        {
            switch (kind)
            {
                case InputContextScopeKind.Loading:
                    return 100;
                case InputContextScopeKind.DevConsole:
                    return 90;
                case InputContextScopeKind.Death:
                    return 80;
                case InputContextScopeKind.Dying:
                    return 70;
                case InputContextScopeKind.PauseChildPanel:
                    return 60;
                case InputContextScopeKind.Pause:
                    return 50;
                case InputContextScopeKind.LiveReward:
                    return 40;
                case InputContextScopeKind.Menu:
                    return 10;
                default:
                    return 0;
            }
        }

        private bool CanReadGameplayState()
        {
            return _isInitialized
                && !_isDisposed
                && !_isSuspended
                && _isApplicationFocused
                && IsDeviceAvailable
                && IsGameplayEligible;
        }

        private static bool IsConsoleSupported()
        {
            return Debug.isDebugBuild || Application.isEditor;
        }

        private void CancelActiveBrakeIfNeeded()
        {
            if (!_isBrakePressed)
            {
                return;
            }

            _isBrakePressed = false;
            OnBrakeCanceled?.Invoke(this, EventArgs.Empty);
        }

        private void DisableAllActionMaps()
        {
            if (_inputActions == null)
            {
                return;
            }

            var maps = _inputActions.actionMaps;
            for (int i = 0; i < maps.Count; i++)
            {
                maps[i].Disable();
            }
        }

        private static void SetActionEnabled(InputAction action, bool shouldEnable)
        {
            if (action == null)
            {
                return;
            }

            if (shouldEnable && !action.enabled)
            {
                action.Enable();
            }
            else if (!shouldEnable && action.enabled)
            {
                action.Disable();
            }
        }

        private void CapturePressedDigitalControlsForRelease()
        {
            CapturePressedActionControls(_brakeAction);
            CapturePressedActionControls(_interactAction);
            CapturePressedActionControls(_pauseAction);
            CapturePressedActionControls(_toggleConsoleAction);
            CapturePressedActionControls(_uiSubmitAction);
            CapturePressedActionControls(_uiCancelAction);
            CapturePressedActionControls(_selectChoice1Action);
            CapturePressedActionControls(_selectChoice2Action);
            CapturePressedActionControls(_selectChoice3Action);
            CapturePressedActionControls(_devConsoleCloseAction);
        }

        private void CapturePressedActionControls(InputAction action)
        {
            if (action == null)
            {
                return;
            }

            var controls = action.controls;
            for (int i = 0; i < controls.Count; i++)
            {
                InputControl control = controls[i];
                if (IsControlActuated(control))
                {
                    AddControlAwaitingRelease(control);
                }
            }
        }

        private bool IsActionTriggeredByHeldControlAwaitingRelease(InputAction action)
        {
            if (action == null || _controlsAwaitingRelease.Count == 0)
            {
                return false;
            }

            var controls = action.controls;
            for (int i = 0; i < controls.Count; i++)
            {
                InputControl control = controls[i];
                if (IsControlActuated(control) && IsControlAwaitingRelease(control))
                {
                    return true;
                }
            }

            return false;
        }

        private void MarkTriggeredControlsForRelease(InputAction action)
        {
            if (action == null)
            {
                return;
            }

            var controls = action.controls;
            for (int i = 0; i < controls.Count; i++)
            {
                InputControl control = controls[i];
                if (IsControlActuated(control))
                {
                    AddControlAwaitingRelease(control);
                }
            }
        }

        private void AddControlAwaitingRelease(InputControl control)
        {
            if (control == null)
            {
                return;
            }

            for (int i = 0; i < _controlsAwaitingRelease.Count; i++)
            {
                if (_controlsAwaitingRelease[i] == control)
                {
                    return;
                }
            }

            _controlsAwaitingRelease.Add(control);
        }

        private bool IsControlAwaitingRelease(InputControl control)
        {
            if (control == null)
            {
                return false;
            }

            for (int i = 0; i < _controlsAwaitingRelease.Count; i++)
            {
                if (_controlsAwaitingRelease[i] == control)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateControlsAwaitingRelease()
        {
            for (int i = _controlsAwaitingRelease.Count - 1; i >= 0; i--)
            {
                InputControl control = _controlsAwaitingRelease[i];
                if (control == null || control.device == null || !control.device.added || !IsControlActuated(control))
                {
                    _controlsAwaitingRelease.RemoveAt(i);
                }
            }
        }

        private bool IsControlActuated(InputControl control)
        {
            if (control == null || control.device == null || !control.device.added)
            {
                return false;
            }

            if (control is ButtonControl button)
            {
                return button.isPressed;
            }

            if (control is AxisControl axis)
            {
                return Mathf.Abs(axis.ReadValue()) >= _runtimeConfig.StickActivityRearmThreshold;
            }

            if (control is StickControl stick)
            {
                return stick.ReadValue().magnitude >= _runtimeConfig.StickActivityRearmThreshold;
            }

            if (control is DpadControl dpad)
            {
                return dpad.ReadValue().sqrMagnitude > 0.01f;
            }

            return control.IsPressed();
        }

        private void HandleStartLoadingScene(object sender, ValueEventArgs<GameScene> args)
        {
            _isLoading = true;
            _isSceneReady = false;
            _eligibleRewardSlotCount = 0;
            _sceneEpoch++;
            _contextGeneration++;

            _activeScopes.Clear();
            _pendingCommands.Clear();
            _dispatchBuffer.Clear();

            CancelActiveBrakeIfNeeded();
            CapturePressedDigitalControlsForRelease();
            ReapplyPolicy();
            OnContextChanged?.Invoke();
        }

        private void HandleSceneLoaded(object sender, ValueEventArgs<GameScene> args)
        {
            _isLoading = false;
            _contextGeneration++;
            ReapplyPolicy();
            OnContextChanged?.Invoke();
        }

        private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!_isInitialized || _isDisposed)
            {
                return;
            }

            if (device is Joystick && !(device is Gamepad))
            {
                if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)
                {
                    LogUnsupportedJoystick(device);
                }
                else if (change == InputDeviceChange.Removed)
                {
                    _loggedUnsupportedJoysticks.Remove(device.deviceId);
                }

                return;
            }

            bool emitChange = false;

            if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected)
            {
                if (device == _activeGamepad)
                {
                    CancelActiveBrakeIfNeeded();
                }

                CapturePressedDigitalControlsForRelease();
                RemoveStickState(device.deviceId);
                emitChange = RefreshDeviceAvailability(switchIfUnavailable: true);
            }
            else if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)
            {
                CapturePressedDigitalControlsForRelease();
                InitializeStickStateForDevice(device as Gamepad);
                emitChange = RefreshDeviceAvailability(switchIfUnavailable: true);
            }
            else if (change == InputDeviceChange.ConfigurationChanged)
            {
                emitChange = true;
            }

            if (emitChange)
            {
                SyncSwitchFaceButtonOverrides();
                OnDeviceChanged?.Invoke();
            }
        }

        private bool RefreshDeviceAvailability(bool switchIfUnavailable)
        {
            bool previousHasKm = _hasKeyboardOrMouse;
            bool previousHasPad = _hasSupportedGamepad;
            InputScheme previousScheme = _activeScheme;
            InputDeviceFamily previousFamily = _effectiveFamily;
            Gamepad previousGamepad = _activeGamepad;

            Keyboard currentKeyboard = Keyboard.current;
            Mouse currentMouse = Mouse.current;
            _activeKeyboard = currentKeyboard;
            _hasKeyboardOrMouse = (currentKeyboard != null && currentKeyboard.added)
                || (currentMouse != null && currentMouse.added);

            Gamepad firstAvailablePad = null;
            var gamepads = Gamepad.all;
            for (int i = 0; i < gamepads.Count; i++)
            {
                Gamepad pad = gamepads[i];
                if (pad != null && pad.added)
                {
                    firstAvailablePad = pad;
                    break;
                }
            }

            _hasSupportedGamepad = firstAvailablePad != null;

            if (_activeGamepad != null && !_activeGamepad.added)
            {
                _activeGamepad = null;
            }

            if (switchIfUnavailable)
            {
                if (_activeScheme == InputScheme.Gamepad)
                {
                    if (_activeGamepad == null)
                    {
                        if (firstAvailablePad != null)
                        {
                            _activeGamepad = firstAvailablePad;
                            _effectiveFamily = ResolveGamepadPresentationFamily(firstAvailablePad);
                        }
                        else if (_hasKeyboardOrMouse)
                        {
                            _activeScheme = InputScheme.KeyboardMouse;
                            _effectiveFamily = InputDeviceFamily.KeyboardMouse;
                        }
                        else
                        {
                            _effectiveFamily = InputDeviceFamily.Unavailable;
                        }
                    }
                    else
                    {
                        _effectiveFamily = ResolveGamepadPresentationFamily(_activeGamepad);
                    }
                }
                else
                {
                    if (_hasKeyboardOrMouse)
                    {
                        _activeScheme = InputScheme.KeyboardMouse;
                        _effectiveFamily = InputDeviceFamily.KeyboardMouse;
                    }
                    else if (firstAvailablePad != null)
                    {
                        _activeScheme = InputScheme.Gamepad;
                        _activeGamepad = firstAvailablePad;
                        _effectiveFamily = ResolveGamepadPresentationFamily(firstAvailablePad);
                    }
                    else
                    {
                        _effectiveFamily = InputDeviceFamily.Unavailable;
                    }
                }
            }
            else if (_activeScheme == InputScheme.Gamepad && _activeGamepad != null)
            {
                _effectiveFamily = ResolveGamepadPresentationFamily(_activeGamepad);
            }

            return previousHasKm != _hasKeyboardOrMouse
                || previousHasPad != _hasSupportedGamepad
                || previousScheme != _activeScheme
                || previousFamily != _effectiveFamily
                || previousGamepad != _activeGamepad;
        }

        private InputDeviceFamily ResolveGamepadPresentationFamily(Gamepad gamepad)
        {
            if (gamepad == null)
            {
                return InputDeviceFamily.Unavailable;
            }

            switch (_runtimeConfig.ControllerFamilyOverride)
            {
                case InputPresentationOverride.Xbox:
                    return InputDeviceFamily.Xbox;
                case InputPresentationOverride.PlayStation:
                    return InputDeviceFamily.PlayStation;
                case InputPresentationOverride.Switch:
                    return InputDeviceFamily.Switch;
                case InputPresentationOverride.GenericGamepad:
                    return InputDeviceFamily.GenericGamepad;
            }

            if (gamepad is DualShockGamepad)
            {
                return InputDeviceFamily.PlayStation;
            }

#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX || UNITY_WSA
            if (gamepad is SwitchProControllerHID)
            {
                return InputDeviceFamily.Switch;
            }
#endif

            if (gamepad is XInputController)
            {
                return InputDeviceFamily.Xbox;
            }

            if (IsPlayStationDevice(gamepad))
            {
                return InputDeviceFamily.PlayStation;
            }

            if (IsSwitchDevice(gamepad))
            {
                return InputDeviceFamily.Switch;
            }

            if (IsXboxDevice(gamepad))
            {
                return InputDeviceFamily.Xbox;
            }

            return InputDeviceFamily.GenericGamepad;
        }

        private void SyncSwitchFaceButtonOverrides()
        {
            bool shouldUseSwitchLayout = _effectiveFamily == InputDeviceFamily.Switch;
            if (shouldUseSwitchLayout == _isSwitchFaceButtonLayoutActive)
            {
                return;
            }

            CancelActiveBrakeIfNeeded();
            _isSwitchFaceButtonLayoutActive = shouldUseSwitchLayout;

            if (shouldUseSwitchLayout)
            {
                SetGamepadBindingOverride(_brakeAction, InputConstants.GAMEPAD_BUTTON_EAST_PATH, InputConstants.GAMEPAD_BUTTON_SOUTH_PATH);
                SetGamepadBindingOverride(_interactAction, InputConstants.GAMEPAD_BUTTON_SOUTH_PATH, InputConstants.GAMEPAD_BUTTON_EAST_PATH);
                SetGamepadBindingOverride(_uiSubmitAction, InputConstants.GAMEPAD_BUTTON_SOUTH_PATH, InputConstants.GAMEPAD_BUTTON_EAST_PATH);
                SetGamepadBindingOverride(_uiCancelAction, InputConstants.GAMEPAD_BUTTON_EAST_PATH, InputConstants.GAMEPAD_BUTTON_SOUTH_PATH);
                SetGamepadBindingOverride(_selectChoice1Action, InputConstants.GAMEPAD_BUTTON_NORTH_PATH, InputConstants.GAMEPAD_BUTTON_WEST_PATH);
                SetGamepadBindingOverride(_selectChoice2Action, InputConstants.GAMEPAD_BUTTON_WEST_PATH, InputConstants.GAMEPAD_BUTTON_NORTH_PATH);
                SetGamepadBindingOverride(_selectChoice3Action, InputConstants.GAMEPAD_BUTTON_SOUTH_PATH, InputConstants.GAMEPAD_BUTTON_EAST_PATH);
                SetGamepadBindingOverride(_devConsoleCloseAction, InputConstants.GAMEPAD_BUTTON_EAST_PATH, InputConstants.GAMEPAD_BUTTON_SOUTH_PATH);
            }
            else
            {
                ClearSwitchFaceButtonOverrides();
            }
        }

        private void ClearSwitchFaceButtonOverrides()
        {
            _isSwitchFaceButtonLayoutActive = false;

            SetGamepadBindingOverride(_brakeAction, InputConstants.GAMEPAD_BUTTON_EAST_PATH, null);
            SetGamepadBindingOverride(_interactAction, InputConstants.GAMEPAD_BUTTON_SOUTH_PATH, null);
            SetGamepadBindingOverride(_uiSubmitAction, InputConstants.GAMEPAD_BUTTON_SOUTH_PATH, null);
            SetGamepadBindingOverride(_uiCancelAction, InputConstants.GAMEPAD_BUTTON_EAST_PATH, null);
            SetGamepadBindingOverride(_selectChoice1Action, InputConstants.GAMEPAD_BUTTON_NORTH_PATH, null);
            SetGamepadBindingOverride(_selectChoice2Action, InputConstants.GAMEPAD_BUTTON_WEST_PATH, null);
            SetGamepadBindingOverride(_selectChoice3Action, InputConstants.GAMEPAD_BUTTON_SOUTH_PATH, null);
            SetGamepadBindingOverride(_devConsoleCloseAction, InputConstants.GAMEPAD_BUTTON_EAST_PATH, null);
        }

        private static void SetGamepadBindingOverride(InputAction action, string canonicalPath, string overridePath)
        {
            if (action == null)
            {
                return;
            }

            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding binding = bindings[i];
                if (binding.isComposite || binding.isPartOfComposite)
                {
                    continue;
                }

                if (!BindingMatchesGroup(binding.groups, InputConstants.GAMEPAD_GROUP))
                {
                    continue;
                }

                if (!string.Equals(binding.path, canonicalPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(overridePath))
                {
                    action.RemoveBindingOverride(i);
                }
                else
                {
                    action.ApplyBindingOverride(i, overridePath);
                }
            }
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

        private void SyncCursorVisibility()
        {
            if (!_isApplicationFocused || _isSuspended)
            {
                return;
            }

            bool shouldShowCursor = _activeScheme != InputScheme.Gamepad;
            if (Cursor.visible != shouldShowCursor)
            {
                Cursor.visible = shouldShowCursor;
            }
        }

        private static bool IsPlayStationDevice(Gamepad gamepad)
        {
            string layout = gamepad.layout;
            UnityEngine.InputSystem.Layouts.InputDeviceDescription description = gamepad.description;

            if (ContainsOrdinalIgnoreCase(layout, "DualShock")
                || ContainsOrdinalIgnoreCase(layout, "DualSense")
                || ContainsOrdinalIgnoreCase(layout, "PlayStation"))
            {
                return true;
            }

            if (ContainsOrdinalIgnoreCase(description.manufacturer, "Sony")
                || ContainsOrdinalIgnoreCase(description.manufacturer, "PlayStation"))
            {
                return true;
            }

            if (ContainsOrdinalIgnoreCase(description.product, "DualSense")
                || ContainsOrdinalIgnoreCase(description.product, "DualShock")
                || ContainsOrdinalIgnoreCase(description.product, "PlayStation")
                || ContainsOrdinalIgnoreCase(description.product, "PS5")
                || ContainsOrdinalIgnoreCase(description.product, "PS4")
                || ContainsOrdinalIgnoreCase(description.product, "PS3"))
            {
                return true;
            }

            if (HasVendorId(description.capabilities, 1356, "054c"))
            {
                return true;
            }

            return ContainsOrdinalIgnoreCase(description.product, "Wireless Controller")
                && ContainsOrdinalIgnoreCase(description.interfaceName, "HID")
                && !ContainsOrdinalIgnoreCase(description.manufacturer, "Microsoft")
                && !ContainsOrdinalIgnoreCase(description.manufacturer, "Nintendo");
        }

        private static bool IsSwitchDevice(Gamepad gamepad)
        {
            UnityEngine.InputSystem.Layouts.InputDeviceDescription description = gamepad.description;
            return ContainsOrdinalIgnoreCase(gamepad.layout, "Switch")
                || ContainsOrdinalIgnoreCase(description.manufacturer, "Nintendo")
                || ContainsOrdinalIgnoreCase(description.product, "Pro Controller")
                || ContainsOrdinalIgnoreCase(description.product, "Joy-Con")
                || HasVendorId(description.capabilities, 1406, "057e");
        }

        private static bool IsXboxDevice(Gamepad gamepad)
        {
            UnityEngine.InputSystem.Layouts.InputDeviceDescription description = gamepad.description;
            return ContainsOrdinalIgnoreCase(gamepad.layout, "XInput")
                || ContainsOrdinalIgnoreCase(gamepad.layout, "Xbox")
                || ContainsOrdinalIgnoreCase(description.manufacturer, "Microsoft")
                || ContainsOrdinalIgnoreCase(description.product, "Xbox")
                || HasVendorId(description.capabilities, 1118, "045e");
        }

        private static bool HasVendorId(string capabilitiesJson, int decimalVendorId, string hexVendorId)
        {
            if (string.IsNullOrEmpty(capabilitiesJson))
            {
                return false;
            }

            string decimalToken = $"\"vendorId\":{decimalVendorId}";
            string spacedDecimalToken = $"\"vendorId\": {decimalVendorId}";
            return capabilitiesJson.IndexOf(decimalToken, StringComparison.OrdinalIgnoreCase) >= 0
                || capabilitiesJson.IndexOf(spacedDecimalToken, StringComparison.OrdinalIgnoreCase) >= 0
                || capabilitiesJson.IndexOf(hexVendorId, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsOrdinalIgnoreCase(string source, string value)
        {
            return !string.IsNullOrEmpty(source)
                && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool DetectMeaningfulKeyboardOrMouseActivity()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.added && keyboard.anyKey.wasPressedThisFrame)
            {
                return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.added)
            {
                return false;
            }

            if (mouse.leftButton.wasPressedThisFrame
                || mouse.rightButton.wasPressedThisFrame
                || mouse.middleButton.wasPressedThisFrame
                || mouse.forwardButton.wasPressedThisFrame
                || mouse.backButton.wasPressedThisFrame)
            {
                return true;
            }

            if (mouse.scroll.ReadValue().sqrMagnitude > 0.01f)
            {
                return true;
            }

            Vector2 currentPos = mouse.position.ReadValue();
            float now = Time.unscaledTime;

            if (!_hasMouseWindowAnchor || now - _mouseWindowStartTime > _runtimeConfig.MouseActivityWindowSeconds)
            {
                _mouseWindowAnchor = currentPos;
                _mouseWindowStartTime = now;
                _hasMouseWindowAnchor = true;
                return false;
            }

            if (Vector2.Distance(currentPos, _mouseWindowAnchor) >= _runtimeConfig.MouseActivityPixelThreshold)
            {
                _mouseWindowAnchor = currentPos;
                _mouseWindowStartTime = now;
                return true;
            }

            return false;
        }

        private bool DetectMeaningfulGamepadActivity(out Gamepad activatedGamepad)
        {
            var gamepads = Gamepad.all;
            for (int i = 0; i < gamepads.Count; i++)
            {
                Gamepad gamepad = gamepads[i];
                if (gamepad == null || !gamepad.added)
                {
                    continue;
                }

                if (HasMeaningfulGamepadInput(gamepad))
                {
                    activatedGamepad = gamepad;
                    return true;
                }
            }

            activatedGamepad = null;
            return false;
        }

        private bool HasMeaningfulGamepadInput(Gamepad gamepad)
        {
            if (gamepad.buttonSouth.wasPressedThisFrame
                || gamepad.buttonNorth.wasPressedThisFrame
                || gamepad.buttonEast.wasPressedThisFrame
                || gamepad.buttonWest.wasPressedThisFrame
                || gamepad.leftShoulder.wasPressedThisFrame
                || gamepad.rightShoulder.wasPressedThisFrame
                || gamepad.startButton.wasPressedThisFrame
                || gamepad.selectButton.wasPressedThisFrame
                || gamepad.leftStickButton.wasPressedThisFrame
                || gamepad.rightStickButton.wasPressedThisFrame
                || gamepad.dpad.up.wasPressedThisFrame
                || gamepad.dpad.down.wasPressedThisFrame
                || gamepad.dpad.left.wasPressedThisFrame
                || gamepad.dpad.right.wasPressedThisFrame)
            {
                return true;
            }

            GamepadStickActivityState state = GetOrCreateStickState(gamepad.deviceId);
            float enterThreshold = _runtimeConfig.StickActivityEnterThreshold;
            float rearmThreshold = _runtimeConfig.StickActivityRearmThreshold;
            float deltaThreshold = _runtimeConfig.StickActivityDeltaThreshold;

            bool leftTriggerActive = CheckTriggerMeaningful(gamepad.leftTrigger.ReadValue(), ref state.LeftTriggerRearmed);
            bool rightTriggerActive = CheckTriggerMeaningful(gamepad.rightTrigger.ReadValue(), ref state.RightTriggerRearmed);
            bool leftStickActive = CheckStickMeaningful(gamepad.leftStick.ReadValue(), enterThreshold, rearmThreshold, deltaThreshold, ref state.LeftRearmed, ref state.LastLeftStick);
            bool rightStickActive = CheckStickMeaningful(gamepad.rightStick.ReadValue(), enterThreshold, rearmThreshold, deltaThreshold, ref state.RightRearmed, ref state.LastRightStick);

            return leftTriggerActive || rightTriggerActive || leftStickActive || rightStickActive;
        }

        private static bool CheckTriggerMeaningful(float value, ref bool rearmed)
        {
            if (value <= 0.25f)
            {
                rearmed = true;
                return false;
            }

            if (value >= 0.5f && rearmed)
            {
                rearmed = false;
                return true;
            }

            return false;
        }

        private static bool CheckStickMeaningful(
            Vector2 current,
            float enterThreshold,
            float rearmThreshold,
            float deltaThreshold,
            ref bool rearmed,
            ref Vector2 lastVector)
        {
            float magnitude = current.magnitude;
            if (magnitude <= rearmThreshold)
            {
                rearmed = true;
                lastVector = current;
                return false;
            }

            if (magnitude >= enterThreshold)
            {
                bool meaningful = rearmed || Vector2.Distance(current, lastVector) >= deltaThreshold;
                if (meaningful)
                {
                    rearmed = false;
                    lastVector = current;
                    return true;
                }
            }

            return false;
        }

        private GamepadStickActivityState GetOrCreateStickState(int deviceId)
        {
            for (int i = 0; i < _stickStates.Count; i++)
            {
                if (_stickStates[i].DeviceId == deviceId)
                {
                    return _stickStates[i];
                }
            }

            GamepadStickActivityState created = new GamepadStickActivityState
            {
                DeviceId = deviceId,
                LeftRearmed = false,
                RightRearmed = false,
                LeftTriggerRearmed = false,
                RightTriggerRearmed = false
            };
            _stickStates.Add(created);
            return created;
        }

        private void InitializeStickStateForDevice(Gamepad gamepad)
        {
            if (gamepad == null)
            {
                return;
            }

            GamepadStickActivityState state = GetOrCreateStickState(gamepad.deviceId);
            Vector2 left = gamepad.leftStick.ReadValue();
            Vector2 right = gamepad.rightStick.ReadValue();
            float rearmThreshold = _runtimeConfig.StickActivityRearmThreshold;

            state.LeftRearmed = left.magnitude <= rearmThreshold;
            state.RightRearmed = right.magnitude <= rearmThreshold;
            state.LeftTriggerRearmed = gamepad.leftTrigger.ReadValue() <= 0.25f;
            state.RightTriggerRearmed = gamepad.rightTrigger.ReadValue() <= 0.25f;
            state.LastLeftStick = left;
            state.LastRightStick = right;
        }

        private void ClearStickRearmStates()
        {
            for (int i = 0; i < _stickStates.Count; i++)
            {
                GamepadStickActivityState state = _stickStates[i];
                state.LeftRearmed = false;
                state.RightRearmed = false;
                state.LeftTriggerRearmed = false;
                state.RightTriggerRearmed = false;
            }

            var gamepads = Gamepad.all;
            for (int i = 0; i < gamepads.Count; i++)
            {
                InitializeStickStateForDevice(gamepads[i]);
            }
        }

        private void RemoveStickState(int deviceId)
        {
            for (int i = 0; i < _stickStates.Count; i++)
            {
                if (_stickStates[i].DeviceId == deviceId)
                {
                    _stickStates.RemoveAt(i);
                    return;
                }
            }
        }

        private void ResetMouseActivityAnchor()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.added)
            {
                _mouseWindowAnchor = mouse.position.ReadValue();
                _mouseWindowStartTime = Time.unscaledTime;
                _hasMouseWindowAnchor = true;
            }
            else
            {
                _hasMouseWindowAnchor = false;
            }
        }

        private void LogExistingUnsupportedJoysticks()
        {
            var joysticks = Joystick.all;
            for (int i = 0; i < joysticks.Count; i++)
            {
                LogUnsupportedJoystick(joysticks[i]);
            }
        }

        private void LogUnsupportedJoystick(InputDevice device)
        {
            if (device == null || _loggedUnsupportedJoysticks.Contains(device.deviceId))
            {
                return;
            }

            _loggedUnsupportedJoysticks.Add(device.deviceId);
            Debug.LogWarning(
                $"[{nameof(GameInputService)}] Connected generic Joystick device '{device.displayName}' ({device.layout}) is unsupported. Only Keyboard/Mouse and Gamepad-layout controllers are mapped.");
        }
    }
}
