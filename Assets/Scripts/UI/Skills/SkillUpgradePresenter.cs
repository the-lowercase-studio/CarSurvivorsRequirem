using Assets.Scripts.Audio;
using Assets.Scripts.Common.EventArgs;
using Assets.Scripts.Input;
using Assets.Scripts.LevelSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Skills;
using Assets.Scripts.Skills.Constants;
using Assets.Scripts.Skills.ObjectsImpactingSkills.Crate;
using Assets.Scripts.Skills.UpgradeFlow;
using Assets.Scripts.Enemies;
using Assets.Scripts.UI.Input;
using Assets.Scripts.UI.Level;
using Assets.Scripts.UI.Constants;
using Reflex.Attributes;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Skills
{
    public interface ISkillUpgradePresenter
    {
        bool IsAutoUpgradeEnabled { get; set; }
        void AutoResolvePendingUpgrades();
    }

    public class SkillUpgradePresenter : MonoBehaviour, ISkillUpgradePresenter
    {
        [Inject] private readonly IPlayerManager _playerManager = null;
        [Inject] private readonly IPlayerLevelPresenter _playerLevelPresenter = null;
        [Inject] private readonly ICollectibleDropNotifier _collectibleDropNotifier = null;
        [Inject] private readonly ISkillUpgradeFlow _skillUpgradeFlow = null;
        [Inject] private readonly ISkillsVisualPresenter _skillsVisualPresenter = null;
        [Inject] private readonly IInputCommands _inputCommands = null;
        [Inject] private readonly IInputContexts _inputContexts = null;
        [Inject] private readonly IInputPromptProvider _promptProvider = null;

        [Header("Upgrade Skill")]
        [SerializeField] private GameObject _upgradeSkillSection;
        [SerializeField] private GameObject _upgradeButtonPrefab;
        [SerializeField] private Transform _buttonsHolder;

        [Header("New Skill Choice")]
        [SerializeField] private GameObject _newSkillSection;
        [SerializeField] private GameObject _firstSkillCard;
        [SerializeField] private Button _firstSkillCardButton;
        [SerializeField] private InputPromptView _firstSkillPromptView;
        [SerializeField] private TextMeshProUGUI _newSkillName;
        [SerializeField] private TextMeshProUGUI _newSkillDescription;
        [SerializeField] private GameObject _secondSkillCard;
        [SerializeField] private Button _secondSkillCardButton;
        [SerializeField] private InputPromptView _secondSkillPromptView;
        [SerializeField] private TextMeshProUGUI _secondSkillName;
        [SerializeField] private TextMeshProUGUI _secondSkillDescription;

        [Header("New Skill Rewards")]
        [SerializeField] private int _newSkillLevelInterval = 3;

        [SerializeField] private AudioClipPlayer _buttonsAudioPlayer;

        private bool _isShowingAnySection;
        private IAudioClipPlayer _audioClipPlayer;
        private readonly List<SkillUpgradeButton> _upgradeButtons = new();
        private int _lastHandledInputFrame = -1;
        private IReadOnlyList<ISkillBase> _currentSkillChoices;
        private InputScopeToken _rewardScopeToken;
        private bool _isInitialized;

        private void Awake()
        {
            if (_inputCommands == null || _inputContexts == null || _promptProvider == null)
            {
                throw new InvalidOperationException($"[{nameof(SkillUpgradePresenter)}] Required input dependencies were not injected on '{name}'.");
            }

            if (_firstSkillCardButton == null || _firstSkillPromptView == null || _secondSkillCardButton == null || _secondSkillPromptView == null)
            {
                throw new InvalidOperationException($"[{nameof(SkillUpgradePresenter)}] New skill card Button/InputPromptView references are unassigned on '{name}'.");
            }

            _audioClipPlayer = _buttonsAudioPlayer != null ? _buttonsAudioPlayer : GetComponentInChildren<IAudioClipPlayer>();
            _firstSkillCardButton.onClick.AddListener(HandleFirstSkillCardClicked);
            _secondSkillCardButton.onClick.AddListener(HandleSecondSkillCardClicked);
            _isInitialized = true;
        }

        private void OnEnable()
        {
            if (!_isInitialized)
            {
                return;
            }

            _inputCommands.OnCommand += HandleInputCommand;
        }

        private void Start()
        {
            _collectibleDropNotifier.OnSkillUpgradeCollectibleCollected += HandleCrateRewardRequest;
            _playerLevelPresenter.OnExpSliderVisualEndValueReached += HandleLevelRewardRequest;
            _skillUpgradeFlow.OnRequestQueued += HandleRequestQueued;
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
            if (_firstSkillCardButton != null)
            {
                _firstSkillCardButton.onClick.RemoveListener(HandleFirstSkillCardClicked);
            }

            if (_secondSkillCardButton != null)
            {
                _secondSkillCardButton.onClick.RemoveListener(HandleSecondSkillCardClicked);
            }

            _collectibleDropNotifier.OnSkillUpgradeCollectibleCollected -= HandleCrateRewardRequest;
            _playerLevelPresenter.OnExpSliderVisualEndValueReached -= HandleLevelRewardRequest;
            _skillUpgradeFlow.OnRequestQueued -= HandleRequestQueued;

            ReleaseRewardScope();
        }

        public bool IsAutoUpgradeEnabled { get; set; }

        public void AutoResolvePendingUpgrades()
        {
            if (_newSkillSection != null)
            {
                _newSkillSection.SetActive(false);
            }
            if (_upgradeSkillSection != null)
            {
                _upgradeSkillSection.SetActive(false);
            }
            if (_skillsVisualPresenter != null)
            {
                _skillsVisualPresenter.HideAll();
            }
            _isShowingAnySection = false;
            _currentSkillChoices = null;
            ReleaseRewardScope();

            while (_skillUpgradeFlow.TryGetNextRequest(_playerManager.SkillsRegistry, out SkillUpgradeRequest request))
            {
                if (request.RequestType == SkillUpgradeRequestType.NewSkillChoice)
                {
                    if (request.SkillChoices != null && request.SkillChoices.Count > 0)
                    {
                        _playerManager.SkillsRegistry.InitializeSkill(request.SkillChoices[0]);
                    }
                }
                else if (request.RequestType == SkillUpgradeRequestType.UpgradeSkill)
                {
                    if (request.UpgradeOptions != null && request.UpgradeOptions.Count > 0)
                    {
                        request.UpgradeOptions[0].Apply();
                    }
                }
            }
        }

        private void HandleRequestQueued(object sender, System.EventArgs e)
        {
            TryShowQueuedRewardSection();
        }

        private void HandleCrateRewardRequest(object sender, System.EventArgs e)
        {
            _skillUpgradeFlow.QueueRandomSkillUpgradeRequest(_playerManager.SkillsRegistry);
        }

        private void HandleLevelRewardRequest(object sender, ValueEventArgs<LevelData> e)
        {
            bool isNewSkillQueued = ShouldQueueNewSkillReward(e.Value.Lvl)
                && _skillUpgradeFlow.QueueRandomNewSkillRequest(_playerManager.SkillsRegistry);

            if (!isNewSkillQueued)
            {
                _skillUpgradeFlow.QueueRandomSkillUpgradeRequest(_playerManager.SkillsRegistry);
            }

            TryShowQueuedRewardSection();
        }

        private void TryShowQueuedRewardSection()
        {
            if (IsAutoUpgradeEnabled)
            {
                AutoResolvePendingUpgrades();
                return;
            }

            if (!_isShowingAnySection)
            {
                _isShowingAnySection = true;
                HandleUpgradeableOrInitializableSkillsShowing();
            }
        }

        private bool ShouldQueueNewSkillReward(int level)
        {
            return _newSkillLevelInterval > 0
                && level > 1
                && (level - 1) % _newSkillLevelInterval == 0
                && _playerManager.SkillsRegistry.InitializedSkillsCount < SkillConstants.MAX_ACTIVE_SKILLS
                && _playerManager.SkillsRegistry.UninitializedSkillsCount > 0;
        }

        private void HandleUpgradeableOrInitializableSkillsShowing()
        {
            if (IsAutoUpgradeEnabled)
            {
                AutoResolvePendingUpgrades();
                return;
            }

            _skillsVisualPresenter.HideAll();

            if (_skillUpgradeFlow.TryGetNextRequest(_playerManager.SkillsRegistry, out SkillUpgradeRequest request))
            {
                _newSkillSection.SetActive(false);
                _upgradeSkillSection.SetActive(false);

                if (request.RequestType == SkillUpgradeRequestType.NewSkillChoice)
                {
                    ShowNewSkillChoiceSection(request.SkillChoices);
                }
                else
                {
                    ShowStatsUpgradeSection(request);
                }

                if (_audioClipPlayer != null)
                {
                    _audioClipPlayer.Play("Show");
                }
            }
            else
            {
                _newSkillSection.SetActive(false);
                _upgradeSkillSection.SetActive(false);
                _isShowingAnySection = false;
                ReleaseRewardScope();
            }
        }

        private void ShowNewSkillChoiceSection(IReadOnlyList<ISkillBase> choices)
        {
            _currentSkillChoices = choices;

            if (choices == null || choices.Count == 0)
            {
                HandleUpgradeableOrInitializableSkillsShowing();
                return;
            }

            _skillsVisualPresenter.SetContext(SkillVisualContext.NewSkillUnlocked);

            int slotCount = Math.Min(choices.Count, 2);
            AcquireRewardScope(slotCount);

            if (_firstSkillCard != null)
            {
                _firstSkillCard.SetActive(true);
            }

            _firstSkillPromptView.Bind(_promptProvider, InputActionId.UpgradeSelectChoice1);
            _newSkillName.text = string.Format(UIConstants.SKILL_NAME_TEMPLATE, choices[0].SkillInfo.Name);
            _newSkillDescription.text = choices[0].SkillInfo.Description;
            _skillsVisualPresenter.ShowSkillVisual(choices[0].SkillInfo, 0);

            if (choices.Count > 1)
            {
                if (_secondSkillCard != null)
                {
                    _secondSkillCard.SetActive(true);
                }
                _secondSkillPromptView.Bind(_promptProvider, InputActionId.UpgradeSelectChoice2);
                if (_secondSkillName != null)
                {
                    _secondSkillName.text = string.Format(UIConstants.SKILL_NAME_TEMPLATE, choices[1].SkillInfo.Name);
                }
                if (_secondSkillDescription != null)
                {
                    _secondSkillDescription.text = choices[1].SkillInfo.Description;
                }
                _skillsVisualPresenter.ShowSkillVisual(choices[1].SkillInfo, 1);
            }
            else
            {
                _secondSkillPromptView.Unbind();
                if (_secondSkillCard != null)
                {
                    _secondSkillCard.SetActive(false);
                }
            }

            _newSkillSection.SetActive(true);
        }

        private void ShowStatsUpgradeSection(SkillUpgradeRequest request)
        {
            List<ClickableButtonData> skillStatsUpgradeButtonsData = new();

            foreach (SkillUpgradeOption option in request.UpgradeOptions)
            {
                skillStatsUpgradeButtonsData.Add(new ClickableButtonData
                {
                    Text = option.Text,
                    Rarity = option.Rarity,
                    Icon = option.Icon,
                    OnClick = () => HandleSkillUpgradeOptionSelected(option)
                });
            }

            DisplayNewButtons(skillStatsUpgradeButtonsData);
            AcquireRewardScope(_upgradeButtons.Count);

            _skillsVisualPresenter.SetContext(SkillVisualContext.StatUpgrade);
            _skillsVisualPresenter.ShowSkillVisual(request.UpgradeableSkill.SkillInfo, 0);

            _upgradeSkillSection.SetActive(true);
        }

        private void DisplayNewButtons(IEnumerable<ClickableButtonData> clickableButtonsData)
        {
            DestroyAllButtons();
            CreateUpgradeButtons(clickableButtonsData);
        }

        private void DestroyAllButtons()
        {
            _upgradeButtons.Clear();

            foreach (Transform child in _buttonsHolder)
            {
                Destroy(child.gameObject);
            }
        }

        private void CreateUpgradeButtons(IEnumerable<ClickableButtonData> clickableButtonsData)
        {
            int slotIndex = 0;

            foreach (var clickableButtonData in clickableButtonsData)
            {
                GameObject buttonObject = Instantiate(_upgradeButtonPrefab, _buttonsHolder);
                SkillUpgradeButton button = buttonObject.GetComponent<SkillUpgradeButton>();
                if (button == null)
                {
                    button = buttonObject.AddComponent<SkillUpgradeButton>();
                }

                InputActionId slotAction = GetSlotActionId(slotIndex);
                button.Initialize(
                    clickableButtonData.Text,
                    _promptProvider,
                    slotAction,
                    clickableButtonData.Rarity,
                    clickableButtonData.OnClick,
                    icon: clickableButtonData.Icon);

                _upgradeButtons.Add(button);

                slotIndex++;
            }
        }

        private void HandleInputCommand(InputCommand command)
        {
            if (command.Kind != InputCommandKind.UpgradeSelectChoice || !_isShowingAnySection)
            {
                return;
            }

            if (_lastHandledInputFrame == Time.frameCount)
            {
                return;
            }

            if (_inputContexts.EffectiveContext != InputContextScopeKind.LiveReward || !_inputContexts.IsSlotEligible(command.SlotIndex))
            {
                return;
            }

            if (_newSkillSection.activeSelf)
            {
                SelectNewSkillChoice(command.SlotIndex);
                return;
            }

            if (_upgradeSkillSection.activeSelf)
            {
                InvokeUpgradeButton(command.SlotIndex);
            }
        }

        private void HandleFirstSkillCardClicked()
        {
            SelectNewSkillChoice(0);
        }

        private void HandleSecondSkillCardClicked()
        {
            SelectNewSkillChoice(1);
        }

        private void InvokeUpgradeButton(int buttonIndex)
        {
            if (_lastHandledInputFrame == Time.frameCount || buttonIndex < 0 || buttonIndex >= _upgradeButtons.Count)
            {
                return;
            }

            _upgradeButtons[buttonIndex].Invoke();
        }

        private void HandleSkillUpgradeOptionSelected(SkillUpgradeOption option)
        {
            if (_lastHandledInputFrame == Time.frameCount)
            {
                return;
            }

            _lastHandledInputFrame = Time.frameCount;
            _inputContexts.ConsumeActiveControlsUntilRelease();
            option.Apply();
            HandleUpgradeableOrInitializableSkillsShowing();
            if (_audioClipPlayer != null)
            {
                _audioClipPlayer.Play("Click");
            }
        }

        private void SelectNewSkillChoice(int choiceIndex)
        {
            if (_lastHandledInputFrame == Time.frameCount)
            {
                return;
            }

            if (_currentSkillChoices == null || choiceIndex < 0 || choiceIndex >= _currentSkillChoices.Count)
            {
                return;
            }

            _lastHandledInputFrame = Time.frameCount;
            _inputContexts.ConsumeActiveControlsUntilRelease();

            ISkillBase chosenSkill = _currentSkillChoices[choiceIndex];
            _currentSkillChoices = null;

            _playerManager.SkillsRegistry.InitializeSkill(chosenSkill);

            if (_audioClipPlayer != null)
            {
                _audioClipPlayer.Play("Click");
            }

            HandleUpgradeableOrInitializableSkillsShowing();
        }

        private void AcquireRewardScope(int visibleSlotCount)
        {
            if (!_rewardScopeToken.IsValid)
            {
                _rewardScopeToken = _inputContexts.AcquireScope(InputContextScopeKind.LiveReward, GetEntityId().GetHashCode());
            }

            _inputContexts.SetLiveRewardEligibleSlots(visibleSlotCount);
        }

        private void ReleaseRewardScope()
        {
            if (_firstSkillPromptView != null)
            {
                _firstSkillPromptView.Unbind();
            }

            if (_secondSkillPromptView != null)
            {
                _secondSkillPromptView.Unbind();
            }

            _inputContexts?.SetLiveRewardEligibleSlots(0);
            if (_rewardScopeToken.IsValid)
            {
                _inputContexts?.ReleaseScope(_rewardScopeToken);
                _rewardScopeToken = default;
            }
        }

        private static InputActionId GetSlotActionId(int slotIndex)
        {
            switch (slotIndex)
            {
                case 0:
                    return InputActionId.UpgradeSelectChoice1;
                case 1:
                    return InputActionId.UpgradeSelectChoice2;
                case 2:
                    return InputActionId.UpgradeSelectChoice3;
                default:
                    throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Reward slot index out of supported range.");
            }
        }
    }
}
