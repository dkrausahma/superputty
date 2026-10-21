using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Xml;
using log4net;

namespace SuperPutty.Utils
{
    /// <summary>
    /// One-time move of the settings that used to live in .NET's user.config (one folder per version) into
    /// SuperPuTTY.settings, so all settings are in one file that survives updates and goes into a backup.
    /// Runs once per settings file; the marker is the setting UserConfigMigrated.
    /// </summary>
    public static class UserConfigMigration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(UserConfigMigration));

        /// <summary>The settings that were stored in user.config before 1.5</summary>
        public static readonly string[] SettingNames =
        {
            "PscpHomePrefix", "AutoUpdateCheck", "PscpRootHomePrefix", "Action_OpenScriptEditor_Shortcut",
            "SessiontreeShowFoldersFirst", "FileZillaExe", "WinSCPExe", "PersistCommandBarHistory",
            "AllowPlainTextPuttyPasswordArg", "PuttyDefaultParameters", "SaveCommandHistoryDays", "CommandBarHistory"
        };

        public const string SectionName = "SuperPutty.Properties.Settings";

        /// <summary>Copies the old values once, then sets the marker and saves</summary>
        internal static void Run(Properties.Settings settings)
        {
            try
            {
                string current = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
                string file = FindUserConfig(current);
                if (file != null)
                {
                    int count = Apply(settings, ReadValues(file));
                    Log.InfoFormat("Moved {0} settings from {1} to {2}", count, file, settings.SettingsFilePath);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Unable to move settings from user.config", ex);
            }
            settings.UserConfigMigrated = true;
            settings.Save();
        }

        /// <summary>The current user.config, otherwise the newest one of another version of the same exe, or null</summary>
        public static string FindUserConfig(string currentUserConfig)
        {
            if (string.IsNullOrEmpty(currentUserConfig))
            {
                return null;
            }
            if (File.Exists(currentUserConfig))
            {
                return currentUserConfig;
            }
            // <company>\<exe folder>\<version>\user.config
            DirectoryInfo exeFolder = new FileInfo(currentUserConfig).Directory?.Parent;
            if (exeFolder == null || !exeFolder.Exists)
            {
                return null;
            }
            return exeFolder.GetFiles("user.config", SearchOption.AllDirectories)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => f.FullName)
                .FirstOrDefault();
        }

        /// <summary>The values of the moved settings found in a user.config file (name → text)</summary>
        public static Dictionary<string, string> ReadValues(string userConfigFile)
        {
            Dictionary<string, string> values = new Dictionary<string, string>();
            XmlDocument doc = new XmlDocument();
            doc.Load(userConfigFile);
            XmlNodeList settings = doc.SelectNodes("/configuration/userSettings/" + SectionName + "/setting[@name]");
            foreach (XmlElement setting in settings)
            {
                string name = setting.GetAttribute("name");
                XmlNode value = setting.SelectSingleNode("value");
                if (value != null && SettingNames.Contains(name))
                {
                    values[name] = value.InnerText;
                }
            }
            return values;
        }

        /// <summary>Assigns the values (converted to each setting's type); returns how many were taken over</summary>
        public static int Apply(ApplicationSettingsBase settings, IDictionary<string, string> values)
        {
            int count = 0;
            foreach (KeyValuePair<string, string> pair in values)
            {
                SettingsProperty property = settings.Properties[pair.Key];
                if (property == null)
                {
                    continue;
                }
                try
                {
                    settings[pair.Key] = property.PropertyType == typeof(string)
                        ? pair.Value
                        : TypeDescriptor.GetConverter(property.PropertyType).ConvertFromInvariantString(pair.Value);
                    count++;
                }
                catch (Exception ex)
                {
                    Log.WarnFormat("Skipping setting {0}='{1}': {2}", pair.Key, pair.Value, ex.Message);
                }
            }
            return count;
        }
    }
}
