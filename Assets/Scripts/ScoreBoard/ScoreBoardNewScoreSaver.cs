using System.Collections.Generic;
using Assets.Scripts.ScoreBoard.Constants;

namespace Assets.Scripts.ScoreBoard
{
    public interface IScoreBoardNewScoreSaver
    {
        void Save(uint timeSeconds, bool hasSurvived = false);
    }

    public class ScoreBoardNewScoreSaver : IScoreBoardNewScoreSaver
    {
        private readonly StoredScoreBoard _storedScoreBoard;

        public ScoreBoardNewScoreSaver(StoredScoreBoard storedScoreBoard)
        {
            _storedScoreBoard = storedScoreBoard;
        }

        public void Save(uint timeSeconds, bool hasSurvived = false)
        {
            List<ScoreBoardRecord> scores = _storedScoreBoard.GetValueOrStoredDefault();
            if (scores == null)
            {
                scores = new List<ScoreBoardRecord>();
            }

            var newRecord = new ScoreBoardRecord(timeSeconds, hasSurvived);
            if (!InsertAndSort(scores, newRecord, ScoreBoardConstants.MAX_SAVED_SCORES_COUNT))
            {
                return;
            }

            _storedScoreBoard.SaveValue(scores);
        }

        public static bool InsertAndSort(List<ScoreBoardRecord> records, ScoreBoardRecord newRecord, int maxEntries = ScoreBoardConstants.MAX_SAVED_SCORES_COUNT)
        {
            if (records == null || maxEntries <= 0)
            {
                return false;
            }

            records.Sort(CompareRecords);

            bool alreadyPresent = ContainsRecord(records, newRecord);
            if (!alreadyPresent && records.Count >= maxEntries && records.Count > 0)
            {
                ScoreBoardRecord worstKept = records[records.Count - 1];
                if (CompareRecords(newRecord, worstKept) >= 0)
                {
                    return false;
                }
            }

            if (!alreadyPresent)
            {
                records.Add(newRecord);
            }

            records.Sort(CompareRecords);

            while (records.Count > maxEntries)
            {
                records.RemoveAt(records.Count - 1);
            }

            return true;
        }

        public static int CompareRecords(ScoreBoardRecord a, ScoreBoardRecord b)
        {
            if (a.HasSurvived != b.HasSurvived)
            {
                return a.HasSurvived ? -1 : 1;
            }

            if (a.HasSurvived)
            {
                return a.TimeSeconds.CompareTo(b.TimeSeconds);
            }

            return b.TimeSeconds.CompareTo(a.TimeSeconds);
        }

        private static bool ContainsRecord(List<ScoreBoardRecord> records, ScoreBoardRecord target)
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Equals(target))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
