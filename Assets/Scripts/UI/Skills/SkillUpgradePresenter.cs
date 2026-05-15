using Assets.Scripts.Audio;
using Assets.Scripts.GameFlow;
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
using UnityEngine.UI;

namespace Assets.Scripts.UI.Skills
{
    public class SkillUpgradePresenter : MonoBehaviour
    {
        [Inject] private readonly IPlayerManager _playerManager;
        [Inject] private readonly IPlayerLevelPresenter _playerLevelPresenter;
        [Inject] private readonly IOnRandomGridPosSpawner<CollectibleItemsSpawner> _collectibleItemsSpawner;
        [Inject] private readonly ISkillUpgradeFlow _skillUpgradeFlow;

        [Header("Upgrade Skill")]
        [SerializeField] private GameObject _upgradeSkillSection;
        [SerializeField] private Button _upgradeButtonPrefab;
        [SerializeField] private Transform _buttonsHolder;

        [Header("New Skill")]
        [SerializeField] private GameObject _newSkillSection;
        [SerializeField] private TextMeshProUGUI _newSkillName;
        [SerializeField] private TextMeshProUGUI _newSkillDescription;
        [SerializeField] private Button _continueButton;

        [SerializeField] private AudioClipPlayer _buttonsAudioPlayer;
        [SerializeField] private SkillsVisualPresenter _skillsVisualPresenter;

        private const string SKILL_NAME_TEMPLATE = "New Skill: {0}";

        private bool _isShowingAnySection;

        private IAudioClipPlayer _audioClipPlayer;

        private void Awake()
        {
            _audioClipPlayer = GetComponentInChildren<IAudioClipPlayer>();
        }

        private void Start()
        {
            _collectibleItemsSpawner.OnSpawnedEntityReleased += ShowRandomSkillInInitializationOrUpgradeSection_OnEvent;
            _playerLevelPresenter.OnExpSliderVisualEndValueReached += ShowRandomSkillInInitializationOrUpgradeSection_OnEvent;
            _continueButton.onClick.AddListener(() => HandleUpgradeableOrInitializableSkillsShowing());
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

            GameTime.Resume();

            if (_skillUpgradeFlow.TryGetNextRequest(_playerManager.SkillsRegistry, out SkillUpgradeRequest request))
            {
                if (request.RequestType == SkillUpgradeRequestType.NewSkill)
                {
                    ShowNewSkillSection(request.NewSkill);
                }
                else
                {
                    ShowStatsUpgradeSection(request);
                }

                _audioClipPlayer.Play("Show");
                GameTime.Pause();
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
                    OnClick = () =>
                    {
                        option.Apply();
                        HandleUpgradeableOrInitializableSkillsShowing();
                        _buttonsAudioPlayer.Play("Click");
                    }
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
            foreach (Button child in _buttonsHolder.GetComponentsInChildren<Button>())
            {
                Destroy(child.gameObject);
            }
        }

        private void CreateUpgradeButtons(IEnumerable<ClickableButtonData> clickableButtonsData)
        {
            foreach (var clickableButtonData in clickableButtonsData)
            {
                Button button = Instantiate(_upgradeButtonPrefab, _buttonsHolder);

                button.onClick.AddListener(() => clickableButtonData.OnClick?.Invoke());

                button.gameObject.AddComponent<PointerEnterHandler>().OnPointerEnterAction += () =>
                {
                    _buttonsAudioPlayer.Play("Hover");
                };

                TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
                if (button != null)
                {
                    buttonText.text = clickableButtonData.Text;
                }
            }
        }
    }
}
