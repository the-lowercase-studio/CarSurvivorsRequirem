using System.Collections.Generic;
using Reflex.Attributes;
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
            List<ScoreBoardRecord> scoreBoardRecords = _storedScoreBoard.GetValueOrStoredDefault();
            scoreBoardRecords.Sort(ScoreBoardNewScoreSaver.CompareRecords);

            for (int i = 0; i < scoreBoardRecords.Count; i++)
            {
                ScoreBoardRecord record = scoreBoardRecords[i];
                ScoreBoardEntry entry = Instantiate(_scoreBoardEntryPrefab, _scoreBoardEntriesParent);
                entry.SetOrderNumber((byte)(i + 1));
                entry.SetScore(record.TimeSeconds);
                entry.SetStatus(record.HasSurvived);
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
