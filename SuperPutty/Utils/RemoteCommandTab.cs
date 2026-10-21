using System;
using System.IO;
using System.Text;
using log4net;
using SuperPutty.Data;

namespace SuperPutty.Utils
{
    /// <summary>
    /// Opens a new terminal tab on the same host as an existing session that runs a command instead of a login
    /// shell, e.g. a shell inside a docker container or a log follow. The session is copied and PuTTY gets
    /// "-t -m &lt;file&gt;" (remote command read from a file, with a terminal). These tabs are temporary: their
    /// copied session is not saved, so they are not restored with a layout.
    /// </summary>
    public static class RemoteCommandTab
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(RemoteCommandTab));

        /// <summary>Folder for the command files PuTTY reads at start-up</summary>
        public static string CommandFolder
        {
            get { return Path.Combine(Path.GetTempPath(), "SuperPuTTY", "remote-commands"); }
        }

        /// <summary>Remote commands need SSH; PuTTY ignores -m for the other protocols</summary>
        public static bool IsSupported(SessionData session)
        {
            return session != null && (session.Proto == ConnectionProtocol.SSH || session.Proto == ConnectionProtocol.SSH2);
        }

        /// <summary>
        /// Wrap a command so the tab keeps an interactive shell after the command ends, instead of closing
        /// (PuTTY closes the window when the remote command exits cleanly)
        /// </summary>
        public static string KeepShellAfter(string command)
        {
            return command.TrimEnd() + "; exec \"${SHELL:-sh}\" -l";
        }

        /// <summary>The session copy for a command tab: titled, without login script, with PuTTY's -t -m arguments</summary>
        public static SessionData CreateCommandSession(SessionData session, string commandFile, string title)
        {
            SessionData copy = (SessionData)session.Clone();
            copy.CommandTabOf = session.ScopeSessionId;
            copy.SessionId = session.SessionId + " > " + title;
            copy.SessionName = title;
            copy.SPSLFileName = null;
            copy.ExtraArgs = string.Format("{0} -t -m \"{1}\"", session.ExtraArgs, commandFile).Trim();
            return copy;
        }

        /// <summary>Open the tab; returns null (and reports why) if the session can't run remote commands</summary>
        public static ctlPuttyPanel Open(SessionData session, string command, string title)
        {
            if (!IsSupported(session))
            {
                SuperPuTTY.ReportStatus("New tabs with a command need an SSH session");
                return null;
            }

            string commandFile = WriteCommandFile(command);
            ctlPuttyPanel panel = SuperPuTTY.OpenPuttySession(CreateCommandSession(session, commandFile, title));
            if (panel == null)
            {
                TryDelete(commandFile);
                return null;
            }

            // PuTTY reads the file when it starts; remove it with the tab
            panel.FormClosed += delegate { TryDelete(commandFile); };
            Log.InfoFormat("Opened command tab: session={0}, title={1}, command=[{2}]", session.SessionId, title, command);
            return panel;
        }

        private static string WriteCommandFile(string command)
        {
            Directory.CreateDirectory(CommandFolder);
            string file = Path.Combine(CommandFolder, Guid.NewGuid().ToString("N") + ".sh");
            // Unix line endings and no BOM: the file is sent to the remote shell as-is
            File.WriteAllText(file, command.Replace("\r\n", "\n") + "\n", new UTF8Encoding(false));
            return file;
        }

        /// <summary>Remove command files left behind, e.g. after a crash; called at start-up</summary>
        public static void CleanUp()
        {
            try
            {
                if (!Directory.Exists(CommandFolder))
                {
                    return;
                }
                foreach (string file in Directory.GetFiles(CommandFolder, "*.sh"))
                {
                    if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddHours(-1))
                    {
                        TryDelete(file);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.Warn("Could not clean up remote command files", ex);
            }
        }

        private static void TryDelete(string file)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.WarnFormat("Could not delete {0}: {1}", file, ex.Message);
            }
        }
    }
}
