using System;
using Assets.Scripts.Input;
using Assets.Scripts.Player;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Input
{
    public class GameplayInputCoordinator : MonoBehaviour
    {
        [Inject] private readonly IInputContexts _inputContexts = null;
        [Inject] private readonly IInputCommands _inputCommands = null;
        [Inject] private readonly IInputPromptProvider _promptProvider = null;
        [Inject] private readonly IPlayerManager _playerManager = null;

        [Header("UI Scope")]
        [SerializeField] private UiInputScope _uiInputScope;

        [Header("Pause UI Focus")]
        [SerializeField] private GameObject _pauseRoot;
        [SerializeField] private CanvasGroup _pauseRootCanvasGroup;
        [SerializeField] private Selectable _pauseFirstSelectable;
        [SerializeField] private MenuInputCoordinator.MenuPanelEntry[] _pauseChildPanels = Array.Empty<MenuInputCoordinator.MenuPanelEntry>();

        [Header("Death UI Focus")]
        [SerializeField] private Selectable _deathFirstSelectable;

        [Header("Prompt Views")]
        [SerializeField] private InputPromptView[] _pausePromptViews = Array.Empty<InputPromptView>();
        [SerializeField] private InputPromptView[] _deathPromptViews = Array.Empty<InputPromptView>();

        private InputScopeToken _dyingScopeToken;
        private InputScopeToken _pauseChildScopeToken;
        private InputContextScopeKind _previousContext = InputContextScopeKind.None;
        private Selectable _lastValidPauseSelectable;
        private int _activePauseChildIndex = -1;
        private bool _isInitialized;

        public UiInputScope UiInputScope
        {
            get { return _uiInputScope; }
        }

        public Selectable PauseFirstSelectable
        {
            get { return _pauseFirstSelectable; }
        }

        public Selectable DeathFirstSelectable
        {
            get { return _deathFirstSelectable; }
        }

        public MenuInputCoordinator.MenuPanelEntry[] PauseChildPanels
        {
            get { return _pauseChildPanels; }
        }

        private void Awake()
        {
            if (_inputContexts == null || _inputCommands == null || _promptProvider == null || _playerManager == null)
            {
                throw new InvalidOperationException($"[{nameof(GameplayInputCoordinator)}] Required dependencies were not injected on '{name}'.");
            }

            if (_uiInputScope == null || _pauseFirstSelectable == null || _deathFirstSelectable == null)
            {
                throw new InvalidOperationException($"[{nameof(GameplayInputCoordinator)}] Required UI references are unassigned on '{name}'.");
            }

            _uiInputScope.SetSelectionResolver(ResolveDefaultSelectable);
            _isInitialized = true;
        }

        private void OnEnable()
        {
            if (!_isInitialized)
            {
                return;
            }

            _inputCommands.OnCommand += HandleInputCommand;
            _inputContexts.OnContextChanged += HandleContextChanged;
            BindPromptViews(_pausePromptViews);
            BindPromptViews(_deathPromptViews);
        }

        private void Start()
        {
            if (_playerManager.Health != null)
            {
                _playerManager.Health.OnNoHealth += HandlePlayerNoHealth;
                if (!_playerManager.Health.IsAlive())
                {
                    AcquireDyingScopeIfNeeded();
                }
            }

            _inputContexts.RegisterSceneReady();
            _previousContext = _inputContexts.EffectiveContext;
        }

        private void Update()
        {
            if (!_isInitialized)
            {
                return;
            }

            SyncPauseChildPanels();
            CaptureLastValidPauseSelectable();
        }

        private void OnDisable()
        {
            if (!_isInitialized)
            {
                return;
            }

            _inputCommands.OnCommand -= HandleInputCommand;
            _inputContexts.OnContextChanged -= HandleContextChanged;
        }

        private void OnDestroy()
        {
            if (_playerManager != null && _playerManager.Health != null)
            {
                _playerManager.Health.OnNoHealth -= HandlePlayerNoHealth;
            }

            if (_pauseChildScopeToken.IsValid)
            {
                _inputContexts?.ReleaseScope(_pauseChildScopeToken);
                _pauseChildScopeToken = default;
            }

            if (_dyingScopeToken.IsValid)
            {
                _inputContexts?.ReleaseScope(_dyingScopeToken);
                _dyingScopeToken = default;
            }
        }

        public bool TryHandlePauseCancel()
        {
            if (_uiInputScope.TryHandleWidgetCancel())
            {
                return true;
            }

            if (_activePauseChildIndex >= 0 && _activePauseChildIndex < _pauseChildPanels.Length)
            {
                MenuInputCoordinator.MenuPanelEntry child = _pauseChildPanels[_activePauseChildIndex];
                if (child.PanelRoot != null && child.PanelRoot.activeSelf)
                {
                    child.PanelRoot.SetActive(false);
                }

                _inputContexts.ConsumeActiveControlsUntilRelease();
                SyncPauseChildPanels();
                return true;
            }

            return false;
        }

        private void HandlePlayerNoHealth(object sender, EventArgs args)
        {
            AcquireDyingScopeIfNeeded();
        }

        private void AcquireDyingScopeIfNeeded()
        {
            if (!_dyingScopeToken.IsValid)
            {
                _dyingScopeToken = _inputContexts.AcquireScope(InputContextScopeKind.Dying, GetEntityId().GetHashCode());
            }
        }

        private void HandleInputCommand(InputCommand command)
        {
            if (command.Kind != InputCommandKind.UiCancel)
            {
                return;
            }

            if (_inputContexts.EffectiveContext == InputContextScopeKind.PauseChildPanel)
            {
                TryHandlePauseCancel();
            }
            else if (_inputContexts.EffectiveContext == InputContextScopeKind.Pause)
            {
                _uiInputScope.TryHandleWidgetCancel();
            }
        }

        private void HandleContextChanged()
        {
            InputContextScopeKind current = _inputContexts.EffectiveContext;
            if (current == _previousContext)
            {
                return;
            }

            InputContextScopeKind previous = _previousContext;
            _previousContext = current;

            if (current == InputContextScopeKind.Pause && previous != InputContextScopeKind.PauseChildPanel)
            {
                _uiInputScope.SetSelectedSafe(_pauseFirstSelectable);
            }
            else if (current == InputContextScopeKind.Pause && previous == InputContextScopeKind.PauseChildPanel)
            {
                Selectable restoreTarget = UiInputScope.IsValidSelectable(_lastValidPauseSelectable)
                    ? _lastValidPauseSelectable
                    : _pauseFirstSelectable;
                _uiInputScope.SetSelectedSafe(restoreTarget);
            }
            else if (current == InputContextScopeKind.Death)
            {
                _uiInputScope.SetSelectedSafe(_deathFirstSelectable);
            }
        }

        private void SyncPauseChildPanels()
        {
            if (_pauseChildPanels == null || _pauseChildPanels.Length == 0)
            {
                return;
            }

            int detected = -1;
            for (int i = _pauseChildPanels.Length - 1; i >= 0; i--)
            {
                GameObject root = _pauseChildPanels[i].PanelRoot;
                if (root != null && root.activeInHierarchy)
                {
                    detected = i;
                    break;
                }
            }

            if (detected == _activePauseChildIndex)
            {
                return;
            }

            int previousIndex = _activePauseChildIndex;
            _activePauseChildIndex = detected;

            bool hasChild = _activePauseChildIndex >= 0;
            if (_pauseRootCanvasGroup != null)
            {
                _pauseRootCanvasGroup.interactable = !hasChild;
                _pauseRootCanvasGroup.blocksRaycasts = !hasChild;
            }

            if (hasChild)
            {
                if (!_pauseChildScopeToken.IsValid)
                {
                    _pauseChildScopeToken = _inputContexts.AcquireScope(InputContextScopeKind.PauseChildPanel, GetEntityId().GetHashCode());
                }

                Selectable childDefault = _pauseChildPanels[_activePauseChildIndex].DefaultSelectable;
                if (childDefault != null)
                {
                    _uiInputScope.SetSelectedSafe(childDefault);
                }
            }
            else
            {
                if (_pauseChildScopeToken.IsValid)
                {
                    _inputContexts.ReleaseScope(_pauseChildScopeToken);
                    _pauseChildScopeToken = default;
                }

                if (previousIndex >= 0 && _inputContexts.EffectiveContext == InputContextScopeKind.Pause)
                {
                    Selectable returnTarget = ResolvePauseReturnSelectable(previousIndex);
                    _uiInputScope.SetSelectedSafe(returnTarget);
                }
            }
        }

        private void CaptureLastValidPauseSelectable()
        {
            if (_activePauseChildIndex >= 0 || _pauseRoot == null || _inputContexts.EffectiveContext != InputContextScopeKind.Pause)
            {
                return;
            }

            GameObject selected = _uiInputScope.EventSystem.currentSelectedGameObject;
            if (selected == null || !selected.transform.IsChildOf(_pauseRoot.transform))
            {
                return;
            }

            Selectable selectable = selected.GetComponent<Selectable>();
            if (UiInputScope.IsValidSelectable(selectable))
            {
                _lastValidPauseSelectable = selectable;
            }
        }

        private Selectable ResolveDefaultSelectable()
        {
            InputContextScopeKind current = _inputContexts.EffectiveContext;
            if (current == InputContextScopeKind.PauseChildPanel && _activePauseChildIndex >= 0 && _activePauseChildIndex < _pauseChildPanels.Length)
            {
                Selectable childDefault = _pauseChildPanels[_activePauseChildIndex].DefaultSelectable;
                if (UiInputScope.IsValidSelectable(childDefault))
                {
                    return childDefault;
                }
            }

            if (current == InputContextScopeKind.Pause)
            {
                return UiInputScope.IsValidSelectable(_lastValidPauseSelectable)
                    ? _lastValidPauseSelectable
                    : _pauseFirstSelectable;
            }

            if (current == InputContextScopeKind.Death)
            {
                return _deathFirstSelectable;
            }

            return null;
        }

        private Selectable ResolvePauseReturnSelectable(int closedChildIndex)
        {
            if (UiInputScope.IsValidSelectable(_lastValidPauseSelectable))
            {
                return _lastValidPauseSelectable;
            }

            if (closedChildIndex >= 0 && closedChildIndex < _pauseChildPanels.Length)
            {
                Selectable parentReturn = _pauseChildPanels[closedChildIndex].ParentReturnSelectable;
                if (UiInputScope.IsValidSelectable(parentReturn))
                {
                    return parentReturn;
                }
            }

            return _pauseFirstSelectable;
        }

        private void BindPromptViews(InputPromptView[] views)
        {
            if (views == null)
            {
                return;
            }

            for (int i = 0; i < views.Length; i++)
            {
                InputPromptView view = views[i];
                if (view != null && view.DefaultActionId != InputActionId.None)
                {
                    view.Bind(_promptProvider);
                }
            }
        }
    }
}
