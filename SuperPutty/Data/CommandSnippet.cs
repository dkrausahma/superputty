using System;
using System.Xml.Serialization;

namespace SuperPutty.Data
{
    /// <summary>Where a <see cref="CommandSnippet"/> applies</summary>
    public enum CommandScope
    {
        /// <summary>All sessions</summary>
        Global,
        /// <summary>All sessions in a folder of the session tree (<see cref="CommandSnippet.ScopeKey"/> = folder path)</summary>
        Folder,
        /// <summary>One session (<see cref="CommandSnippet.ScopeKey"/> = SessionId)</summary>
        Session
    }

    /// <summary>A reusable command shown in the Commands pane</summary>
    public class CommandSnippet
    {
        /// <summary>The text sent to the terminal</summary>
        [XmlAttribute]
        public string Command { get; set; }

        /// <summary>Optional short name shown instead of the command</summary>
        [XmlAttribute]
        public string Name { get; set; }

        /// <summary>Optional description, shown as tooltip</summary>
        [XmlAttribute]
        public string Description { get; set; }

        /// <summary>Category (folder in the Commands pane)</summary>
        [XmlAttribute]
        public string Category { get; set; }

        /// <summary>Where the command applies; only <see cref="CommandScope.Global"/> is used so far</summary>
        [XmlAttribute]
        public CommandScope Scope { get; set; }

        /// <summary>Folder path or SessionId for <see cref="CommandScope.Folder"/> / <see cref="CommandScope.Session"/></summary>
        [XmlAttribute]
        public string ScopeKey { get; set; }

        /// <summary>The text shown in the Commands pane: the name if set, otherwise the command</summary>
        [XmlIgnore]
        public string DisplayText
        {
            get { return string.IsNullOrEmpty(this.Name) ? this.Command : this.Name; }
        }

        /// <summary>True if the filter text occurs (case-insensitive) in the command, name, description or category</summary>
        public bool Matches(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return true;
            }
            filter = filter.Trim();
            return Contains(this.Command, filter) || Contains(this.Name, filter)
                || Contains(this.Description, filter) || Contains(this.Category, filter);
        }

        private static bool Contains(string text, string filter)
        {
            return text != null && text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public CommandSnippet Clone()
        {
            return (CommandSnippet)this.MemberwiseClone();
        }

        public override string ToString()
        {
            return this.Command;
        }
    }
}
