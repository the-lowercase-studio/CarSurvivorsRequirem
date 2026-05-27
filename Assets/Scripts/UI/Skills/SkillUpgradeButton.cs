using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Skills
{
    public class SkillUpgradeButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private TextMeshProUGUI _selectKeyText;

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

            if (_selectKeyText != null)
            {
                _selectKeyText.text = keyboardNumber.ToString();
            }

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
            _text ??= GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(text => text != _selectKeyText);
        }
    }
}
