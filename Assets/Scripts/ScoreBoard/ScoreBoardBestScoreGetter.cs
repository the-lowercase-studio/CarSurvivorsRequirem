using System.Collections.Generic;

namespace Assets.Scripts.ScoreBoard
{
    public interface IScoreBoardBestScoreGetter
    {
        uint GetBestScore();
    }

    public class ScoreBoardBestScoreGetter : IScoreBoardBestScoreGetter
    {
        private readonly StoredScoreBoard _storedScoreBoard;

        public ScoreBoardBestScoreGetter(StoredScoreBoard storedScoreBoard)
        {
            _storedScoreBoard = storedScoreBoard;
        }

        public uint GetBestScore()
        {
            List<uint> scoreBoardValues = _storedScoreBoard.GetValueOrStoredDefault();

            if (scoreBoardValues == null || scoreBoardValues.Count == 0)
            {
                return 0;
            }

            uint maxScore = scoreBoardValues[0];
            for (int i = 1; i < scoreBoardValues.Count; i++)
            {
                if (scoreBoardValues[i] > maxScore)
                {
                    maxScore = scoreBoardValues[i];
                }
            }

            return maxScore;
        }
    }
}

