using System;
using System.Collections.Generic;
using Assets.Scripts.Input;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Input
{
    public class UiInputScope : MonoBehaviour
    {
        [Inject] private readonly IInputContexts _inputContexts = null;
        [Inject] private readonly IInputActionSource _actionSource = null;
        [Inject] private readonly IInputDeviceContext _deviceContext = null;

        [Header("Event System References")]
        [SerializeField] private EventSystem _eventSystem;
        [SerializeField] private InputSystemUIInputModule _uiInputModule;

        [Header("Action References")]
        [SerializeField] private InputActionReference _navigateReference;
        [SerializeField] private InputActionReference _submitReference;
        [SerializeField] private InputActionReference _pointReference;
        [SerializeField] private InputActionReference _clickReference;
        [SerializeField] private InputActionReference _scrollWheelReference;

        [Header("Managed Native Widgets")]
        [SerializeField] private List<TMP_Dropdown> _registeredDropdowns = new List<TMP_Dropdown>();
        [SerializeField] private List<TMP_InputField> _registeredInputFields = new List<TMP_InputField>();

        private readonly List<Toggle> _dropdownItemBuffer = new List<Toggle>(16);
        private readonly Vector3[] _viewportCorners = new Vector3[4];
        private readonly Vector3[] _itemCorners = new Vector3[4];

        private Func<Selectable> _selectionResolver;
        private TMP_Dropdown _activeExpandedDropdown;
        private TMP_Dropdown _closingDropdown;
        private ScrollRect _activeDropdownScrollRect;
        private GameObject _lastSelectedInDropdown;
        private bool _isInitialized;

        public EventSystem EventSystem
        {
            get { return _eventSystem; }
        }

        public InputSystemUIInputModule UiInputModule
        {
            get { return _uiInputModule; }
        }

        public void SetSelectionResolver(Func<Selectable> selectionResolver)
        {
            _selectionResolver = selectionResolver;
        }

        public void RegisterDropdown(TMP_Dropdown dropdown)
        {
            if (dropdown != null && !_registeredDropdowns.Contains(dropdown))
            {
                _registeredDropdowns.Add(dropdown);
            }
        }

        public void RegisterInputField(TMP_InputField inputField)
        {
            if (inputField != null && !_registeredInputFields.Contains(inputField))
            {
                _registeredInputFields.Add(inputField);
            }
        }

        public void SyncModulePolicy()
        {
            if (!_isInitialized)
            {
                return;
            }

            bool navEligible = _inputContexts.IsUiNavigationEligible;
            bool pointerEligible = navEligible || _inputContexts.EffectiveContext == InputContextScopeKind.LiveReward;

            _uiInputModule.cancel = null;
            _uiInputModule.deselectOnBackgroundClick = false;
            _uiInputModule.move = navEligible ? _navigateReference : null;
            _uiInputModule.submit = navEligible ? _submitReference : null;
            _uiInputModule.scrollWheel = navEligible ? _scrollWheelReference : null;
            _uiInputModule.point = pointerEligible ? _pointReference : null;
            _uiInputModule.leftClick = pointerEligible ? _clickReference : null;

            _eventSystem.sendNavigationEvents = navEligible;

            if (!navEligible && _eventSystem.currentSelectedGameObject != null)
            {
                _eventSystem.SetSelectedGameObject(null);
            }

            _inputContexts.ReapplyPolicy();
        }

        public bool TryHandleWidgetCancel()
        {
            for (int i = 0; i < _registeredDropdowns.Count; i++)
            {
                TMP_Dropdown dropdown = _registeredDropdowns[i];
                if (dropdown != null && dropdown.IsExpanded && dropdown != _closingDropdown)
                {
                    PrepareDropdownListForImmediateCancel(dropdown);
                    _closingDropdown = dropdown;
                    _activeExpandedDropdown = null;
                    _activeDropdownScrollRect = null;
                    _lastSelectedInDropdown = null;
                    dropdown.Hide();
                    _inputContexts.ConsumeActiveControlsUntilRelease();

                    if (IsValidSelectable(dropdown))
                    {
                        _eventSystem.SetSelectedGameObject(dropdown.gameObject);
                    }

                    return true;
                }
            }

            for (int i = 0; i < _registeredInputFields.Count; i++)
            {
                TMP_InputField inputField = _registeredInputFields[i];
                if (inputField != null && inputField.isFocused)
                {
                    inputField.DeactivateInputField();
                    _inputContexts.ConsumeActiveControlsUntilRelease();
                    return true;
                }
            }

            return false;
        }

        public void EnsureValidSelection(Selectable preferredSelectable)
        {
            if (!_isInitialized || !_inputContexts.IsUiNavigationEligible)
            {
                return;
            }

            if (IsValidGameObjectSelection(_eventSystem.currentSelectedGameObject))
            {
                return;
            }

            if (IsValidSelectable(preferredSelectable))
            {
                _eventSystem.SetSelectedGameObject(preferredSelectable.gameObject);
                return;
            }

            if (_selectionResolver != null)
            {
                Selectable resolved = _selectionResolver();
                if (IsValidSelectable(resolved))
                {
                    _eventSystem.SetSelectedGameObject(resolved.gameObject);
                }
            }
        }

        public void SetSelectedSafe(Selectable selectable)
        {
            if (!_isInitialized || !_inputContexts.IsUiNavigationEligible)
            {
                return;
            }

            if (IsValidSelectable(selectable))
            {
                _eventSystem.SetSelectedGameObject(selectable.gameObject);
            }
        }

        public static bool IsValidSelectable(Selectable selectable)
        {
            return selectable != null
                && selectable.gameObject.activeInHierarchy
                && selectable.isActiveAndEnabled
                && selectable.interactable
                && selectable.navigation.mode != UnityEngine.UI.Navigation.Mode.None;
        }

        public static bool IsValidGameObjectSelection(GameObject target)
        {
            if (target == null || !target.activeInHierarchy)
            {
                return false;
            }

            Selectable selectable = target.GetComponent<Selectable>();
            return IsValidSelectable(selectable);
        }

        private void Awake()
        {
            if (_eventSystem == null)
            {
                throw new InvalidOperationException($"[{nameof(UiInputScope)}] _eventSystem is unassigned on '{name}'.");
            }

            if (_uiInputModule == null)
            {
                throw new InvalidOperationException($"[{nameof(UiInputScope)}] _uiInputModule is unassigned on '{name}'.");
            }

            if (_inputContexts == null || _actionSource == null || _deviceContext == null)
            {
                throw new InvalidOperationException($"[{nameof(UiInputScope)}] Required input services were not injected on '{name}'.");
            }

            if (_navigateReference == null)
            {
                _navigateReference = _uiInputModule.move;
            }

            if (_submitReference == null)
            {
                _submitReference = _uiInputModule.submit;
            }

            if (_pointReference == null)
            {
                _pointReference = _uiInputModule.point;
            }

            if (_clickReference == null)
            {
                _clickReference = _uiInputModule.leftClick;
            }

            if (_scrollWheelReference == null)
            {
                _scrollWheelReference = _uiInputModule.scrollWheel;
            }

            if (_navigateReference == null || _submitReference == null || _pointReference == null || _clickReference == null || _scrollWheelReference == null)
            {
                throw new InvalidOperationException($"[{nameof(UiInputScope)}] One or more UI InputActionReferences are unassigned on '{name}'.");
            }

            _uiInputModule.actionsAsset = _actionSource.ActionAsset;
            _uiInputModule.cancel = null;
            _uiInputModule.deselectOnBackgroundClick = false;
            _isInitialized = true;
        }

        private void OnEnable()
        {
            if (!_isInitialized)
            {
                return;
            }

            _inputContexts.OnContextChanged += HandleContextChanged;
            _deviceContext.OnDeviceChanged += HandleDeviceChanged;
            SyncModulePolicy();
        }

        private void Start()
        {
            SyncModulePolicy();
        }

        private void OnDisable()
        {
            if (!_isInitialized)
            {
                return;
            }

            _inputContexts.OnContextChanged -= HandleContextChanged;
            _deviceContext.OnDeviceChanged -= HandleDeviceChanged;
            _activeExpandedDropdown = null;
            _closingDropdown = null;
            _activeDropdownScrollRect = null;
            _lastSelectedInDropdown = null;
        }

        private void Update()
        {
            if (!_isInitialized || !_inputContexts.IsUiNavigationEligible)
            {
                return;
            }

            UpdateExpandedDropdownNavigation();

            if (_deviceContext.ActiveScheme == InputScheme.Gamepad
                && !IsValidGameObjectSelection(_eventSystem.currentSelectedGameObject))
            {
                EnsureValidSelection(null);
            }
        }

        private void HandleContextChanged()
        {
            SyncModulePolicy();

            if (_inputContexts.IsUiNavigationEligible && _deviceContext.ActiveScheme == InputScheme.Gamepad)
            {
                EnsureValidSelection(null);
            }
        }

        private void HandleDeviceChanged()
        {
            if (_deviceContext.ActiveScheme == InputScheme.Gamepad && _inputContexts.IsUiNavigationEligible)
            {
                EnsureValidSelection(null);
            }
        }

        private void UpdateExpandedDropdownNavigation()
        {
            if (_closingDropdown != null && !_closingDropdown.IsExpanded)
            {
                TMP_Dropdown finishedClosing = _closingDropdown;
                _closingDropdown = null;
                if (IsValidSelectable(finishedClosing) && !IsValidGameObjectSelection(_eventSystem.currentSelectedGameObject))
                {
                    SetSelectedSafe(finishedClosing);
                }
            }

            TMP_Dropdown expanded = null;
            for (int i = 0; i < _registeredDropdowns.Count; i++)
            {
                TMP_Dropdown candidate = _registeredDropdowns[i];
                if (candidate != null && candidate.IsExpanded && candidate != _closingDropdown)
                {
                    expanded = candidate;
                    break;
                }
            }

            if (expanded == null)
            {
                TMP_Dropdown previouslyExpanded = _activeExpandedDropdown;
                _activeExpandedDropdown = null;
                _activeDropdownScrollRect = null;
                _lastSelectedInDropdown = null;

                if (_closingDropdown != null && IsValidSelectable(_closingDropdown))
                {
                    if (_eventSystem.currentSelectedGameObject != _closingDropdown.gameObject)
                    {
                        SetSelectedSafe(_closingDropdown);
                    }
                }
                else if (previouslyExpanded != null
                    && IsValidSelectable(previouslyExpanded)
                    && !IsValidGameObjectSelection(_eventSystem.currentSelectedGameObject))
                {
                    SetSelectedSafe(previouslyExpanded);
                }

                return;
            }

            if (_activeExpandedDropdown != expanded)
            {
                _activeExpandedDropdown = expanded;
                _lastSelectedInDropdown = null;
                ConfigureExpandedDropdownList(expanded);
            }

            GameObject currentSelected = _eventSystem.currentSelectedGameObject;
            if (currentSelected != null && currentSelected != _lastSelectedInDropdown && _activeDropdownScrollRect != null)
            {
                _lastSelectedInDropdown = currentSelected;
                ScrollItemIntoView(_activeDropdownScrollRect, currentSelected.transform as RectTransform);
            }
        }

        private void ConfigureExpandedDropdownList(TMP_Dropdown dropdown)
        {
            Transform dropdownListTransform = dropdown.transform.Find("Dropdown List");
            if (dropdownListTransform == null)
            {
                _activeDropdownScrollRect = null;
                return;
            }

            _activeDropdownScrollRect = dropdownListTransform.GetComponentInChildren<ScrollRect>();
            _dropdownItemBuffer.Clear();

            Toggle[] toggles = dropdownListTransform.GetComponentsInChildren<Toggle>(false);
            for (int i = 0; i < toggles.Length; i++)
            {
                Toggle toggle = toggles[i];
                if (toggle != null && toggle.interactable)
                {
                    _dropdownItemBuffer.Add(toggle);
                }
            }

            int count = _dropdownItemBuffer.Count;
            if (count == 0)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Toggle toggle = _dropdownItemBuffer[i];
                UnityEngine.UI.Navigation nav = toggle.navigation;
                nav.mode = UnityEngine.UI.Navigation.Mode.Explicit;
                nav.selectOnUp = i > 0 ? _dropdownItemBuffer[i - 1] : null;
                nav.selectOnDown = i < count - 1 ? _dropdownItemBuffer[i + 1] : null;
                nav.selectOnLeft = null;
                nav.selectOnRight = null;
                toggle.navigation = nav;
            }

            Toggle selectedToggle = null;
            int selectedIndex = dropdown.value;
            if (selectedIndex >= 0 && selectedIndex < count)
            {
                selectedToggle = _dropdownItemBuffer[selectedIndex];
            }
            else
            {
                selectedToggle = _dropdownItemBuffer[0];
            }

            if (selectedToggle != null)
            {
                _eventSystem.SetSelectedGameObject(selectedToggle.gameObject);
                _lastSelectedInDropdown = selectedToggle.gameObject;

                if (_activeDropdownScrollRect != null)
                {
                    ScrollItemIntoView(_activeDropdownScrollRect, selectedToggle.transform as RectTransform);
                }
            }
        }

        private static void PrepareDropdownListForImmediateCancel(TMP_Dropdown dropdown)
        {
            Transform dropdownListTransform = dropdown.transform.Find("Dropdown List");
            if (dropdownListTransform == null)
            {
                return;
            }

            CanvasGroup canvasGroup = dropdownListTransform.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.alpha = 0f;
            }

            Toggle[] toggles = dropdownListTransform.GetComponentsInChildren<Toggle>(true);
            for (int i = 0; i < toggles.Length; i++)
            {
                if (toggles[i] != null)
                {
                    toggles[i].interactable = false;
                }
            }

            dropdownListTransform.gameObject.SetActive(false);
        }

        private void ScrollItemIntoView(ScrollRect scrollRect, RectTransform item)
        {
            if (scrollRect == null || scrollRect.viewport == null || scrollRect.content == null || item == null)
            {
                return;
            }

            if (!item.IsChildOf(scrollRect.content))
            {
                return;
            }

            Canvas.ForceUpdateCanvases();

            scrollRect.viewport.GetWorldCorners(_viewportCorners);
            item.GetWorldCorners(_itemCorners);

            float viewportBottom = _viewportCorners[0].y;
            float viewportTop = _viewportCorners[1].y;
            float itemBottom = _itemCorners[0].y;
            float itemTop = _itemCorners[1].y;

            float deltaWorldY = 0f;
            if (itemTop > viewportTop)
            {
                deltaWorldY = itemTop - viewportTop;
            }
            else if (itemBottom < viewportBottom)
            {
                deltaWorldY = itemBottom - viewportBottom;
            }

            if (Mathf.Approximately(deltaWorldY, 0f))
            {
                return;
            }

            Vector2 contentPos = scrollRect.content.anchoredPosition;
            float localDelta = scrollRect.content.InverseTransformVector(new Vector3(0f, deltaWorldY, 0f)).y;
            contentPos.y -= localDelta;

            float maxScroll = Mathf.Max(0f, scrollRect.content.rect.height - scrollRect.viewport.rect.height);
            contentPos.y = Mathf.Clamp(contentPos.y, 0f, maxScroll);
            scrollRect.content.anchoredPosition = contentPos;
        }
    }
}
