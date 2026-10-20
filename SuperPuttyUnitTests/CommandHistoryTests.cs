using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Data;

namespace SuperPuttyUnitTests
{
    /// <summary>Command bar history, kept per session and login</summary>
    [TestFixture]
    public class CommandHistoryTests
    {
        [Test]
        public void KeyCombinesLoginAndSession()
        {
            ClassicAssert.AreEqual("root@Prod/web01", CommandHistory.KeyFor(new SessionData { SessionId = "Prod/web01", Username = "root" }));
            ClassicAssert.AreEqual("Prod/web01", CommandHistory.KeyFor(new SessionData { SessionId = "Prod/web01" }));
            ClassicAssert.IsNull(CommandHistory.KeyFor(null));
        }

        [Test]
        public void SessionsHaveSeparateHistories()
        {
            CommandHistory history = new CommandHistory();
            history.Add("root@web01", "uptime");
            history.Add("admin@web01", "ls -la");
            history.Add("root@db01", "df -h");

            CollectionAssert.AreEqual(new[] { "uptime" }, history.GetCommands("root@web01"));
            CollectionAssert.AreEqual(new[] { "ls -la" }, history.GetCommands("admin@web01"));
            CollectionAssert.AreEqual(new[] { "df -h" }, history.GetCommands("root@db01"));
            CollectionAssert.IsEmpty(history.GetCommands(null));
        }

        [Test]
        public void NewestFirstAndRepeatedCommandMovesToTop()
        {
            CommandHistory history = new CommandHistory();
            history.Add("s", "one");
            history.Add("s", "two");
            history.Add("s", "three");
            history.Add("s", "one");

            CollectionAssert.AreEqual(new[] { "one", "three", "two" }, history.GetCommands("s"));
        }

        [Test]
        public void KeepsOnlyTheNewestEntriesPerSession()
        {
            CommandHistory history = new CommandHistory();
            history.Add("other", "keep me");
            for (int i = 0; i < CommandHistory.MaxEntriesPerSession + 10; i++)
            {
                history.Add("s", "cmd " + i);
            }

            ClassicAssert.AreEqual(CommandHistory.MaxEntriesPerSession, history.GetCommands("s").Count);
            ClassicAssert.AreEqual("cmd " + (CommandHistory.MaxEntriesPerSession + 9), history.GetCommands("s")[0]);
            CollectionAssert.AreEqual(new[] { "keep me" }, history.GetCommands("other"));
        }

        [Test]
        public void PurgeRemovesOldEntriesAndZeroDaysKeepsAll()
        {
            DateTime now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
            CommandHistory history = new CommandHistory();
            history.Add("s", "old", now.AddDays(-10));
            history.Add("s", "new", now.AddHours(-1));

            history.Purge(0, now);
            ClassicAssert.AreEqual(2, history.Count);

            history.Purge(7, now);
            CollectionAssert.AreEqual(new[] { "new" }, history.GetCommands("s"));
        }

        /// <summary>A freshly added command must survive the default retention of 0 days (it used to be purged at once)</summary>
        [Test]
        public void NewCommandSurvivesDefaultRetention()
        {
            CommandHistory history = new CommandHistory();
            history.Add("s", "uptime");
            history.Purge(0, DateTime.UtcNow);

            CollectionAssert.AreEqual(new[] { "uptime" }, history.GetCommands("s"));
        }

        [Test]
        public void SerializationRoundTrip()
        {
            CommandHistory history = new CommandHistory();
            history.Add("root@web01", "uptime");
            history.Add("root@web01", "df -h");
            history.Add("root@db01", "psql");

            CommandHistory loaded = CommandHistory.DeserializeXML(history.SerializeXML());

            CollectionAssert.AreEqual(new[] { "df -h", "uptime" }, loaded.GetCommands("root@web01"));
            CollectionAssert.AreEqual(new[] { "psql" }, loaded.GetCommands("root@db01"));
        }

        [Test]
        public void LegacyEntriesWithoutSessionAreDropped()
        {
            string legacy =
                "<?xml version=\"1.0\" encoding=\"utf-16\"?>" +
                "<ArrayOfHistoryEntry xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">" +
                "<HistoryEntry><Command>uptime</Command><TimeStamp>2026-10-01T10:00:00Z</TimeStamp></HistoryEntry>" +
                "</ArrayOfHistoryEntry>";

            ClassicAssert.AreEqual(0, CommandHistory.DeserializeXML(legacy).Count);
        }

        [Test]
        public void InvalidXmlGivesEmptyHistory()
        {
            ClassicAssert.AreEqual(0, CommandHistory.DeserializeXML("<not-history").Count);
            ClassicAssert.AreEqual(0, CommandHistory.DeserializeXML(null).Count);
        }
    }
}
