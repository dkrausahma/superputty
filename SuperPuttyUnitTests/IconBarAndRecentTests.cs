using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Data;
using SuperPutty.Utils;

namespace SuperPuttyUnitTests
{
    /// <summary>Configurable icon bar (stored layout) and the recent sessions list</summary>
    [TestFixture]
    public class IconBarAndRecentTests
    {
        private static readonly string[] Known = { "sessions", "recent", "layouts", "commands", "containers", "hostinfo", "copysession", "upload", "transfer", "kiosk", "options" };

        [Test]
        public void EmptyLayoutIsTheDefault()
        {
            CollectionAssert.AreEqual(IconBarLayout.Default.Split(','), IconBarLayout.Parse("", Known));
            CollectionAssert.AreEqual(IconBarLayout.Default.Split(','), IconBarLayout.Parse(null, Known));
            CollectionAssert.AreEqual(IconBarLayout.Default.Split(','), IconBarLayout.Parse("|,gone,|", Known), "nothing usable left");
        }

        [Test]
        public void LayoutKeepsKnownToolsOnceInOrder()
        {
            CollectionAssert.AreEqual(new[] { "options", "sessions", "|", "kiosk" },
                IconBarLayout.Parse("options, sessions,removedtool,sessions,|,kiosk", Known));
        }

        [Test]
        public void SeparatorsOnlyBetweenIcons()
        {
            CollectionAssert.AreEqual(new[] { "sessions", "|", "kiosk" },
                IconBarLayout.Normalize(new[] { "|", "sessions", "|", "|", "gone", "|", "kiosk", "|" }, Known));
            ClassicAssert.AreEqual("sessions,|,kiosk", IconBarLayout.Format(IconBarLayout.Parse("sessions,|,|,kiosk,|", Known)));
        }

        [Test]
        public void SpacesRepeatForWiderGapsButNotAtTheEnds()
        {
            CollectionAssert.AreEqual(new[] { "sessions", "_", "_", "layouts", "|", "_", "kiosk" },
                IconBarLayout.Normalize(new[] { "_", "sessions", "_", "_", "layouts", "|", "_", "kiosk", "_", "|" }, Known));
            CollectionAssert.AreEqual(IconBarLayout.Default.Split(','), IconBarLayout.Parse("_,|,_", Known), "only gaps: default");
            ClassicAssert.IsTrue(IconBarLayout.IsGap("_"));
            ClassicAssert.IsTrue(IconBarLayout.IsGap("|"));
            ClassicAssert.IsFalse(IconBarLayout.IsGap("sessions"));
        }

        [Test]
        public void RecentSessionsNewestFirstWithoutDuplicates()
        {
            List<string> list = new List<string>();
            foreach (string id in new[] { "a", "b", "c", "b" })
            {
                list = RecentSessions.Add(list, id);
            }
            CollectionAssert.AreEqual(new[] { "b", "c", "a" }, list);
        }

        [Test]
        public void RecentSessionsKeepTen()
        {
            List<string> list = new List<string>();
            for (int i = 0; i < 15; i++)
            {
                list = RecentSessions.Add(list, "s" + i);
            }
            ClassicAssert.AreEqual(RecentSessions.Max, list.Count);
            ClassicAssert.AreEqual("s14", list[0]);
            ClassicAssert.AreEqual("s5", list[9]);
        }

        [Test]
        public void RecentSessionsRoundTripFollowRenamesAndSkipDeletedSessions()
        {
            string stored = RecentSessions.Format(new[] { "Prod/web01", "Dev/db01", "Prod/web02" });
            CollectionAssert.AreEqual(new[] { "Prod/web01", "Dev/db01", "Prod/web02" }, RecentSessions.Parse(stored + "\n\n"));

            CollectionAssert.AreEqual(new[] { "Prod/web-01", "Dev/db01", "Prod/web02" },
                RecentSessions.Rename(RecentSessions.Parse(stored), "Prod/web01", "Prod/web-01"));
            CollectionAssert.AreEqual(new[] { "Prod/web02", "Dev/db01" },
                RecentSessions.Rename(RecentSessions.Parse(stored), "Prod/web01", "Prod/web02"), "renamed onto an existing entry: once");

            Dictionary<string, SessionData> sessions = new Dictionary<string, SessionData>
            {
                { "Prod/web01", new SessionData { SessionId = "Prod/web01", SessionName = "web01" } },
                { "Prod/web02", new SessionData { SessionId = "Prod/web02", SessionName = "web02" } },
            };
            CollectionAssert.AreEqual(new[] { "web01", "web02" },
                RecentSessions.Resolve(RecentSessions.Parse(stored), id => sessions.TryGetValue(id, out SessionData s) ? s : null).Select(s => s.SessionName),
                "Dev/db01 was deleted");
        }
    }
}
