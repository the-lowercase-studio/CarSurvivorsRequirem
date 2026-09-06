using Assets.Scripts.Audio;
using Assets.Scripts.Common.EventArgs;
using Assets.Scripts.LevelSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Skills;
using Assets.Scripts.Skills.Constants;
using Assets.Scripts.Skills.ObjectsImpactingSkills.Crate;
using Assets.Scripts.Skills.UpgradeFlow;
using Assets.Scripts.Enemies;
using Assets.Scripts.UI.Level;
using Reflex.Attributes;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Skills
{
    public interface ISkillUpgradePresenter
    {
    }

    public class SkillUpgradePresenter : MonoBehaviour, ISkillUpgradePresenter
    {
        [Inject] private readonly IPlayerManager _playerManager = null;
        [Inject] private readonly IPlayerLevelPresenter _playerLevelPresenter = null;
        [Inject] private readonly ICollectibleDropNotifier _collectibleDropNotifier = null;
        [Inject] private readonly ISkillUpgradeFlow _skillUpgradeFlow = null;
        [Inject] private readonly ISkillsVisualPresenter _skillsVisualPresenter = null;

        [Header("Upgrade Skill")]
        [SerializeField] private GameObject _upgradeSkillSection;
        [SerializeField] private GameObject _upgradeButtonPrefab;
        [SerializeField] private Transform _buttonsHolder;

        [Header("New Skill Choice")]
        [SerializeField] private GameObject _newSkillSection;
        [SerializeField] private GameObject _firstSkillCard;
        [SerializeField] private TextMeshProUGUI _newSkillName;
        [SerializeField] private TextMeshProUGUI _newSkillDescription;
        [SerializeField] private GameObject _secondSkillCard;
        [SerializeField] private TextMeshProUGUI _secondSkillName;
        [SerializeField] private TextMeshProUGUI _secondSkillDescription;

        [Header("New Skill Rewards")]
        [SerializeField] private int _newSkillLevelInterval = 3;

        [SerializeField] private AudioClipPlayer _buttonsAudioPlayer;

        private const string SKILL_NAME_TEMPLATE = "New Skill: {0}";

        private bool _isShowingAnySection;
        private IAudioClipPlayer _audioClipPlayer;
        private readonly List<SkillUpgradeButton> _upgradeButtons = new();
        private int _lastHandledInputFrame = -1;
        private IReadOnlyList<ISkillBase> _currentSkillChoices;

        private void Awake()
        {
            _audioClipPlayer = _buttonsAudioPlayer != null ? _buttonsAudioPlayer : GetComponentInChildren<IAudioClipPlayer>();
        }

        private void Start()
        {
            _collectibleDropNotifier.OnSkillUpgradeCollectibleCollected += HandleCrateRewardRequest;
            _playerLevelPresenter.OnExpSliderVisualEndValueReached += HandleLevelRewardRequest;
            _skillUpgradeFlow.OnRequestQueued += HandleRequestQueued;
        }

        private void Update()
        {
            if (!_isShowingAnySection || Keyboard.current == null || _lastHandledInputFrame == Time.frameCount)
            {
                return;
            }

            if (_newSkillSection.activeSelf)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
                {
                    SelectNewSkillChoice(0);
                    return;
                }

                if ((Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
                    && _currentSkillChoices != null && _currentSkillChoices.Count > 1)
                {
                    SelectNewSkillChoice(1);
                    return;
                }
            }

            if (_upgradeSkillSection.activeSelf)
            {
                TryHandleUpgradeButtonHotkey();
            }
        }

        private void OnDestroy()
        {
            _collectibleDropNotifier.OnSkillUpgradeCollectibleCollected -= HandleCrateRewardRequest;
            _playerLevelPresenter.OnExpSliderVisualEndValueReached -= HandleLevelRewardRequest;
            _skillUpgradeFlow.OnRequestQueued -= HandleRequestQueued;
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

            if (_firstSkillCard != null)
            {
                _firstSkillCard.SetActive(true);
            }

            _newSkillName.text = string.Format(SKILL_NAME_TEMPLATE, choices[0].SkillInfo.Name);
            _newSkillDescription.text = choices[0].SkillInfo.Description;
            _skillsVisualPresenter.ShowSkillVisual(choices[0].SkillInfo, 0);

            if (choices.Count > 1)
            {
                if (_secondSkillCard != null)
                {
                    _secondSkillCard.SetActive(true);
                }
                if (_secondSkillName != null)
                {
                    _secondSkillName.text = string.Format(SKILL_NAME_TEMPLATE, choices[1].SkillInfo.Name);
                }
                if (_secondSkillDescription != null)
                {
                    _secondSkillDescription.text = choices[1].SkillInfo.Description;
                }
                _skillsVisualPresenter.ShowSkillVisual(choices[1].SkillInfo, 1);
            }
            else
            {
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
            int buttonNumber = 1;

            foreach (var clickableButtonData in clickableButtonsData)
            {
                GameObject buttonObject = Instantiate(_upgradeButtonPrefab, _buttonsHolder);
                SkillUpgradeButton button = buttonObject.GetComponent<SkillUpgradeButton>();
                if (button == null)
                {
                    button = buttonObject.AddComponent<SkillUpgradeButton>();
                }

                button.Initialize(
                    clickableButtonData.Text,
                    buttonNumber,
                    clickableButtonData.Rarity,
                    clickableButtonData.OnClick,
                    icon: clickableButtonData.Icon);

                _upgradeButtons.Add(button);

                buttonNumber++;
            }
        }

        private void TryHandleUpgradeButtonHotkey()
        {
            if (IsButtonHotkeyPressed(0))
            {
                InvokeUpgradeButton(0);
            }
            else if (IsButtonHotkeyPressed(1))
            {
                InvokeUpgradeButton(1);
            }
            else if (IsButtonHotkeyPressed(2))
            {
                InvokeUpgradeButton(2);
            }
        }

        private bool IsButtonHotkeyPressed(int buttonIndex)
        {
            return buttonIndex switch
            {
                0 => Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame,
                1 => Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame,
                2 => Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame,
                _ => false
            };
        }

        private void InvokeUpgradeButton(int buttonIndex)
        {
            if (buttonIndex >= _upgradeButtons.Count)
            {
                return;
            }

            _lastHandledInputFrame = Time.frameCount;
            _upgradeButtons[buttonIndex].Invoke();
        }

        private void HandleSkillUpgradeOptionSelected(SkillUpgradeOption option)
        {
            _lastHandledInputFrame = Time.frameCount;
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

            ISkillBase chosenSkill = _currentSkillChoices[choiceIndex];
            _currentSkillChoices = null;

            _playerManager.SkillsRegistry.InitializeSkill(chosenSkill);

            if (_audioClipPlayer != null)
            {
                _audioClipPlayer.Play("Click");
            }

            HandleUpgradeableOrInitializableSkillsShowing();
        }
    }
}
