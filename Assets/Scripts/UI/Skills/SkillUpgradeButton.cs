using System;
using Assets.ScriptableObjects.UI;
using Assets.Scripts.Skills.UpgradeFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Skills
{
    public class SkillUpgradeButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private Image _statIconImage;
        [SerializeField] private Image _selectKeyImage;
        [SerializeField] private SkillUpgradeKeyboardIconMapping _keyboardIconMapping;
        [SerializeField] private Image _rarityBackgroundImage;
        [SerializeField] private SkillUpgradeRaritySpriteMapping _raritySpriteMapping;

        public void Initialize(
            string text,
            int keyboardNumber,
            SkillUpgradeRarity rarity,
            Action onClick,
            Action onPointerEnter = null,
            Sprite icon = null)
        {
            ResolveMissingReferences();

            if (_button == null || _text == null)
            {
                Debug.LogWarning($"{nameof(SkillUpgradeButton)} is missing required references.", this);
                return;
            }

            _text.text = text;

            UpdateKeyboardIcon(keyboardNumber);
            UpdateRarityBackground(rarity);
            UpdateStatIcon(icon);

            _button.onClick.AddListener(() => onClick?.Invoke());

            PointerEnterHandler pointerEnterHandler = _button.gameObject.AddComponent<PointerEnterHandler>();

            if (onPointerEnter != null)
            {
                pointerEnterHandler.OnPointerEnterAction += () => onPointerEnter?.Invoke();
            }
        }

        public void Invoke()
        {
            if (_button == null)
            {
                return;
            }

            _button.onClick.Invoke();
        }

        private void ResolveMissingReferences()
        {
            _button ??= GetComponentInChildren<Button>();

            if (_text == null)
            {
                TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>();
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i].gameObject.name != "SelectKeyText")
                    {
                        _text = texts[i];
                        break;
                    }
                }
            }

            if (_statIconImage == null)
            {
                Image[] images = GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    string imageName = images[i].gameObject.name;
                    if (imageName == "StatIcon" || imageName == "Icon")
                    {
                        _statIconImage = images[i];
                        break;
                    }
                }
            }

            if (_selectKeyImage == null)
            {
                Image[] images = GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    if (images[i].gameObject.name == "SelectKey")
                    {
                        _selectKeyImage = images[i];
                        break;
                    }
                }
            }

            _rarityBackgroundImage ??= _button.targetGraphic as Image;
            _rarityBackgroundImage ??= _button.GetComponent<Image>();
        }

        private void UpdateStatIcon(Sprite icon)
        {
            if (_statIconImage == null)
            {
                return;
            }

            if (icon != null)
            {
                _statIconImage.sprite = icon;
                _statIconImage.enabled = true;
            }
            else
            {
                _statIconImage.sprite = null;
                _statIconImage.enabled = false;
            }
        }

        private void UpdateKeyboardIcon(int keyboardNumber)
        {
            if (_selectKeyImage == null)
            {
                return;
            }

            if (_keyboardIconMapping != null && _keyboardIconMapping.TryGetIcon(keyboardNumber, out Sprite icon))
            {
                _selectKeyImage.sprite = icon;
                _selectKeyImage.enabled = true;
                return;
            }

            _selectKeyImage.sprite = null;
            _selectKeyImage.enabled = false;
        }

        private void UpdateRarityBackground(SkillUpgradeRarity rarity)
        {
            if (_rarityBackgroundImage == null
                || _raritySpriteMapping == null
                || !_raritySpriteMapping.TryGetVisual(rarity, out Sprite sprite, out Color color))
            {
                return;
            }

            _rarityBackgroundImage.sprite = sprite;
            _rarityBackgroundImage.color = color;
        }
    }
}
