using Assets.Scripts.Player;
using Assets.Scripts.Skills;
using DG.Tweening;
using Reflex.Attributes;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.HUD
{
    public interface IPlayerSkillsHUDPresenter
    {
    }

    public class PlayerSkillsHUDPresenter : MonoBehaviour, IPlayerSkillsHUDPresenter
    {
        [Inject] private readonly IPlayerManager _playerManager = null;

        [SerializeField] private Image[] _skillIconHolders;
        [SerializeField] private GameObject[] _emptySlotFrames;
        [SerializeField] private Color _emptySlotColor = new Color(0.12f, 0.12f, 0.12f, 0.45f);
        [SerializeField] private Color _filledSlotColor = Color.white;

        private readonly List<Tween> _activeTweens = new();
        private readonly HashSet<ISkillBase> _assignedSkills = new();
        private int _assignedSlotCount = 0;

        private void Start()
        {
            InitializeSlots();

            if (_playerManager != null && _playerManager.SkillsRegistry != null)
            {
                RefreshActiveSkills();
                _playerManager.SkillsRegistry.OnSkillInitialized -= HandleSkillInitialized;
                _playerManager.SkillsRegistry.OnSkillInitialized += HandleSkillInitialized;
            }
        }

        private void OnDisable()
        {
            KillTweens();
        }

        private void OnDestroy()
        {
            if (_playerManager != null && _playerManager.SkillsRegistry != null)
            {
                _playerManager.SkillsRegistry.OnSkillInitialized -= HandleSkillInitialized;
            }

            KillTweens();
        }

        private void InitializeSlots()
        {
            if (_skillIconHolders != null)
            {
                for (int i = 0; i < _skillIconHolders.Length; i++)
                {
                    Image iconHolder = _skillIconHolders[i];
                    if (iconHolder != null)
                    {
                        iconHolder.sprite = null;
                        iconHolder.color = _emptySlotColor;
                        iconHolder.preserveAspect = true;
                        iconHolder.enabled = true;
                        iconHolder.gameObject.SetActive(true);
                    }
                }
            }

            if (_emptySlotFrames != null)
            {
                for (int i = 0; i < _emptySlotFrames.Length; i++)
                {
                    GameObject emptyFrame = _emptySlotFrames[i];
                    if (emptyFrame != null)
                    {
                        if (_skillIconHolders == null || i >= _skillIconHolders.Length || _skillIconHolders[i] == null || emptyFrame != _skillIconHolders[i].gameObject)
                        {
                            emptyFrame.SetActive(true);
                        }
                    }
                }
            }
        }

        private void RefreshActiveSkills()
        {
            if (_playerManager == null || _playerManager.SkillsRegistry == null)
            {
                return;
            }

            IReadOnlyList<ISkillBase> initializedSkills = _playerManager.SkillsRegistry.GetInitializedSkills();
            if (initializedSkills == null)
            {
                return;
            }

            for (int i = 0; i < initializedSkills.Count; i++)
            {
                AssignSkillToSlot(initializedSkills[i], false);
            }
        }

        private void HandleSkillInitialized(ISkillBase skill)
        {
            bool shouldAnimate = Time.timeSinceLevelLoad > 0.5f;
            AssignSkillToSlot(skill, shouldAnimate);
        }

        private void AssignSkillToSlot(ISkillBase skill, bool animate)
        {
            if (skill == null || skill.SkillInfo == null)
            {
                return;
            }

            if (!_assignedSkills.Add(skill))
            {
                return;
            }

            if (_skillIconHolders == null || _assignedSlotCount >= _skillIconHolders.Length)
            {
                return;
            }

            int slotIndex = _assignedSlotCount;
            _assignedSlotCount++;

            Image iconHolder = _skillIconHolders[slotIndex];
            if (iconHolder != null)
            {
                iconHolder.sprite = skill.SkillInfo.Icon;
                iconHolder.color = _filledSlotColor;
                iconHolder.preserveAspect = true;
                iconHolder.enabled = true;
                iconHolder.gameObject.SetActive(true);

                if (animate)
                {
                    Tween punchTween = iconHolder.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f);
                    _activeTweens.Add(punchTween);
                }
            }

            if (_emptySlotFrames != null && slotIndex < _emptySlotFrames.Length && _emptySlotFrames[slotIndex] != null)
            {
                if (iconHolder == null || _emptySlotFrames[slotIndex] != iconHolder.gameObject)
                {
                    _emptySlotFrames[slotIndex].SetActive(false);
                }
            }
        }

        private void KillTweens()
        {
            for (int i = 0; i < _activeTweens.Count; i++)
            {
                if (_activeTweens[i] != null && _activeTweens[i].IsActive())
                {
                    _activeTweens[i].Kill();
                }
            }

            _activeTweens.Clear();
        }
    }
}
