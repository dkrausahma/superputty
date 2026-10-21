using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using log4net;

namespace SuperPutty.Utils
{
    /// <summary>
    /// Backup and restore of everything SuperPuTTY keeps: SuperPuTTY.settings plus the XML files of the settings
    /// folder (sessions, commands, auto-restore layout) and the layouts folder, in one zip. A restore can't
    /// replace files the running application still writes on exit, so it is staged in <see cref="PendingFolder"/>
    /// and applied on the next start, before the settings are loaded.
    /// </summary>
    public static class SettingsBackup
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SettingsBackup));

        /// <summary>Zip entry of SuperPuTTY.settings</summary>
        public const string SettingsEntry = "SuperPuTTY.settings";

        /// <summary>Zip folder holding the contents of the settings folder</summary>
        public const string DataFolder = "data/";

        public const string InfoEntry = "backup-info.txt";

        /// <summary>Records the settings folder a staged restore goes into</summary>
        private const string TargetFile = "target.txt";

        /// <summary>Dated copies SaveSessionsToFile keeps (Sessions.20261021_062444.XML) are not backed up</summary>
        private static readonly Regex DatedCopy = new Regex(@"\.\d{8}_\d{6}\.XML$", RegexOptions.IgnoreCase);

        /// <summary>Where a restore waits for the next start</summary>
        public static string PendingFolder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SuperPuTTY", "restore-pending"); }
        }

        /// <summary>The files of the settings folder that go into a backup, relative to it</summary>
        public static List<string> DataFiles(string settingsFolder)
        {
            List<string> files = new List<string>();
            if (string.IsNullOrEmpty(settingsFolder) || !Directory.Exists(settingsFolder))
            {
                return files;
            }
            files.AddRange(Directory.GetFiles(settingsFolder, "*.xml")
                .Where(f => !DatedCopy.IsMatch(f))
                .Select(Path.GetFileName));
            string layouts = Path.Combine(settingsFolder, "layouts");
            if (Directory.Exists(layouts))
            {
                files.AddRange(Directory.GetFiles(layouts, "*", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(settingsFolder, f)));
            }
            return files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Writes the backup zip; returns the number of files in it (without the info file)</summary>
        public static int Create(string zipFile, string settingsFile, string settingsFolder, string version)
        {
            string folder = Path.GetDirectoryName(Path.GetFullPath(zipFile));
            Directory.CreateDirectory(folder);
            string temp = zipFile + ".tmp";
            int count = 0;
            using (ZipArchive zip = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                if (!string.IsNullOrEmpty(settingsFile) && File.Exists(settingsFile))
                {
                    zip.CreateEntryFromFile(settingsFile, SettingsEntry);
                    count++;
                }
                foreach (string file in DataFiles(settingsFolder))
                {
                    zip.CreateEntryFromFile(Path.Combine(settingsFolder, file), DataFolder + file.Replace('\\', '/'));
                    count++;
                }
                ZipArchiveEntry info = zip.CreateEntry(InfoEntry);
                using (StreamWriter writer = new StreamWriter(info.Open(), new UTF8Encoding(true)))
                {
                    writer.WriteLine("SuperPuTTY {0} settings backup", version);
                    writer.WriteLine("Created: {0:yyyy-MM-dd HH:mm:ss}", DateTime.Now);
                    writer.WriteLine("Computer: {0}, user: {1}", Environment.MachineName, Environment.UserName);
                    writer.WriteLine("Settings folder: {0}", settingsFolder);
                    writer.WriteLine();
                    writer.WriteLine("Stored passwords are encrypted for this Windows user (DPAPI); after a restore under");
                    writer.WriteLine("another user or on another computer they have to be entered again.");
                }
            }
            File.Move(temp, zipFile, true);
            return count;
        }

        /// <summary>The first lines of the backup's info file, or null if the zip is not a SuperPuTTY backup</summary>
        public static string Describe(string zipFile)
        {
            using (ZipArchive zip = ZipFile.OpenRead(zipFile))
            {
                if (zip.GetEntry(SettingsEntry) == null && !zip.Entries.Any(e => e.FullName.StartsWith(DataFolder, StringComparison.Ordinal)))
                {
                    return null;
                }
                ZipArchiveEntry info = zip.GetEntry(InfoEntry);
                if (info == null)
                {
                    return string.Empty;
                }
                using (StreamReader reader = new StreamReader(info.Open()))
                {
                    return string.Join(Environment.NewLine, Enumerable.Range(0, 3).Select(i => reader.ReadLine()).Where(l => l != null));
                }
            }
        }

        /// <summary>
        /// Unpacks the backup into <paramref name="pendingFolder"/> for <see cref="ApplySettingsFile"/> and
        /// <see cref="ApplyData"/> on the next start; the data goes into <paramref name="targetSettingsFolder"/>
        /// </summary>
        public static void StageRestore(string zipFile, string pendingFolder, string targetSettingsFolder)
        {
            if (Describe(zipFile) == null)
            {
                throw new InvalidDataException("Not a SuperPuTTY settings backup: " + zipFile);
            }
            if (Directory.Exists(pendingFolder))
            {
                Directory.Delete(pendingFolder, true);
            }
            Directory.CreateDirectory(pendingFolder);
            string root = Path.GetFullPath(pendingFolder) + Path.DirectorySeparatorChar;
            using (ZipArchive zip = ZipFile.OpenRead(zipFile))
            {
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    if (entry.FullName.EndsWith("/") || entry.FullName == InfoEntry)
                    {
                        continue;
                    }
                    string target = Path.GetFullPath(Path.Combine(pendingFolder, entry.FullName));
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("Invalid entry in backup: " + entry.FullName);
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    entry.ExtractToFile(target, true);
                }
            }
            File.WriteAllText(Path.Combine(pendingFolder, TargetFile), targetSettingsFolder ?? string.Empty);
        }

        /// <summary>
        /// Start of a restore, before the settings are loaded: puts the staged SuperPuTTY.settings in place.
        /// Returns the settings folder the restore was meant for, or null if no restore is pending.
        /// </summary>
        public static string ApplySettingsFile(string pendingFolder, string settingsFile)
        {
            string targetFile = Path.Combine(pendingFolder, TargetFile);
            if (!File.Exists(targetFile))
            {
                return null;
            }
            string staged = Path.Combine(pendingFolder, SettingsEntry);
            if (File.Exists(staged))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(settingsFile)));
                File.Copy(staged, settingsFile, true);
                Log.InfoFormat("Restored {0}", settingsFile);
            }
            return File.ReadAllText(targetFile).Trim();
        }

        /// <summary>Second step of a restore, once the settings are loaded: copies the data files and removes the staging folder</summary>
        public static int ApplyData(string pendingFolder, string settingsFolder)
        {
            int count = 0;
            string data = Path.Combine(pendingFolder, DataFolder.TrimEnd('/'));
            if (Directory.Exists(data) && !string.IsNullOrEmpty(settingsFolder))
            {
                // the layouts are replaced, not merged
                string layouts = Path.Combine(settingsFolder, "layouts");
                if (Directory.Exists(Path.Combine(data, "layouts")) && Directory.Exists(layouts))
                {
                    foreach (string old in Directory.GetFiles(layouts, "*", SearchOption.AllDirectories))
                    {
                        File.Delete(old);
                    }
                }
                foreach (string file in Directory.GetFiles(data, "*", SearchOption.AllDirectories))
                {
                    string target = Path.Combine(settingsFolder, Path.GetRelativePath(data, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(file, target, true);
                    count++;
                }
                Log.InfoFormat("Restored {0} files into {1}", count, settingsFolder);
            }
            Directory.Delete(pendingFolder, true);
            return count;
        }
    }
}
