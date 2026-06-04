using System;
using UnityEngine;

namespace Assets.ScriptableObjects.UI
{
    [CreateAssetMenu(fileName = "SkillUpgradeKeyboardIconMapping", menuName = "Scriptable Objects/UI/Skill Upgrade Keyboard Icon Mapping")]
    public class SkillUpgradeKeyboardIconMapping : ScriptableObject
    {
        [SerializeField] private KeyboardNumberIcon[] _icons;

        public bool TryGetIcon(int buttonNumber, out Sprite icon)
        {
            if (_icons == null)
            {
                icon = null;
                return false;
            }

            foreach (KeyboardNumberIcon keyboardNumberIcon in _icons)
            {
                if (keyboardNumberIcon.ButtonNumber == buttonNumber)
                {
                    icon = keyboardNumberIcon.Icon;
                    return icon != null;
                }
            }

            icon = null;
            return false;
        }
    }

    [Serializable]
    public struct KeyboardNumberIcon
    {
        [field: SerializeField] public int ButtonNumber { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
    }
}
