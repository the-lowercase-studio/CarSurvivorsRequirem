using System.Collections.Generic;
using Assets.Scripts.ScoreBoard;
using Assets.Scripts.ScoreBoard.Constants;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Assets.Scripts.Editor.Tests
{
    public class ScoreBoardDualSortTests
    {
        [Test]
        public void InsertAndSort_PlacesSurvivedBeforeDied_AndSortsEachGroupCorrectly()
        {
            var records = new List<ScoreBoardRecord>();

            ScoreBoardNewScoreSaver.InsertAndSort(records, new ScoreBoardRecord(300, false));
            ScoreBoardNewScoreSaver.InsertAndSort(records, new ScoreBoardRecord(600, false));
            ScoreBoardNewScoreSaver.InsertAndSort(records, new ScoreBoardRecord(420, true));
            ScoreBoardNewScoreSaver.InsertAndSort(records, new ScoreBoardRecord(280, true));

            Assert.AreEqual(4, records.Count);
            Assert.AreEqual(new ScoreBoardRecord(280, true), records[0]);
            Assert.AreEqual(new ScoreBoardRecord(420, true), records[1]);
            Assert.AreEqual(new ScoreBoardRecord(600, false), records[2]);
            Assert.AreEqual(new ScoreBoardRecord(300, false), records[3]);
        }

        [Test]
        public void InsertAndSort_TrimsToMaxEntries_KeepingHighestRankedRecords()
        {
            var records = new List<ScoreBoardRecord>();

            for (uint t = 100; t <= 600; t += 100)
            {
                ScoreBoardNewScoreSaver.InsertAndSort(records, new ScoreBoardRecord(t, false));
            }

            Assert.AreEqual(ScoreBoardConstants.MAX_SAVED_SCORES_COUNT, records.Count);

            bool insertedSurvived = ScoreBoardNewScoreSaver.InsertAndSort(records, new ScoreBoardRecord(350, true));
            Assert.IsTrue(insertedSurvived);
            Assert.AreEqual(ScoreBoardConstants.MAX_SAVED_SCORES_COUNT, records.Count);
            Assert.AreEqual(new ScoreBoardRecord(350, true), records[0]);
            Assert.AreEqual(new ScoreBoardRecord(200, false), records[records.Count - 1]);

            bool insertedSlowSurvived = ScoreBoardNewScoreSaver.InsertAndSort(records, new ScoreBoardRecord(500, true));
            Assert.IsTrue(insertedSlowSurvived);
            Assert.AreEqual(new ScoreBoardRecord(350, true), records[0]);
            Assert.AreEqual(new ScoreBoardRecord(500, true), records[1]);
            Assert.AreEqual(new ScoreBoardRecord(300, false), records[records.Count - 1]);
        }

        [Test]
        public void InsertAndSort_RejectsWorseRecordWhenFull_AndAvoidsDuplicates()
        {
            var records = new List<ScoreBoardRecord>();
            for (uint t = 200; t <= 700; t += 100)
            {
                ScoreBoardNewScoreSaver.InsertAndSort(records, new ScoreBoardRecord(t, true));
            }

            bool insertedSlower = ScoreBoardNewScoreSaver.InsertAndSort(records, new ScoreBoardRecord(800, true));
            Assert.IsFalse(insertedSlower);

            bool insertedDied = ScoreBoardNewScoreSaver.InsertAndSort(records, new ScoreBoardRecord(999, false));
            Assert.IsFalse(insertedDied);

            bool insertedDuplicate = ScoreBoardNewScoreSaver.InsertAndSort(records, new ScoreBoardRecord(200, true));
            Assert.IsTrue(insertedDuplicate);
            Assert.AreEqual(ScoreBoardConstants.MAX_SAVED_SCORES_COUNT, records.Count);
        }

        [Test]
        public void ParseRecords_MigratesLegacyUintArrayAsDiedRecords()
        {
            JToken legacyToken = JToken.Parse("[120, 450, 95]");
            List<ScoreBoardRecord> migrated = StoredScoreBoard.ParseRecords(legacyToken);

            Assert.AreEqual(3, migrated.Count);
            Assert.AreEqual(new ScoreBoardRecord(120, false), migrated[0]);
            Assert.AreEqual(new ScoreBoardRecord(450, false), migrated[1]);
            Assert.AreEqual(new ScoreBoardRecord(95, false), migrated[2]);
        }

        [Test]
        public void TryGetBestTime_DistinguishesSurvivedMinAndDiedMax()
        {
            var records = new List<ScoreBoardRecord>
            {
                new ScoreBoardRecord(410, true),
                new ScoreBoardRecord(290, true),
                new ScoreBoardRecord(520, false),
                new ScoreBoardRecord(180, false)
            };

            Assert.IsTrue(ScoreBoardBestScoreGetter.TryGetBestTime(records, true, out uint bestSurvived));
            Assert.AreEqual(290u, bestSurvived);

            Assert.IsTrue(ScoreBoardBestScoreGetter.TryGetBestTime(records, false, out uint bestDied));
            Assert.AreEqual(520u, bestDied);

            var onlyDied = new List<ScoreBoardRecord> { new ScoreBoardRecord(180, false) };
            Assert.IsFalse(ScoreBoardBestScoreGetter.TryGetBestTime(onlyDied, true, out _));
        }
    }
}
