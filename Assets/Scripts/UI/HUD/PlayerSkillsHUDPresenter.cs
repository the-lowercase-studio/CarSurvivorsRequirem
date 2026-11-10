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

        [Header("Slot References")]
        [Tooltip("Slot background images or holders displayed in the HUD.")]
        [SerializeField] private Image[] _skillIconHolders;
        [Tooltip("Optional explicit foreground icon images. If not assigned, child icon images are resolved or created automatically.")]
        [SerializeField] private Image[] _iconImages;
        [Tooltip("Optional frames or overlays shown only when slots are empty.")]
        [SerializeField] private GameObject[] _emptySlotFrames;

        [Header("Visual Settings")]
        [Tooltip("Background color of the slot.")]
        [SerializeField] private Color _emptySlotColor = new Color(0.12f, 0.12f, 0.12f, 0.45f);
        [Tooltip("Tint color applied to the foreground skill icon sprite.")]
        [SerializeField] private Color _filledSlotColor = Color.white;

        private readonly List<Tween> _activeTweens = new();
        private readonly HashSet<ISkillBase> _assignedSkills = new();
        private readonly List<Image> _resolvedIconImages = new();
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
            _resolvedIconImages.Clear();

            if (_skillIconHolders != null)
            {
                for (int i = 0; i < _skillIconHolders.Length; i++)
                {
                    Image slotBackground = _skillIconHolders[i];
                    if (slotBackground != null)
                    {
                        slotBackground.sprite = null;
                        slotBackground.color = _emptySlotColor;
                        slotBackground.preserveAspect = true;
                        slotBackground.enabled = true;
                        slotBackground.gameObject.SetActive(true);

                        Image iconImage = GetOrResolveIconImage(i, slotBackground);
                        if (iconImage != null)
                        {
                            iconImage.sprite = null;
                            iconImage.color = _filledSlotColor;
                            iconImage.preserveAspect = true;
                            iconImage.enabled = false;
                        }

                        _resolvedIconImages.Add(iconImage);
                    }
                    else
                    {
                        _resolvedIconImages.Add(null);
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

            Image slotBackground = _skillIconHolders[slotIndex];
            if (slotBackground != null)
            {
                slotBackground.color = _emptySlotColor;
                slotBackground.enabled = true;
                slotBackground.gameObject.SetActive(true);
            }

            Image iconImage = slotIndex < _resolvedIconImages.Count ? _resolvedIconImages[slotIndex] : null;
            if (iconImage == null && slotBackground != null)
            {
                iconImage = GetOrResolveIconImage(slotIndex, slotBackground);
            }

            if (iconImage != null)
            {
                iconImage.sprite = skill.SkillInfo.Icon;
                iconImage.color = _filledSlotColor;
                iconImage.preserveAspect = true;
                iconImage.enabled = true;
                iconImage.gameObject.SetActive(true);

                if (animate)
                {
                    Transform punchTarget = slotBackground != null ? slotBackground.transform : iconImage.transform;
                    Tween punchTween = punchTarget.DOPunchScale(Vector3.one * 0.2f, 0.3f);
                    _activeTweens.Add(punchTween);
                }
            }

            if (_emptySlotFrames != null && slotIndex < _emptySlotFrames.Length && _emptySlotFrames[slotIndex] != null)
            {
                if (slotBackground == null || _emptySlotFrames[slotIndex] != slotBackground.gameObject)
                {
                    _emptySlotFrames[slotIndex].SetActive(false);
                }
            }
        }

        private Image GetOrResolveIconImage(int index, Image slotBackground)
        {
            if (slotBackground == null)
            {
                return null;
            }

            if (_iconImages != null && index < _iconImages.Length && _iconImages[index] != null)
            {
                return _iconImages[index];
            }

            Image[] childImages = slotBackground.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < childImages.Length; i++)
            {
                if (childImages[i] != null && childImages[i] != slotBackground)
                {
                    return childImages[i];
                }
            }

            GameObject iconGameObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGameObject.transform.SetParent(slotBackground.transform, false);

            RectTransform rectTransform = iconGameObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;

            Image iconImage = iconGameObject.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            iconImage.enabled = false;

            return iconImage;
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

        private void HandleSkillInitialized(ISkillBase skill)
        {
            bool shouldAnimate = Time.timeSinceLevelLoad > 0.5f;
            AssignSkillToSlot(skill, shouldAnimate);
        }
    }
}
