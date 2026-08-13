using System.Collections.Generic;
using Assets.Scripts.ScoreBoard.Constants;

namespace Assets.Scripts.ScoreBoard
{
    public interface IScoreBoardNewScoreSaver
    {
        void Save(uint score);
    }

    public class ScoreBoardNewScoreSaver : IScoreBoardNewScoreSaver
    {
        private readonly StoredScoreBoard _storedScoreBoard;

        public ScoreBoardNewScoreSaver(StoredScoreBoard storedScoreBoard)
        {
            _storedScoreBoard = storedScoreBoard;
        }

        public void Save(uint score)
        {
            List<uint> scores = _storedScoreBoard.GetValueOrStoredDefault();
            if (scores == null)
            {
                scores = new List<uint>();
            }

            scores.Sort((a, b) => b.CompareTo(a));

            if (scores.Count >= ScoreBoardConstants.MAX_SAVED_SCORES_COUNT && scores.Count > 0)
            {
                uint lowestScore = scores[scores.Count - 1];
                if (score <= lowestScore)
                {
                    return;
                }
            }

            if (!scores.Contains(score))
            {
                scores.Add(score);
            }

            scores.Sort((a, b) => b.CompareTo(a));

            while (scores.Count > ScoreBoardConstants.MAX_SAVED_SCORES_COUNT)
            {
                scores.RemoveAt(scores.Count - 1);
            }

            _storedScoreBoard.SaveValue(scores);
        }
    }
}

