using System;
using System.Collections.Generic;
using System.Linq;

namespace SuperPutty.Utils
{
    /// <summary>
    /// Which icons the icon bar shows, in which order: a comma-separated list of item ids with "|" for a
    /// separator, stored in the setting IconBarLayout (empty = <see cref="Default"/>).
    /// </summary>
    public static class IconBarLayout
    {
        public const string Separator = "|";

        public const string Default = "sessions,recent,layouts,commands,containers,hostinfo,|,copysession,upload,transfer,|,kiosk";

        /// <summary>The entries of a stored layout; empty or unreadable means the default</summary>
        public static List<string> Parse(string text, ICollection<string> knownIds)
        {
            List<string> entries = Normalize((text ?? string.Empty).Split(','), knownIds);
            return entries.Any(e => e != Separator) ? entries : Normalize(Default.Split(','), knownIds);
        }

        /// <summary>
        /// Known ids once each, in order; separators only between icons (none at the ends, never two in a row)
        /// </summary>
        public static List<string> Normalize(IEnumerable<string> entries, ICollection<string> knownIds)
        {
            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string raw in entries)
            {
                string entry = (raw ?? string.Empty).Trim();
                if (entry == Separator)
                {
                    if (result.Count > 0 && result[result.Count - 1] != Separator)
                    {
                        result.Add(Separator);
                    }
                }
                else if (knownIds.Contains(entry) && seen.Add(entry))
                {
                    result.Add(entry);
                }
            }
            while (result.Count > 0 && result[result.Count - 1] == Separator)
            {
                result.RemoveAt(result.Count - 1);
            }
            return result;
        }

        public static string Format(IEnumerable<string> entries)
        {
            return string.Join(",", entries);
        }
    }
}
