using System;
using Assets.Scripts.Input;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Input
{
    public class MenuInputCoordinator : MonoBehaviour
    {
        [Serializable]
        public struct MenuPanelEntry
        {
            [SerializeField] private GameObject _panelRoot;
            [SerializeField] private Selectable _defaultSelectable;
            [SerializeField] private Selectable _parentReturnSelectable;

            public GameObject PanelRoot
            {
                get { return _panelRoot; }
            }

            public Selectable DefaultSelectable
            {
                get { return _defaultSelectable; }
            }

            public Selectable ParentReturnSelectable
            {
                get { return _parentReturnSelectable; }
            }
        }

        [Inject] private readonly IInputContexts _inputContexts = null;
        [Inject] private readonly IInputCommands _inputCommands = null;
        [Inject] private readonly IInputPromptProvider _promptProvider = null;

        [Header("UI Scope")]
        [SerializeField] private UiInputScope _uiInputScope;

        [Header("Main Menu Root")]
        [SerializeField] private GameObject _mainMenuRoot;
        [SerializeField] private CanvasGroup _mainMenuCanvasGroup;
        [SerializeField] private Selectable _mainMenuFirstSelectable;

        [Header("Child Panels")]
        [SerializeField] private MenuPanelEntry[] _childPanels = Array.Empty<MenuPanelEntry>();

        [Header("Prompt Views")]
        [SerializeField] private InputPromptView[] _backPromptViews = Array.Empty<InputPromptView>();

        private InputScopeToken _menuScopeToken;
        private Selectable _lastValidRootSelectable;
        private int _activeChildPanelIndex = -1;
        private bool _isInitialized;

        public GameObject MainMenuRoot
        {
            get { return _mainMenuRoot; }
        }

        public Selectable MainMenuFirstSelectable
        {
            get { return _mainMenuFirstSelectable; }
        }

        public UiInputScope UiInputScope
        {
            get { return _uiInputScope; }
        }

        public MenuPanelEntry[] ChildPanels
        {
            get { return _childPanels; }
        }

        private void Awake()
        {
            if (_inputContexts == null || _inputCommands == null || _promptProvider == null)
            {
                throw new InvalidOperationException($"[{nameof(MenuInputCoordinator)}] Required input services were not injected on '{name}'.");
            }

            if (_uiInputScope == null)
            {
                throw new InvalidOperationException($"[{nameof(MenuInputCoordinator)}] _uiInputScope is unassigned on '{name}'.");
            }

            if (_mainMenuRoot == null || _mainMenuFirstSelectable == null)
            {
                throw new InvalidOperationException($"[{nameof(MenuInputCoordinator)}] Main menu root or first selectable is unassigned on '{name}'.");
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
            BindPromptViews();
        }

        private void Start()
        {
            _menuScopeToken = _inputContexts.AcquireScope(InputContextScopeKind.Menu, GetEntityId().GetHashCode());
            _inputContexts.RegisterSceneReady();
            SyncActivePanelState(forceSelect: true);
        }

        private void Update()
        {
            if (!_isInitialized)
            {
                return;
            }

            SyncActivePanelState(forceSelect: false);
            CaptureLastValidRootSelectable();
        }

        private void OnDisable()
        {
            if (!_isInitialized)
            {
                return;
            }

            _inputCommands.OnCommand -= HandleInputCommand;
        }

        private void OnDestroy()
        {
            if (_menuScopeToken.IsValid)
            {
                _inputContexts?.ReleaseScope(_menuScopeToken);
                _menuScopeToken = default;
            }
        }

        public void CloseActiveChildPanel()
        {
            if (_activeChildPanelIndex < 0 || _activeChildPanelIndex >= _childPanels.Length)
            {
                return;
            }

            MenuPanelEntry entry = _childPanels[_activeChildPanelIndex];
            if (entry.PanelRoot != null && entry.PanelRoot.activeSelf)
            {
                entry.PanelRoot.SetActive(false);
            }

            _inputContexts.ConsumeActiveControlsUntilRelease();
            SyncActivePanelState(forceSelect: true);
        }

        private void HandleInputCommand(InputCommand command)
        {
            if (command.Kind != InputCommandKind.UiCancel)
            {
                return;
            }

            if (_uiInputScope.TryHandleWidgetCancel())
            {
                return;
            }

            if (_activeChildPanelIndex >= 0)
            {
                CloseActiveChildPanel();
            }
        }

        private void SyncActivePanelState(bool forceSelect)
        {
            int detectedIndex = DetectTopActiveChildPanel();
            if (detectedIndex == _activeChildPanelIndex && !forceSelect)
            {
                return;
            }

            int previousIndex = _activeChildPanelIndex;
            _activeChildPanelIndex = detectedIndex;

            bool hasChild = _activeChildPanelIndex >= 0;
            if (_mainMenuCanvasGroup != null)
            {
                _mainMenuCanvasGroup.interactable = !hasChild;
                _mainMenuCanvasGroup.blocksRaycasts = !hasChild;
            }

            if (hasChild)
            {
                MenuPanelEntry child = _childPanels[_activeChildPanelIndex];
                if (child.DefaultSelectable != null)
                {
                    _uiInputScope.SetSelectedSafe(child.DefaultSelectable);
                }
            }
            else if (previousIndex >= 0 || forceSelect)
            {
                Selectable restoreTarget = ResolveRootReturnSelectable(previousIndex);
                _uiInputScope.SetSelectedSafe(restoreTarget);
            }
        }

        private int DetectTopActiveChildPanel()
        {
            for (int i = _childPanels.Length - 1; i >= 0; i--)
            {
                GameObject panelRoot = _childPanels[i].PanelRoot;
                if (panelRoot != null && panelRoot.activeInHierarchy)
                {
                    return i;
                }
            }

            return -1;
        }

        private void CaptureLastValidRootSelectable()
        {
            if (_activeChildPanelIndex >= 0)
            {
                return;
            }

            GameObject selected = _uiInputScope.EventSystem.currentSelectedGameObject;
            if (selected == null || !selected.transform.IsChildOf(_mainMenuRoot.transform))
            {
                return;
            }

            Selectable selectable = selected.GetComponent<Selectable>();
            if (UiInputScope.IsValidSelectable(selectable))
            {
                _lastValidRootSelectable = selectable;
            }
        }

        private Selectable ResolveDefaultSelectable()
        {
            if (_activeChildPanelIndex >= 0 && _activeChildPanelIndex < _childPanels.Length)
            {
                Selectable childDefault = _childPanels[_activeChildPanelIndex].DefaultSelectable;
                if (UiInputScope.IsValidSelectable(childDefault))
                {
                    return childDefault;
                }
            }

            return ResolveRootReturnSelectable(-1);
        }

        private Selectable ResolveRootReturnSelectable(int closedChildIndex)
        {
            if (UiInputScope.IsValidSelectable(_lastValidRootSelectable))
            {
                return _lastValidRootSelectable;
            }

            if (closedChildIndex >= 0 && closedChildIndex < _childPanels.Length)
            {
                Selectable parentReturn = _childPanels[closedChildIndex].ParentReturnSelectable;
                if (UiInputScope.IsValidSelectable(parentReturn))
                {
                    return parentReturn;
                }
            }

            return _mainMenuFirstSelectable;
        }

        private void BindPromptViews()
        {
            if (_backPromptViews == null)
            {
                return;
            }

            for (int i = 0; i < _backPromptViews.Length; i++)
            {
                InputPromptView view = _backPromptViews[i];
                if (view != null)
                {
                    InputActionId actionId = view.DefaultActionId != InputActionId.None
                        ? view.DefaultActionId
                        : InputActionId.UiCancel;
                    view.Bind(_promptProvider, actionId);
                }
            }
        }
    }
}
