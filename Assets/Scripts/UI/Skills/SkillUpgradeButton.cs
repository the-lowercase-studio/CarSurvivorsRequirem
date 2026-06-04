using System;
using System.Linq;
using Assets.ScriptableObjects.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Skills
{
    public class SkillUpgradeButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private Image _selectKeyImage;
        [SerializeField] private SkillUpgradeKeyboardIconMapping _keyboardIconMapping;

        public void Initialize(
            string text,
            int keyboardNumber,
            Action onClick,
            Action onPointerEnter = null)
        {
            ResolveMissingReferences();

            if (_button == null || _text == null)
            {
                Debug.LogWarning($"{nameof(SkillUpgradeButton)} is missing required references.", this);
                return;
            }

            _text.text = text;

            UpdateKeyboardIcon(keyboardNumber);

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
            _text ??= GetComponentsInChildren<TextMeshProUGUI>()
                .FirstOrDefault(text => text.gameObject.name != "SelectKeyText");
            _selectKeyImage ??= GetComponentsInChildren<Image>(true)
                .FirstOrDefault(image => image.gameObject.name == "SelectKey");
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
    }
}
