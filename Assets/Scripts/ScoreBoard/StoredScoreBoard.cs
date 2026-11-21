using System;
using System.Collections.Generic;
using Assets.Scripts.ScoreBoard.Constants;
using Assets.Scripts.Storage;
using Newtonsoft.Json.Linq;

namespace Assets.Scripts.ScoreBoard
{
    [Serializable]
    public struct ScoreBoardRecord : IEquatable<ScoreBoardRecord>
    {
        public uint TimeSeconds { get; set; }
        public bool HasSurvived { get; set; }

        public ScoreBoardRecord(uint timeSeconds, bool hasSurvived)
        {
            TimeSeconds = timeSeconds;
            HasSurvived = hasSurvived;
        }

        public bool Equals(ScoreBoardRecord other)
        {
            return TimeSeconds == other.TimeSeconds && HasSurvived == other.HasSurvived;
        }

        public override bool Equals(object obj)
        {
            return obj is ScoreBoardRecord other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TimeSeconds, HasSurvived);
        }
    }

    public class StoredScoreBoard : IAppStorageValue<List<ScoreBoardRecord>>
    {
        public List<ScoreBoardRecord> DefaultValue => new();

        public string GetKey()
        {
            return ScoreBoardConstants.SCORE_BOARD_STORAGE_KEY;
        }

        public List<ScoreBoardRecord> GetValueOrStoredDefault()
        {
            if (AppStorage.TryGetRawToken(GetKey(), out JToken token))
            {
                return ParseRecords(token);
            }

            return DefaultValue;
        }

        public void SaveValue(List<ScoreBoardRecord> value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value.Count > ScoreBoardConstants.MAX_SAVED_SCORES_COUNT)
            {
                throw new ArgumentException($"StoredScoreBoard can't have more then {ScoreBoardConstants.MAX_SAVED_SCORES_COUNT} scores.");
            }

            AppStorage.SetValue(GetKey(), value);
        }

        public static List<ScoreBoardRecord> ParseRecords(JToken token)
        {
            var records = new List<ScoreBoardRecord>();
            if (token == null || token.Type != JTokenType.Array)
            {
                return records;
            }

            JArray array = (JArray)token;
            for (int i = 0; i < array.Count; i++)
            {
                JToken item = array[i];
                if (item == null)
                {
                    continue;
                }

                if (item.Type == JTokenType.Integer)
                {
                    long raw = item.Value<long>();
                    uint timeSeconds = raw < 0 ? 0u : (uint)Math.Min(raw, uint.MaxValue);
                    records.Add(new ScoreBoardRecord(timeSeconds, false));
                }
                else if (item.Type == JTokenType.Object)
                {
                    try
                    {
                        ScoreBoardRecord record = item.ToObject<ScoreBoardRecord>();
                        records.Add(record);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            return records;
        }
    }
}
