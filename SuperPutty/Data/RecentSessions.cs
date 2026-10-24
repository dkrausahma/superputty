using System;
using System.Collections.Generic;
using System.Linq;

namespace SuperPutty.Data
{
    /// <summary>
    /// The last opened sessions (SessionIds, newest first, no duplicates), stored one per line in the setting
    /// RecentSessions. Ids of deleted sessions stay in the setting but are left out when shown.
    /// </summary>
    public static class RecentSessions
    {
        public const int Max = 10;

        public static List<string> Parse(string text)
        {
            return (text ?? string.Empty).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        }

        public static string Format(IEnumerable<string> ids)
        {
            return string.Join("\n", ids);
        }

        /// <summary>The list with <paramref name="sessionId"/> in front, once, cut to <paramref name="max"/> entries</summary>
        public static List<string> Add(IEnumerable<string> ids, string sessionId, int max = Max)
        {
            List<string> result = new List<string> { sessionId };
            result.AddRange(ids.Where(id => !string.Equals(id, sessionId, StringComparison.Ordinal)));
            return result.Take(max).ToList();
        }

        /// <summary>The list with a renamed session's id replaced (and not twice)</summary>
        public static List<string> Rename(IEnumerable<string> ids, string oldId, string newId)
        {
            return ids.Select(id => string.Equals(id, oldId, StringComparison.Ordinal) ? newId : id).Distinct(StringComparer.Ordinal).ToList();
        }

        /// <summary>The sessions that still exist, in list order</summary>
        public static List<SessionData> Resolve(IEnumerable<string> ids, Func<string, SessionData> lookup)
        {
            return ids.Select(lookup).Where(s => s != null).ToList();
        }
    }
}
