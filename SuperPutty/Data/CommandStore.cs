using System;
using System.IO;
using System.Linq;
using log4net;

namespace SuperPutty.Data
{
    /// <summary>
    /// The application's command library (Commands.XML in the settings folder), loaded on first use and shared by
    /// the Commands pane and the session rename handling, so renames are followed even while the pane is closed
    /// </summary>
    public static class CommandStore
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CommandStore));

        private static CommandLibrary library;

        /// <summary>Raised after the library changed and was saved</summary>
        public static event EventHandler Changed;

        public static string LibraryPath
        {
            get { return Path.Combine(SuperPuTTY.Settings.SettingsFolder, CommandLibrary.FileName); }
        }

        public static CommandLibrary Library
        {
            get
            {
                if (library == null)
                {
                    library = CommandLibrary.Load(LibraryPath);
                    if (library.Upgrade() || !File.Exists(LibraryPath))
                    {
                        Save();
                    }
                }
                return library;
            }
        }

        /// <summary>Save the library and tell the listeners</summary>
        public static void Save()
        {
            if (library == null)
            {
                return;
            }
            library.Save(LibraryPath);
            if (Changed != null)
            {
                Changed(null, EventArgs.Empty);
            }
        }

        /// <summary>Called when a session's ID changes (rename, moved in the tree)</summary>
        public static void SessionIdChanged(string oldId, string newId)
        {
            try
            {
                if (Library.SessionIdChanged(oldId, newId, SuperPuTTY.GetAllSessions().Select(s => s.SessionId).ToList()))
                {
                    Log.InfoFormat("Commands follow session rename {0} -> {1}", oldId, newId);
                    Save();
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.Error("Could not update commands after session rename", ex);
            }
        }
    }
}
