using System;
using Assets.Scripts.ScoreBoard.Constants;
using Assets.Scripts.Utils;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.ScoreBoard
{
    public class ScoreBoardEntry : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _orderNumberText;
        [SerializeField] private TextMeshProUGUI _scoreText;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private Color _survivedStatusColor = new Color(0.97f, 0.84f, 0.11f, 1f);
        [SerializeField] private Color _diedStatusColor = new Color(0.85f, 0.25f, 0.25f, 1f);

        private void Awake()
        {
            if (_orderNumberText == null)
            {
                throw new InvalidOperationException($"[{nameof(ScoreBoardEntry)}] _orderNumberText is unassigned on '{name}'.");
            }

            if (_scoreText == null)
            {
                throw new InvalidOperationException($"[{nameof(ScoreBoardEntry)}] _scoreText is unassigned on '{name}'.");
            }

            if (_statusText == null)
            {
                throw new InvalidOperationException($"[{nameof(ScoreBoardEntry)}] _statusText is unassigned on '{name}'.");
            }
        }

        public void SetOrderNumber(byte number)
        {
            _orderNumberText.text = number.ToString();
        }

        public void SetScore(uint score)
        {
            _scoreText.text = TimeConversionUtility.FormatSecondsToTimeString(score);
        }

        public void SetStatus(bool hasSurvived)
        {
            _statusText.text = hasSurvived
                ? ScoreBoardConstants.SURVIVED_STATUS_LABEL
                : ScoreBoardConstants.DIED_STATUS_LABEL;
            _statusText.color = hasSurvived
                ? _survivedStatusColor
                : _diedStatusColor;
        }
    }
}
