using System;
using System.Collections.Generic;
using Assets.Scripts.ScoreBoard.Constants;
using Assets.Scripts.Storage;

namespace Assets.Scripts.ScoreBoard
{
    public class StoredScoreBoard : IAppStorageValue<List<uint>>
    {
        public List<uint> DefaultValue => new();

        public string GetKey()
        {
            return ScoreBoardConstants.SCORE_BOARD_STORAGE_KEY;
        }

        public List<uint> GetValueOrStoredDefault()
        {
            if (AppStorage.TryGetValue<List<uint>>(GetKey(), out var value))
            {
                return value;
            }

            return DefaultValue;
        }

        public void SaveValue(List<uint> value)
        {
            if (value.Count > ScoreBoardConstants.MAX_SAVED_SCORES_COUNT)
            {
                throw new ArgumentException($"StoredScoreBoard can't have more then {ScoreBoardConstants.MAX_SAVED_SCORES_COUNT} scores.");
            }

            AppStorage.SetValue(GetKey(), value);
        }
    }
}

