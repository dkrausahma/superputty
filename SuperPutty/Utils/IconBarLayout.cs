using System;
using System.Collections.Generic;
using System.Linq;

namespace SuperPutty.Utils
{
    /// <summary>
    /// Which icons the icon bar shows, in which order: a comma-separated list of item ids with "|" for a
    /// separator line and "_" for an empty space, stored in the setting IconBarLayout (empty = <see cref="Default"/>).
    /// </summary>
    public static class IconBarLayout
    {
        public const string Separator = "|";

        /// <summary>An empty gap; several in a row make a wider one</summary>
        public const string Space = "_";

        /// <summary>A separator or a space: allowed only between icons</summary>
        public static bool IsGap(string entry)
        {
            return entry == Separator || entry == Space;
        }

        public const string Default = "sessions,recent,layouts,commands,containers,hostinfo,|,copysession,upload,transfer,|,kiosk";

        /// <summary>The entries of a stored layout; empty or unreadable means the default</summary>
        public static List<string> Parse(string text, ICollection<string> knownIds)
        {
            List<string> entries = Normalize((text ?? string.Empty).Split(','), knownIds);
            return entries.Any(e => !IsGap(e)) ? entries : Normalize(Default.Split(','), knownIds);
        }

        /// <summary>
        /// Known ids once each, in order; separators and spaces only between icons (none at the ends), never two
        /// separators in a row (spaces may repeat for a wider gap)
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
                else if (entry == Space)
                {
                    if (result.Count > 0)
                    {
                        result.Add(Space);
                    }
                }
                else if (knownIds.Contains(entry) && seen.Add(entry))
                {
                    result.Add(entry);
                }
            }
            while (result.Count > 0 && IsGap(result[result.Count - 1]))
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
