using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using log4net;

namespace SuperPutty.Data
{
    /// <summary>
    /// The commands of the Commands pane, stored as Commands.XML in the settings folder. A missing file is created
    /// with a starter set of standard Linux commands.
    /// </summary>
    [XmlRoot("Commands")]
    public class CommandLibrary
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CommandLibrary));

        public const string FileName = "Commands.XML";

        /// <summary>Category used for commands without one</summary>
        public const string DefaultCategory = "General";

        /// <summary>Category always shown first, even when empty</summary>
        public const string FavoritesCategory = "Favorites";

        /// <summary>File format version; 1 added the Favorites category</summary>
        public const int CurrentVersion = 1;

        [XmlAttribute]
        public int Version { get; set; }

        [XmlElement("Command")]
        public List<CommandSnippet> Commands { get; set; } = new List<CommandSnippet>();

        /// <summary>The distinct categories: Favorites first (always), then in order of first appearance</summary>
        public List<string> Categories()
        {
            List<string> categories = new List<string> { FavoritesCategory };
            categories.AddRange(this.Commands.Select(c => CategoryOf(c))
                .Where(c => !IsFavorites(c))
                .Distinct(StringComparer.OrdinalIgnoreCase));
            return categories;
        }

        public static bool IsFavorites(string category)
        {
            return FavoritesCategory.Equals(category, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Copy a command into the Favorites of its scope; false if an equal command is already there</summary>
        public bool AddToFavorites(CommandSnippet command, out CommandSnippet favorite)
        {
            favorite = this.Commands.FirstOrDefault(c => IsFavorites(CategoryOf(c)) && c.Command == command.Command
                && c.Scope == command.Scope && c.ScopeKey == command.ScopeKey);
            if (favorite != null)
            {
                return false;
            }
            favorite = command.Clone();
            favorite.Category = FavoritesCategory;
            this.Commands.Insert(0, favorite);
            return true;
        }

        #region Scopes

        /// <summary>The folders a session lies in, nearest first: "Prod/DB/web01" → "Prod/DB", "Prod"</summary>
        public static List<string> FolderChain(string sessionId)
        {
            List<string> folders = new List<string>();
            string folder = SessionData.GetSessionParentId(sessionId);
            while (!string.IsNullOrEmpty(folder))
            {
                folders.Add(folder);
                folder = SessionData.GetSessionParentId(folder);
            }
            return folders;
        }

        /// <summary>True if the command applies to the session (global, one of its folders, or the session itself)</summary>
        public static bool AppliesTo(CommandSnippet command, string sessionId)
        {
            switch (command.Scope)
            {
                case CommandScope.Session: return sessionId != null && command.ScopeKey == sessionId;
                case CommandScope.Folder: return sessionId != null && FolderChain(sessionId).Contains(command.ScopeKey);
                default: return true;
            }
        }

        /// <summary>
        /// True if a session- or folder-scoped command belongs to a session or folder that no longer exists, given
        /// the IDs of all sessions
        /// </summary>
        public static bool IsOrphan(CommandSnippet command, ICollection<string> sessionIds)
        {
            switch (command.Scope)
            {
                case CommandScope.Session: return !sessionIds.Contains(command.ScopeKey);
                case CommandScope.Folder: return !sessionIds.Any(id => FolderChain(id).Contains(command.ScopeKey));
                default: return false;
            }
        }

        /// <summary>Move commands of one scope target to another (session or folder renamed); true if any changed</summary>
        public bool RenameScope(CommandScope scope, string oldKey, string newKey)
        {
            bool changed = false;
            foreach (CommandSnippet command in this.Commands.Where(c => c.Scope == scope && c.ScopeKey == oldKey))
            {
                command.ScopeKey = newKey;
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// Follow a session's ID change: session commands move with it. Folder commands move only for a folder
        /// rename at the same depth, once no session is left in the old folder (a session merely moving to another
        /// folder leaves the folder commands where they are). True if anything changed.
        /// </summary>
        public bool SessionIdChanged(string oldId, string newId, ICollection<string> allSessionIds)
        {
            if (string.IsNullOrEmpty(oldId) || string.IsNullOrEmpty(newId) || oldId == newId)
            {
                return false;
            }
            bool changed = RenameScope(CommandScope.Session, oldId, newId);

            List<string> oldFolders = FolderChain(oldId);
            List<string> newFolders = FolderChain(newId);
            if (oldFolders.Count == newFolders.Count)
            {
                for (int i = 0; i < oldFolders.Count; i++)
                {
                    string oldFolder = oldFolders[i];
                    if (oldFolder != newFolders[i] && !allSessionIds.Any(id => FolderChain(id).Contains(oldFolder)))
                    {
                        changed |= RenameScope(CommandScope.Folder, oldFolder, newFolders[i]);
                    }
                }
            }
            return changed;
        }

        #endregion

        /// <summary>Bring a library loaded from an older file up to <see cref="CurrentVersion"/>; true if it changed</summary>
        public bool Upgrade()
        {
            if (this.Version >= CurrentVersion)
            {
                return false;
            }
            if (this.Version < 1)
            {
                // Favorites with "clear" (added once; a user who deletes it keeps it deleted)
                this.Commands.Insert(0, ClearCommand());
            }
            this.Version = CurrentVersion;
            return true;
        }

        private static CommandSnippet ClearCommand()
        {
            return new CommandSnippet { Category = FavoritesCategory, Command = "clear", Description = "Clear the screen" };
        }

        /// <summary>The category a command is shown under</summary>
        public static string CategoryOf(CommandSnippet command)
        {
            return string.IsNullOrWhiteSpace(command.Category) ? DefaultCategory : command.Category.Trim();
        }

        /// <summary>Load the library from a file; a missing file yields the starter set, an unreadable one an empty library</summary>
        public static CommandLibrary Load(string path)
        {
            if (!File.Exists(path))
            {
                Log.InfoFormat("No commands file at {0}, using the standard commands", path);
                return CreateDefault();
            }

            try
            {
                XmlSerializer xs = new XmlSerializer(typeof(CommandLibrary));
                using (StreamReader reader = new StreamReader(path))
                {
                    CommandLibrary library = (CommandLibrary)xs.Deserialize(reader);
                    library.Commands.RemoveAll(c => string.IsNullOrEmpty(c.Command));
                    return library;
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is IOException)
            {
                Log.Error("Could not load commands from " + path, ex);
                return new CommandLibrary();
            }
        }

        public void Save(string path)
        {
            XmlSerializer xs = new XmlSerializer(typeof(CommandLibrary));
            using (StreamWriter writer = new StreamWriter(path))
            {
                xs.Serialize(writer, this);
            }
        }

        /// <summary>A starter set of standard Linux commands; trailing spaces leave room for arguments</summary>
        public static CommandLibrary CreateDefault()
        {
            CommandLibrary library = new CommandLibrary { Version = CurrentVersion };
            void Add(string category, string command, string description)
            {
                library.Commands.Add(new CommandSnippet { Category = category, Command = command, Description = description });
            }

            library.Commands.Add(ClearCommand());

            const string files = "Files & Navigation";
            Add(files, "ls -lah", "List files with details, including hidden ones");
            Add(files, "ls -laht", "List files, newest first");
            Add(files, "pwd", "Show the current directory");
            Add(files, "cd ..", "Go up one directory");
            Add(files, "cd ~", "Go to the home directory");
            Add(files, "du -sh * | sort -h", "Size of each entry in the current directory, sorted");
            Add(files, "df -h", "Free disk space per file system");
            Add(files, "find . -name ", "Find files by name below the current directory");
            Add(files, "grep -rn ", "Search text recursively, with line numbers");
            Add(files, "tail -f ", "Follow a growing file, e.g. a log");
            Add(files, "less ", "Page through a file");

            const string system = "System";
            Add(system, "uptime", "Uptime and load average");
            Add(system, "free -h", "Memory usage");
            Add(system, "uname -a", "Kernel and architecture");
            Add(system, "cat /etc/os-release", "Distribution and version");
            Add(system, "whoami", "Current user");
            Add(system, "sudo -i", "Root shell");
            Add(system, "systemctl status ", "Status of a service");
            Add(system, "systemctl --failed", "Failed services");
            Add(system, "journalctl -xe", "Recent system log with explanations");
            Add(system, "journalctl -f -u ", "Follow the log of a service");
            Add(system, "dmesg -T | tail -50", "Recent kernel messages");
            Add(system, "history | tail -50", "Last 50 shell commands");

            const string processes = "Processes";
            Add(processes, "top", "Live process list");
            Add(processes, "htop", "Live process list (if installed)");
            Add(processes, "ps aux --sort=-%mem | head -15", "Processes using the most memory");
            Add(processes, "ps aux --sort=-%cpu | head -15", "Processes using the most CPU");
            Add(processes, "ps aux | grep ", "Find a process");
            Add(processes, "kill ", "Stop a process by PID");

            const string network = "Network";
            Add(network, "ip -br a", "IP addresses per interface");
            Add(network, "ip r", "Routing table");
            Add(network, "ss -tulpn", "Listening ports and their processes");
            Add(network, "ping -c 4 ", "Check if a host answers");
            Add(network, "curl -I ", "Show the HTTP headers of a URL");
            Add(network, "dig +short ", "Resolve a host name");

            const string packages = "Packages";
            Add(packages, "sudo apt update", "Refresh package lists (Debian/Ubuntu)");
            Add(packages, "apt list --upgradable", "Packages with updates (Debian/Ubuntu)");
            Add(packages, "sudo apt upgrade", "Install updates (Debian/Ubuntu)");
            Add(packages, "sudo dnf check-update", "Packages with updates (Fedora/RHEL)");

            const string archives = "Archives";
            Add(archives, "tar -czvf archive.tar.gz ", "Create a .tar.gz archive");
            Add(archives, "tar -xzvf ", "Extract a .tar.gz archive");
            Add(archives, "unzip ", "Extract a .zip archive");

            const string docker = "Docker";
            Add(docker, "docker ps", "Running containers");
            Add(docker, "docker ps -a", "All containers");
            Add(docker, "docker logs -f --tail 100 ", "Follow a container's log");
            Add(docker, "docker compose up -d", "Start the compose project in the background");
            Add(docker, "docker compose logs -f", "Follow the compose project's logs");

            return library;
        }
    }
}
