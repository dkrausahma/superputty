using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using log4net;

namespace SuperPutty.Utils
{
    /// <summary>
    /// Passes a password to PuTTY, PSCP or plink with "-pwfile &lt;file&gt;" (PuTTY 0.77+) instead of "-pw &lt;password&gt;",
    /// so it doesn't show up in the process list. The file lies in the user's temp folder (only readable by the
    /// user) and is deleted shortly after the program has read it; leftovers are removed at start-up.
    /// </summary>
    public static class PasswordFile
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PasswordFile));

        /// <summary>Time the started program gets to read the file before it is deleted</summary>
        private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

        public static string Folder
        {
            get { return Path.Combine(Path.GetTempPath(), "SuperPuTTY", "pw"); }
        }

        /// <summary>True if the PuTTY tools understand -pwfile (0.77 and later); unknown versions count as new</summary>
        public static bool IsSupported(string puttyExe)
        {
            try
            {
                if (string.IsNullOrEmpty(puttyExe) || !File.Exists(puttyExe))
                {
                    return true;
                }
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(puttyExe);
                // PuTTY's file version is "0.84.0.0" (KiTTY uses its own numbers and is based on recent PuTTY)
                if (info.FileMajorPart == 0 && info.FileMinorPart > 0)
                {
                    return info.FileMinorPart >= 77;
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return true;
            }
        }

        /// <summary>The command-line option for a password: "-pwfile &lt;file&gt;" (quoted), or the old "-pw" for PuTTY before 0.77</summary>
        public static string Argument(string password)
        {
            if (!IsSupported(SuperPuTTY.Settings.PuttyExe))
            {
                Log.Warn("PuTTY is older than 0.77 and has no -pwfile; the password is passed with -pw (visible in the process list)");
                return "-pw " + password;
            }
            return "-pwfile \"" + Create(password) + "\"";
        }

        /// <summary>Write the password to a new file that is deleted after <see cref="Lifetime"/></summary>
        public static string Create(string password)
        {
            Directory.CreateDirectory(Folder);
            string file = Path.Combine(Folder, Guid.NewGuid().ToString("N"));
            File.WriteAllText(file, password, new UTF8Encoding(false));
            Timer timer = null;
            timer = new Timer(delegate
            {
                TryDelete(file);
                timer.Dispose();
            }, null, Lifetime, Timeout.InfiniteTimeSpan);
            return file;
        }

        /// <summary>Remove password files left behind (e.g. after a crash); called at start-up</summary>
        public static void CleanUp()
        {
            try
            {
                if (Directory.Exists(Folder))
                {
                    foreach (string file in Directory.GetFiles(Folder))
                    {
                        // a second instance may have just created one that PuTTY hasn't read yet
                        if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow - Lifetime - Lifetime)
                        {
                            TryDelete(file);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.Warn("Could not clean up password files", ex);
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
                Log.WarnFormat("Could not delete password file {0}: {1}", file, ex.Message);
            }
        }
    }
}
