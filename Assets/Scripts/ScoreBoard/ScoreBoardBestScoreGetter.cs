using System.Collections.Generic;

namespace Assets.Scripts.ScoreBoard
{
    public interface IScoreBoardBestScoreGetter
    {
        bool TryGetBestTime(bool hasSurvived, out uint bestTime);
    }

    public class ScoreBoardBestScoreGetter : IScoreBoardBestScoreGetter
    {
        private readonly StoredScoreBoard _storedScoreBoard;

        public ScoreBoardBestScoreGetter(StoredScoreBoard storedScoreBoard)
        {
            _storedScoreBoard = storedScoreBoard;
        }

        public bool TryGetBestTime(bool hasSurvived, out uint bestTime)
        {
            List<ScoreBoardRecord> records = _storedScoreBoard.GetValueOrStoredDefault();
            return TryGetBestTime(records, hasSurvived, out bestTime);
        }

        public static bool TryGetBestTime(IReadOnlyList<ScoreBoardRecord> records, bool hasSurvived, out uint bestTime)
        {
            bestTime = 0;
            if (records == null || records.Count == 0)
            {
                return false;
            }

            bool found = false;
            for (int i = 0; i < records.Count; i++)
            {
                ScoreBoardRecord record = records[i];
                if (record.HasSurvived != hasSurvived)
                {
                    continue;
                }

                if (!found)
                {
                    bestTime = record.TimeSeconds;
                    found = true;
                }
                else if (hasSurvived && record.TimeSeconds < bestTime)
                {
                    bestTime = record.TimeSeconds;
                }
                else if (!hasSurvived && record.TimeSeconds > bestTime)
                {
                    bestTime = record.TimeSeconds;
                }
            }

            return found;
        }
    }
}
