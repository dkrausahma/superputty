using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using log4net;
using SuperPutty.Data;

namespace SuperPutty.Utils
{
    /// <summary>
    /// PuTTY session logs: with session logging on (View → Log Sessions to Files), every PuTTY tab writes all
    /// session output to its own file in &lt;settings folder&gt;\logs. The raw log contains terminal control codes
    /// (colours, window titles); <see cref="WriteCleanCopy"/> produces a readable "&lt;log&gt;.txt" next to it, written
    /// automatically when the tab closes and on demand (Tools → Open Session Log).
    /// </summary>
    public static class SessionLog
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SessionLog));

        public static string Folder
        {
            get { return Path.Combine(SuperPuTTY.Settings.SettingsFolder, "logs"); }
        }

        /// <summary>A new log file for a session's tab, or null when logging is off or the protocol isn't PuTTY's</summary>
        public static string NewLogFile(SessionData session)
        {
            if (!SuperPuTTY.Settings.SessionLogging || session == null
                || session.Proto == ConnectionProtocol.Mintty || session.Proto == ConnectionProtocol.VNC || session.Proto == ConnectionProtocol.Cygterm)
            {
                return null;
            }
            try
            {
                Directory.CreateDirectory(Folder);
                return Path.Combine(Folder, FileName(session.SessionName, DateTime.Now));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.Warn("Could not create the session log folder " + Folder, ex);
                return null;
            }
        }

        /// <summary>"name-yyyyMMdd-HHmmss.log" with characters not allowed in file names replaced</summary>
        public static string FileName(string sessionName, DateTime time)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string name = new string((sessionName ?? "session").Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
            return string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyyMMdd-HHmmss}.log", name.Length == 0 ? "session" : name, time);
        }

        /// <summary>
        /// Write the log without control codes to "&lt;log&gt;.txt" and return its path. The log may still be open
        /// in PuTTY, so it is read with shared access.
        /// </summary>
        public static string WriteCleanCopy(string logFile)
        {
            string raw;
            using (FileStream stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                raw = reader.ReadToEnd();
            }
            string cleanFile = Path.ChangeExtension(logFile, ".txt");
            // with BOM, so Windows editors don't show UTF-8 characters such as lsblk's "├─" as "â”œâ”€"
            File.WriteAllText(cleanFile, TerminalText.Clean(raw), new UTF8Encoding(true));
            return cleanFile;
        }

        /// <summary>Logs larger than this are cleaned in the background when their tab closes</summary>
        private const long ForegroundLimit = 20L * 1024 * 1024;

        /// <summary>Write the clean copy of a closed tab's log; failures are only logged</summary>
        public static void WriteCleanCopyAfterClose(string logFile)
        {
            if (string.IsNullOrEmpty(logFile) || !File.Exists(logFile))
            {
                return;
            }
            System.Threading.WaitCallback write = delegate
            {
                try
                {
                    WriteCleanCopy(logFile);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    Log.Warn("Could not write the clean copy of " + logFile, ex);
                }
            };
            if (new FileInfo(logFile).Length > ForegroundLimit)
            {
                System.Threading.ThreadPool.QueueUserWorkItem(write);
            }
            else
            {
                write(null);
            }
        }
    }
}
