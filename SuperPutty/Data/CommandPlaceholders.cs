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
