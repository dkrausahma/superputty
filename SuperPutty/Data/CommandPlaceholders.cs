using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace SuperPutty.Data
{
    /// <summary>A placeholder in a command: {{name}} or {{name:default}}</summary>
    public class CommandPlaceholder
    {
        public string Name { get; set; }
        /// <summary>Value used when nothing was entered before; may be empty</summary>
        public string Default { get; set; }
    }

    /// <summary>A remembered placeholder value of a session (stored in Commands.XML)</summary>
    public class PlaceholderValue
    {
        /// <summary>SessionId the value belongs to; empty when used without a session</summary>
        [XmlAttribute]
        public string Session { get; set; }

        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public string Value { get; set; }
    }

    /// <summary>
    /// Placeholders in commands: <c>{{service}}</c> or <c>{{compose dir:~/app}}</c>. The name starts with a letter
    /// (so docker templates like <c>{{.Names}}</c> are left alone) and may contain letters, digits, spaces, '_' and
    /// '-'; everything after the first ':' is the default value.
    /// </summary>
    public static class CommandPlaceholders
    {
        private static readonly Regex Pattern = new Regex(@"\{\{(?<name>[A-Za-z][A-Za-z0-9 _-]*?)\s*(?::(?<default>[^{}]*))?\}\}", RegexOptions.Compiled);

        /// <summary>The distinct placeholders of a command, in order of appearance (first default wins)</summary>
        public static List<CommandPlaceholder> Parse(string command)
        {
            List<CommandPlaceholder> placeholders = new List<CommandPlaceholder>();
            foreach (Match match in Pattern.Matches(command ?? string.Empty))
            {
                string name = match.Groups["name"].Value.Trim();
                if (!placeholders.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    placeholders.Add(new CommandPlaceholder { Name = name, Default = match.Groups["default"].Success ? match.Groups["default"].Value : string.Empty });
                }
            }
            return placeholders;
        }

        /// <summary>The command with its placeholders' defaults left out ("{{compose dir:~}}" -> "{{compose dir}}"), for display</summary>
        public static string WithoutDefaults(string command)
        {
            return Pattern.Replace(command ?? string.Empty, match => "{{" + match.Groups["name"].Value.Trim() + "}}");
        }

        /// <summary>The command split into plain text (false) and placeholders (true), in order, for highlighting</summary>
        public static List<KeyValuePair<string, bool>> Segments(string command)
        {
            List<KeyValuePair<string, bool>> segments = new List<KeyValuePair<string, bool>>();
            string text = command ?? string.Empty;
            int position = 0;
            foreach (Match match in Pattern.Matches(text))
            {
                if (match.Index > position)
                {
                    segments.Add(new KeyValuePair<string, bool>(text.Substring(position, match.Index - position), false));
                }
                segments.Add(new KeyValuePair<string, bool>(match.Value, true));
                position = match.Index + match.Length;
            }
            if (position < text.Length)
            {
                segments.Add(new KeyValuePair<string, bool>(text.Substring(position), false));
            }
            return segments;
        }

        public static bool HasPlaceholders(string command)
        {
            return Pattern.IsMatch(command ?? string.Empty);
        }

        /// <summary>Replace every placeholder with its value (names compared case-insensitively)</summary>
        public static string Fill(string command, IDictionary<string, string> values)
        {
            return Pattern.Replace(command ?? string.Empty, match =>
            {
                string name = match.Groups["name"].Value.Trim();
                string value = values.Where(v => v.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(v => v.Value).FirstOrDefault();
                return value ?? (match.Groups["default"].Success ? match.Groups["default"].Value : string.Empty);
            });
        }
    }
}
