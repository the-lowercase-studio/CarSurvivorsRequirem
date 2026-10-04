using System;
using System.Collections;
using System.Collections.Generic;
using Assets.ScriptableObjects.Player.Skills;
using Assets.Scripts.Common.EventArgs;
using Assets.Scripts.GameFlow;
using Assets.Scripts.Player;
using Assets.Scripts.Skills;
using Assets.Scripts.Skills.Constants;
using Assets.Scripts.Stats;
using Assets.Scripts.UI.Constants;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.UI.Skills
{
    public interface ISkillsStatsPresenter
    {
        void SetPauseVisible(bool isVisible);
        void ShowDeathStats();
    }

    public class SkillsStatsPresenter : MonoBehaviour, ISkillsStatsPresenter
    {
        [Inject] private readonly IPlayerManager _playerManager = null;
        [Inject] private readonly IGameSceneLoader _gameSceneLoader = null;

        [SerializeField] private GameObject _visual;
        [SerializeField] private RectTransform _skillGroupsHolder;
        [SerializeField] private SkillStatsGroupView _skillGroupPrefab;

        private readonly List<SkillStatsGroupView> _groups = new(SkillConstants.MAX_ACTIVE_SKILLS);
        private readonly List<IUpgradeableSkill> _ownedSkills = new(SkillConstants.MAX_ACTIVE_SKILLS);
        private readonly List<NameUpgradableStatPair> _stats = new(SkillsStatsConstants.MAX_STATS_PER_SKILL);
        private readonly HashSet<IUpgradeableStat> _subscribedStats = new();
        private ISkillsRegistry _registry;
        private Coroutine _initializationRoutine;
        private DisplayMode _mode;
        private bool _hasStarted;
        private bool _isReady;
        private bool _hasDeathSnapshot;
        private bool _isRegistrySubscribed;
        private bool _isSceneLoadingSubscribed;
        private bool _isSceneLoading;

        private enum DisplayMode
        {
            Hidden,
            Pause,
            Death
        }

        private void Awake()
        {
            if (_visual == null || _skillGroupsHolder == null || _skillGroupPrefab == null || _visual == gameObject)
            {
                throw new InvalidOperationException("Skills stats presenter requires a child Visual, group holder, and group prefab.");
            }

            _visual.SetActive(false);
        }

        private void OnEnable()
        {
            if (_hasStarted && !_isSceneLoading)
            {
                ConnectSceneLoading();
                if (_isReady)
                {
                    ApplyRequestedMode();
                }
                else
                {
                    _initializationRoutine = StartCoroutine(InitializeAfterSceneStart());
                }
            }
        }

        private void Start()
        {
            _hasStarted = true;
            ConnectSceneLoading();
            _initializationRoutine = StartCoroutine(InitializeAfterSceneStart());
        }

        private void OnDisable()
        {
            StopInitialization();
            DisconnectSceneLoading();
            ReleaseLiveReferences();
            _visual.SetActive(false);
            if (!_hasDeathSnapshot)
            {
                ClearGroups();
            }
        }

        private void OnDestroy()
        {
            DisconnectSceneLoading();
            ReleaseLiveReferences();
        }

        public void SetPauseVisible(bool isVisible)
        {
            if (_mode == DisplayMode.Death || _isSceneLoading)
            {
                return;
            }

            _mode = isVisible ? DisplayMode.Pause : DisplayMode.Hidden;
            if (isActiveAndEnabled && _isReady)
            {
                ApplyRequestedMode();
            }
        }

        public void ShowDeathStats()
        {
            if (_mode == DisplayMode.Death || _isSceneLoading)
            {
                return;
            }

            _mode = DisplayMode.Death;
            if (isActiveAndEnabled && _isReady)
            {
                ApplyRequestedMode();
            }
        }

        private IEnumerator InitializeAfterSceneStart()
        {
            // Registry.Start resets runtime configs and initializes the starting skill.
            yield return null;

            while (_groups.Count < SkillConstants.MAX_ACTIVE_SKILLS)
            {
                SkillStatsGroupView group = Instantiate(_skillGroupPrefab, _skillGroupsHolder);
                group.Initialize();
                group.Clear();
                _groups.Add(group);
            }

            _isReady = true;
            _initializationRoutine = null;
            ApplyRequestedMode();
        }

        private void ApplyRequestedMode()
        {
            if (_mode == DisplayMode.Hidden)
            {
                ReleaseLiveReferences();
                ClearGroups();
                _visual.SetActive(false);
                return;
            }

            if (_mode == DisplayMode.Death && _hasDeathSnapshot)
            {
                _visual.SetActive(true);
                return;
            }

            RebuildGroups();
            if (_mode == DisplayMode.Pause)
            {
                _registry.OnSkillInitialized += OnSkillInitialized;
                _isRegistrySubscribed = true;
            }
            else
            {
                for (int i = 0; i < _ownedSkills.Count; i++)
                {
                    _groups[i].FreezeSnapshot();
                }

                _hasDeathSnapshot = true;
                ReleaseLiveReferences();
            }

            _visual.SetActive(true);
        }

        private void RebuildGroups()
        {
            ReleaseLiveReferences();
            _registry = _playerManager.SkillsRegistry;
            if (_registry == null || _registry.Skills == null)
            {
                throw new InvalidOperationException("Skills stats presenter requires an initialized skills registry.");
            }

            for (int i = 0; i < _registry.Skills.Count; i++)
            {
                ISkillBase skill = _registry.Skills[i];
                if (!skill.IsInitialized())
                {
                    continue;
                }

                if (skill is not IUpgradeableSkill upgradeableSkill)
                {
                    throw new InvalidOperationException("An owned skill requires an explicit upgradeable stats display contract.");
                }

                _ownedSkills.Add(upgradeableSkill);
            }

            if (_ownedSkills.Count > SkillConstants.MAX_ACTIVE_SKILLS)
            {
                throw new InvalidOperationException("Owned skills exceed the skills stats panel capacity.");
            }

            for (int i = 0; i < _groups.Count; i++)
            {
                if (i >= _ownedSkills.Count)
                {
                    _groups[i].Clear();
                    continue;
                }

                _stats.Clear();
                if (_ownedSkills[i].Config == null)
                {
                    throw new InvalidOperationException("An owned skill requires an initialized stats config.");
                }
                _ownedSkills[i].Config.AppendStatsForDisplay(_stats);
                _groups[i].Bind(_ownedSkills[i].SkillInfo, _stats);
                if (_mode == DisplayMode.Pause)
                {
                    for (int j = 0; j < _stats.Count; j++)
                    {
                        IUpgradeableStat stat = _stats[j].UpgradeableStat;
                        if (_subscribedStats.Add(stat))
                        {
                            stat.OnUpgrade += OnStatUpgraded;
                        }
                    }
                }
            }

            _stats.Clear();
        }

        private void ReleaseLiveReferences()
        {
            if (_isRegistrySubscribed)
            {
                _registry.OnSkillInitialized -= OnSkillInitialized;
                _isRegistrySubscribed = false;
            }

            foreach (IUpgradeableStat stat in _subscribedStats)
            {
                stat.OnUpgrade -= OnStatUpgraded;
            }

            _subscribedStats.Clear();
            _ownedSkills.Clear();
            _stats.Clear();
            _registry = null;
        }

        private void ClearGroups()
        {
            for (int i = 0; i < _groups.Count; i++)
            {
                _groups[i].Clear();
            }
        }

        private void StopInitialization()
        {
            if (_initializationRoutine != null)
            {
                StopCoroutine(_initializationRoutine);
                _initializationRoutine = null;
            }
        }

        private void ConnectSceneLoading()
        {
            if (!_isSceneLoadingSubscribed)
            {
                _gameSceneLoader.OnStartLoadingScene += OnStartLoadingScene;
                _isSceneLoadingSubscribed = true;
            }
        }

        private void DisconnectSceneLoading()
        {
            if (_isSceneLoadingSubscribed)
            {
                _gameSceneLoader.OnStartLoadingScene -= OnStartLoadingScene;
                _isSceneLoadingSubscribed = false;
            }
        }

        private void OnSkillInitialized(ISkillBase skill)
        {
            ApplyRequestedMode();
        }

        private void OnStatUpgraded(object sender, EventArgs args)
        {
            for (int i = 0; i < _ownedSkills.Count; i++)
            {
                _groups[i].RefreshValues();
            }
        }

        private void OnStartLoadingScene(object sender, ValueEventArgs<GameScene> args)
        {
            _isSceneLoading = true;
            _mode = DisplayMode.Hidden;
            _hasDeathSnapshot = false;
            StopInitialization();
            ReleaseLiveReferences();
            ClearGroups();
            _visual.SetActive(false);
            DisconnectSceneLoading();
        }
    }
}
