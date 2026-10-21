using System;
using System.Configuration;
using System.IO;
using System.Linq;
using log4net;

namespace SuperPutty.Utils
{
    /// <summary>
    /// Settings that don't use <see cref="PortableSettingsProvider"/> are stored by .NET in
    /// %LOCALAPPDATA%\&lt;company&gt;\&lt;exe&gt;_Url_&lt;hash&gt;\&lt;version&gt;\user.config. The folder name differs between
    /// .NET Framework (SuperPutty.exe_Url_...) and .NET (SuperPutty_Url_...), so after the migration those
    /// settings would silently fall back to their defaults. This copies the most recent .NET Framework
    /// user.config into the new location once, if the new location has none yet.
    /// </summary>
    public static class LegacyUserConfig
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LegacyUserConfig));

        public static void ImportIfMissing()
        {
            try
            {
                string current = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
                string legacy = FindLegacyUserConfig(current, Path.GetFileName(System.Windows.Forms.Application.ExecutablePath));
                if (legacy == null)
                {
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(current));
                File.Copy(legacy, current);
                Log.InfoFormat("Imported settings from .NET Framework user.config: {0} -> {1}", legacy, current);
            }
            catch (Exception ex)
            {
                Log.Warn("Unable to import .NET Framework user.config", ex);
            }
        }

        /// <summary>Returns the newest .NET Framework user.config next to <paramref name="currentUserConfig"/>, or null</summary>
        /// <param name="currentUserConfig">user.config path used by the running (.NET) application</param>
        /// <param name="exeFileName">file name of the executable, e.g. SuperPutty.exe</param>
        public static string FindLegacyUserConfig(string currentUserConfig, string exeFileName)
        {
            if (string.IsNullOrEmpty(currentUserConfig) || File.Exists(currentUserConfig))
            {
                return null;
            }

            // <company>\<exe folder>\<version>\user.config
            DirectoryInfo exeFolder = new FileInfo(currentUserConfig).Directory?.Parent;
            DirectoryInfo companyFolder = exeFolder?.Parent;
            if (companyFolder == null || !companyFolder.Exists)
            {
                return null;
            }

            return companyFolder.GetDirectories(exeFileName + "_*")
                .Where(d => !string.Equals(d.FullName, exeFolder.FullName, StringComparison.OrdinalIgnoreCase))
                .SelectMany(d => d.GetFiles("user.config", SearchOption.AllDirectories))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => f.FullName)
                .FirstOrDefault();
        }
    }
}
