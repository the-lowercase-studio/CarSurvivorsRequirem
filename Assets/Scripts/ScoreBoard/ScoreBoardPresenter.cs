using Reflex.Attributes;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.ScoreBoard
{
    public class ScoreBoardPresenter : MonoBehaviour
    {
        [Inject] private readonly StoredScoreBoard _storedScoreBoard = null;

        [SerializeField] private ScoreBoardEntry _scoreBoardEntryPrefab;
        [SerializeField] private Transform _scoreBoardEntriesParent;

        private void OnEnable()
        {
            List<uint> scoreBoardValues = _storedScoreBoard.GetValueOrStoredDefault();

            for (int i = 0; i < scoreBoardValues.Count; i++)
            {
                var entry = Instantiate(_scoreBoardEntryPrefab, _scoreBoardEntriesParent);
                entry.SetOrderNumber((byte)(i + 1));
                entry.SetScore(scoreBoardValues[i]);
            }
        }

        private void OnDisable()
        {
            foreach (Transform child in _scoreBoardEntriesParent)
            {
                Destroy(child.gameObject);
            }
        }
    }
}
