using Assets.Scripts.Audio;
using Assets.Scripts.EventHandlers;
using Assets.Scripts.Extensions;
using Assets.Scripts.GameManipulators;
using Assets.Scripts.Player;
using Assets.Scripts.Skills;
using Assets.Scripts.Skills.ObjectsImpactingSkills.Crate;
using Assets.Scripts.Spawners.GridSpace;
using Assets.Scripts.Stats;
using Assets.Scripts.UI.Level;
using Reflex.Attributes;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Skills
{
    public class SkillUpgradePresenter : MonoBehaviour
    {
        [Inject] private readonly IPlayerManager _playerManager;
        [Inject] private readonly IPlayerLevelPresenter _playerLevelPresenter;
        [Inject] private readonly IOnRandomGridPosSpawner<CollectibleItemsSpawner> _collectibleItemsSpawner;

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

        private const byte MAX_SKILLS_UPGRADE_BUTTONS = 3;
        private const string SKILL_NAME_TEMPLATE = "New Skill: {0}";

        private Queue<ISkillBase> _skillsQueuedForInitialization = new Queue<ISkillBase>();
        private Queue<IUpgradeableSkill> _skillsQueuedForUpgrade = new Queue<IUpgradeableSkill>();

        private SkillsVisualPresenter _skillsVisualPresenter;

        private bool _isShowingAnySection;

        private IAudioClipPlayer _audioClipPlayer;

        private void Awake()
        {
            _audioClipPlayer = GetComponentInChildren<IAudioClipPlayer>();
        }

        private void Start()
        {
            _skillsVisualPresenter = GameObject
                .FindGameObjectWithTag(typeof(SkillsVisualPresenter).Name)
                .GetComponent<SkillsVisualPresenter>();

            _collectibleItemsSpawner.OnSpawnedEntityReleased += ShowRandomSkillInInitializationOrUpgradeSection_OnEvent;
            _playerLevelPresenter.OnExpSliderVisualEndValueReached += ShowRandomSkillInInitializationOrUpgradeSection_OnEvent;
            _continueButton.onClick.AddListener(() => HandleUpgradeableOrInitializableSkillsShowing());
        }

        private void ShowRandomSkillInInitializationOrUpgradeSection_OnEvent(object sender, System.EventArgs e)
        {
            ISkillsRegistry skillsRegistry = _playerManager.SkillsRegistry;
            if (_skillsQueuedForInitialization.Count < skillsRegistry.UninitializedSkillsCount)
            {
                ISkillBase skill = RandomUninitializedSkillsInitializator.Initialize(skillsRegistry);
                if (skill is not null)
                {
                    _skillsQueuedForInitialization.Enqueue(skill);
                }
            }
            else
            {
                IUpgradeableSkill upgradeableSkill = RandomUpgradeableSkillFinder.Find(skillsRegistry);
                if (upgradeableSkill is not null)
                {
                    _skillsQueuedForUpgrade.Enqueue(RandomUpgradeableSkillFinder.Find(skillsRegistry));
                }
            }

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

            if (_skillsQueuedForInitialization.Count > 0)
            {
                ISkillBase skill = _skillsQueuedForInitialization.Dequeue();
                _playerManager.SkillsRegistry.InitializeSkill(skill);
                ShowNewSkillSection(skill);
                _audioClipPlayer.Play("Show");
                GameTime.Pause();
            }
            else if (_skillsQueuedForUpgrade.Count > 0)
            {
                IUpgradeableSkill skill = GetQueuedUpgradeableSkillThatIsStillReadyForUpgrade();

                if (skill is not null)
                {
                    ShowStatsUpgradeSection(skill);
                    _audioClipPlayer.Play("Show");
                    GameTime.Pause();
                }
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

        private IUpgradeableSkill GetQueuedUpgradeableSkillThatIsStillReadyForUpgrade()
        {
            IUpgradeableSkill skill = null;

            while (_skillsQueuedForUpgrade.Count > 0)
            {
                skill = _skillsQueuedForUpgrade.Dequeue();
                if (skill.CanBeUgraded())
                {
                    break;
                }
            }

            return skill;
        }

        private void ShowStatsUpgradeSection(IUpgradeableSkill upgradeableStats)
        {
            List<ClickableButtonData> skillStatsUpgradeButtonsData = new List<ClickableButtonData>();

            foreach (var nameUpgradeableStatPair in upgradeableStats.Config.GetUpgradeableStatsThatCanBeUpgraded())
            {
                float upgradeValue = nameUpgradeableStatPair.UpgradeableStat.GetUpgradeValueBasedOnUpdateRange();
                IUpgradeableStat upgradeableStat = nameUpgradeableStatPair.UpgradeableStat;

                string changeInfo = upgradeableStat.IsSubstractModeOn ? "Decrease" : "Increase";
                string statName = nameUpgradeableStatPair.Name.PascalCaseToWords();
                string statUnit = upgradeableStat.Unit.ToDisplayString();
                float statValue = upgradeableStat.Unit == StatsUnits.Percentage
                    ? upgradeableStat.GetWhatPercentOfValueIsUpgradeValue(upgradeValue)
                    : upgradeValue;

                skillStatsUpgradeButtonsData.Add(new ClickableButtonData
                {
                    Text = $"{changeInfo} <b>{statName}</b> by <Color=#F8D61C>{statValue}{statUnit}</Color>",

                    OnClick = () =>
                    {
                        nameUpgradeableStatPair.UpgradeableStat.Upgrade(upgradeValue);
                        HandleUpgradeableOrInitializableSkillsShowing();
                        _buttonsAudioPlayer.Play("Click");
                    }
                });
            }

            DisplayNewButtons(skillStatsUpgradeButtonsData.Shuffle().Take(MAX_SKILLS_UPGRADE_BUTTONS));

            _skillsVisualPresenter.ShowSkillVisualBasedOnSkillInfo(upgradeableStats.SkillInfo);

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

                button.AddComponent<PointerEnterHandler>().OnPointerEnterAction += () =>
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
