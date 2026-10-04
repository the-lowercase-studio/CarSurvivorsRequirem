using System;
using Assets.Scripts.Stats;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Skills
{
    public class SkillStatRowView : MonoBehaviour
    {
        [SerializeField] private Image _statIcon;
        [SerializeField] private TextMeshProUGUI _valueText;

        private IUpgradeableStat _stat;

        private void Awake()
        {
            if (_statIcon == null || _valueText == null)
            {
                throw new InvalidOperationException("Skill stat row requires its icon and value text.");
            }
        }

        public void Bind(IUpgradeableStat stat)
        {
            if (stat == null)
            {
                throw new InvalidOperationException("Skill stat row requires a runtime stat.");
            }

            _stat = stat;
            _statIcon.sprite = stat.Icon;
            RefreshValue();
            gameObject.SetActive(true);
        }

        public void RefreshValue()
        {
            _valueText.text = SkillStatValueFormatter.Format(_stat);
        }

        public void FreezeSnapshot()
        {
            _stat = null;
        }

        public void Clear()
        {
            _stat = null;
            _statIcon.sprite = null;
            _valueText.text = string.Empty;
            gameObject.SetActive(false);
        }
    }
}
