using Assets.Scripts.Audio;
using Assets.Scripts.Player;
using Assets.Scripts.Skills;
using Assets.Scripts.Skills.ObjectsImpactingSkills.Crate;
using Assets.Scripts.Skills.UpgradeFlow;
using Assets.Scripts.Spawners.GridSpace;
using Assets.Scripts.UI.Level;
using Reflex.Attributes;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Skills
{
    public class SkillUpgradePresenter : MonoBehaviour
    {
        [Inject] private readonly IPlayerManager _playerManager;
        [Inject] private readonly IPlayerLevelPresenter _playerLevelPresenter;
        [Inject] private readonly IOnRandomGridPosSpawner<CollectibleItemsSpawner> _collectibleItemsSpawner;
        [Inject] private readonly ISkillUpgradeFlow _skillUpgradeFlow;
        [Inject] private readonly ISkillsVisualPresenter _skillsVisualPresenter;

        [Header("Upgrade Skill")]
        [SerializeField] private GameObject _upgradeSkillSection;
        [SerializeField] private GameObject _upgradeButtonPrefab;
        [SerializeField] private Transform _buttonsHolder;

        [Header("New Skill")]
        [SerializeField] private GameObject _newSkillSection;
        [SerializeField] private TextMeshProUGUI _newSkillName;
        [SerializeField] private TextMeshProUGUI _newSkillDescription;

        [SerializeField] private AudioClipPlayer _buttonsAudioPlayer;

        private const string SKILL_NAME_TEMPLATE = "New Skill: {0}";

        private bool _isShowingAnySection;

        private IAudioClipPlayer _audioClipPlayer;
        private readonly List<SkillUpgradeButton> _upgradeButtons = new();
        private int _lastHandledInputFrame = -1;

        private void Awake()
        {
            _audioClipPlayer = GetComponentInChildren<IAudioClipPlayer>();
        }

        private void Start()
        {
            _collectibleItemsSpawner.OnSpawnedEntityReleased += ShowRandomSkillInInitializationOrUpgradeSection_OnEvent;
            _playerLevelPresenter.OnExpSliderVisualEndValueReached += ShowRandomSkillInInitializationOrUpgradeSection_OnEvent;
        }

        private void Update()
        {
            if (!_isShowingAnySection || Keyboard.current == null || _lastHandledInputFrame == Time.frameCount)
            {
                return;
            }

            if (_newSkillSection.activeSelf && Keyboard.current.enterKey.wasPressedThisFrame)
            {
                HandleContinueButtonClicked();
                return;
            }

            if (_upgradeSkillSection.activeSelf)
            {
                TryHandleUpgradeButtonHotkey();
            }
        }

        private void OnDestroy()
        {
            _collectibleItemsSpawner.OnSpawnedEntityReleased -= ShowRandomSkillInInitializationOrUpgradeSection_OnEvent;
            _playerLevelPresenter.OnExpSliderVisualEndValueReached -= ShowRandomSkillInInitializationOrUpgradeSection_OnEvent;
        }

        private void ShowRandomSkillInInitializationOrUpgradeSection_OnEvent(object sender, System.EventArgs e)
        {
            _skillUpgradeFlow.QueueRandomRequest(_playerManager.SkillsRegistry);

            if (!_isShowingAnySection)
            {
                _isShowingAnySection = true;
                HandleUpgradeableOrInitializableSkillsShowing();
            }
        }

        private void HandleUpgradeableOrInitializableSkillsShowing()
        {
            _skillsVisualPresenter.HideAll();

            if (_skillUpgradeFlow.TryGetNextRequest(_playerManager.SkillsRegistry, out SkillUpgradeRequest request))
            {
                _newSkillSection.SetActive(false);
                _upgradeSkillSection.SetActive(false);

                if (request.RequestType == SkillUpgradeRequestType.NewSkill)
                {
                    ShowNewSkillSection(request.NewSkill);
                }
                else
                {
                    ShowStatsUpgradeSection(request);
                }

                _audioClipPlayer.Play("Show");
            }
            else
            {
                _newSkillSection.SetActive(false);
                _upgradeSkillSection.SetActive(false);
                _isShowingAnySection = false;
            }
        }

        private void ShowNewSkillSection(ISkillBase skillBase)
        {
            _newSkillName.text = string.Format(SKILL_NAME_TEMPLATE, skillBase.SkillInfo.Name);
            _newSkillDescription.text = skillBase.SkillInfo.Description;

            _skillsVisualPresenter.ShowSkillVisualBasedOnSkillInfo(skillBase.SkillInfo);

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
                    OnClick = () => HandleSkillUpgradeOptionSelected(option)
                });
            }

            DisplayNewButtons(skillStatsUpgradeButtonsData);

            _skillsVisualPresenter.ShowSkillVisualBasedOnSkillInfo(request.UpgradeableSkill.SkillInfo);

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
                    clickableButtonData.OnClick);

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
            _buttonsAudioPlayer.Play("Click");
        }

        private void HandleContinueButtonClicked()
        {
            if (_lastHandledInputFrame == Time.frameCount)
            {
                return;
            }

            _lastHandledInputFrame = Time.frameCount;
            HandleUpgradeableOrInitializableSkillsShowing();
        }
    }
}
