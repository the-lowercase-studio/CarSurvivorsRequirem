using System;
using System.Collections.Generic;
using Assets.ScriptableObjects.Player.Skills;
using Assets.ScriptableObjects.Skills;
using Assets.Scripts.UI.Constants;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Skills
{
    public class SkillStatsGroupView : MonoBehaviour
    {
        [SerializeField] private Image _skillIcon;
        [SerializeField] private TextMeshProUGUI _skillNameText;
        [SerializeField] private RectTransform _rowsHolder;
        [SerializeField] private SkillStatRowView _statRowPrefab;

        private readonly List<SkillStatRowView> _rows = new(SkillsStatsConstants.MAX_STATS_PER_SKILL);
        private int _boundRowCount;

        private void Awake()
        {
            if (_skillIcon == null || _skillNameText == null || _rowsHolder == null || _statRowPrefab == null)
            {
                throw new InvalidOperationException("Skill stats group requires header, holder, and row prefab references.");
            }
        }

        public void Initialize()
        {
            if (_rows.Count != 0)
            {
                return;
            }

            for (int i = 0; i < SkillsStatsConstants.MAX_STATS_PER_SKILL; i++)
            {
                SkillStatRowView row = Instantiate(_statRowPrefab, _rowsHolder);
                row.Clear();
                _rows.Add(row);
            }
        }

        public void Bind(SkillInfoSO info, IReadOnlyList<NameUpgradableStatPair> stats)
        {
            if (info == null || stats.Count > SkillsStatsConstants.MAX_STATS_PER_SKILL)
            {
                throw new InvalidOperationException("Skill stats group requires skill info and at most seven stats.");
            }

            _skillIcon.sprite = info.Icon;
            _skillNameText.text = info.Name;
            _boundRowCount = stats.Count;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (i < _boundRowCount)
                {
                    _rows[i].Bind(stats[i].UpgradeableStat);
                }
                else
                {
                    _rows[i].Clear();
                }
            }

            gameObject.SetActive(true);
        }

        public void RefreshValues()
        {
            for (int i = 0; i < _boundRowCount; i++)
            {
                _rows[i].RefreshValue();
            }
        }

        public void FreezeSnapshot()
        {
            for (int i = 0; i < _boundRowCount; i++)
            {
                _rows[i].FreezeSnapshot();
            }
        }

        public void Clear()
        {
            _boundRowCount = 0;
            _skillIcon.sprite = null;
            _skillNameText.text = string.Empty;
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].Clear();
            }

            gameObject.SetActive(false);
        }
    }
}
