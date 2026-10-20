using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using log4net;

namespace SuperPutty.Data
{
    /// <summary>
    /// Command bar history, kept separately for each session and login. Newest entries come first; a command
    /// sent again moves back to the top instead of appearing twice.
    /// </summary>
    public class CommandHistory
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CommandHistory));

        /// <summary>Entries kept per session; older ones are dropped</summary>
        public const int MaxEntriesPerSession = 50;

        /// <summary>All entries of all sessions, newest first</summary>
        private readonly List<HistoryEntry> entries = new List<HistoryEntry>();

        /// <summary>The history key of a session: "login@SessionId", or just the SessionId without a login</summary>
        public static string KeyFor(SessionData session)
        {
            if (session == null || string.IsNullOrEmpty(session.SessionId))
            {
                return null;
            }
            return string.IsNullOrEmpty(session.Username) ? session.SessionId : session.Username + "@" + session.SessionId;
        }

        /// <summary>Number of entries over all sessions</summary>
        public int Count { get { return this.entries.Count; } }

        /// <summary>Record a command sent to the session with the given key</summary>
        public void Add(string sessionKey, string command)
        {
            Add(sessionKey, command, DateTime.UtcNow);
        }

        /// <summary>Record a command sent to the session with the given key at the given UTC time</summary>
        public void Add(string sessionKey, string command, DateTime timeStampUtc)
        {
            if (string.IsNullOrEmpty(sessionKey) || string.IsNullOrEmpty(command))
            {
                return;
            }

            this.entries.RemoveAll(e => e.SessionKey == sessionKey && e.Command == command);
            this.entries.Insert(0, new HistoryEntry { SessionKey = sessionKey, Command = command, TimeStamp = timeStampUtc });

            // keep the newest entries of this session only
            int kept = 0;
            this.entries.RemoveAll(e => e.SessionKey == sessionKey && ++kept > MaxEntriesPerSession);
        }

        /// <summary>The commands of one session, newest first</summary>
        public List<string> GetCommands(string sessionKey)
        {
            if (string.IsNullOrEmpty(sessionKey))
            {
                return new List<string>();
            }
            return this.entries.Where(e => e.SessionKey == sessionKey).Select(e => e.Command).ToList();
        }

        /// <summary>Remove entries older than the given number of days; 0 or less keeps everything</summary>
        public void Purge(int days, DateTime nowUtc)
        {
            if (days <= 0)
            {
                return;
            }
            DateTime cutOff = nowUtc.Subtract(TimeSpan.FromDays(days));
            this.entries.RemoveAll(e => e.TimeStamp < cutOff);
        }

        /// <summary>Serialize all entries, in the same XML format (ArrayOfHistoryEntry) as earlier versions</summary>
        public string SerializeXML()
        {
            XmlSerializer xs = new XmlSerializer(typeof(List<HistoryEntry>));
            using (StringWriter text = new StringWriter())
            {
                xs.Serialize(text, this.entries);
                return text.ToString();
            }
        }

        /// <summary>
        /// Load a history saved with <see cref="SerializeXML"/>. Entries from earlier versions have no session
        /// and are dropped; invalid XML yields an empty history.
        /// </summary>
        public static CommandHistory DeserializeXML(string xml)
        {
            CommandHistory history = new CommandHistory();
            if (string.IsNullOrEmpty(xml))
            {
                return history;
            }

            try
            {
                XmlSerializer xs = new XmlSerializer(typeof(List<HistoryEntry>));
                using (StringReader reader = new StringReader(xml))
                {
                    List<HistoryEntry> loaded = (List<HistoryEntry>)xs.Deserialize(reader);
                    history.entries.AddRange(loaded
                        .Where(e => !string.IsNullOrEmpty(e.SessionKey) && !string.IsNullOrEmpty(e.Command))
                        .OrderByDescending(e => e.TimeStamp));
                }
            }
            catch (InvalidOperationException ex)
            {
                Log.Warn("Could not load command bar history, starting with an empty one", ex);
            }
            return history;
        }
    }
}
