using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Utils;

namespace SuperPuttyUnitTests
{
    /// <summary>Settings backup zip, staged restore, and the one-time move of the user.config settings</summary>
    [TestFixture]
    public class SettingsBackupTests
    {
        private string root;
        private string settingsFolder;
        private string settingsFile;

        [SetUp]
        public void SetUp()
        {
            this.root = Path.Combine(Path.GetTempPath(), "SuperPuTTY.BackupTests." + Guid.NewGuid().ToString("N"));
            this.settingsFolder = Path.Combine(this.root, "settings");
            this.settingsFile = Path.Combine(this.root, "profile", "SuperPuTTY.settings");
            Directory.CreateDirectory(Path.Combine(this.settingsFolder, "layouts"));
            Directory.CreateDirectory(Path.Combine(this.settingsFolder, "logs"));
            Directory.CreateDirectory(Path.GetDirectoryName(this.settingsFile));
            File.WriteAllText(this.settingsFile, "<Settings><PuttyExe>old</PuttyExe></Settings>");
            File.WriteAllText(Path.Combine(this.settingsFolder, "Sessions.XML"), "sessions");
            File.WriteAllText(Path.Combine(this.settingsFolder, "Sessions.20261021_062444.XML"), "dated copy");
            File.WriteAllText(Path.Combine(this.settingsFolder, "Commands.XML"), "commands");
            File.WriteAllText(Path.Combine(this.settingsFolder, "layouts", "work.xml"), "layout");
            File.WriteAllText(Path.Combine(this.settingsFolder, "logs", "db01.log"), "log");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(this.root))
            {
                Directory.Delete(this.root, true);
            }
        }

        private string Zip()
        {
            string zip = Path.Combine(this.root, "backup.zip");
            SettingsBackup.Create(zip, this.settingsFile, this.settingsFolder, "1.5.0.0");
            return zip;
        }

        [Test]
        public void BackupHoldsSettingsSessionsCommandsAndLayouts()
        {
            string zip = Path.Combine(this.root, "backup.zip");
            int count = SettingsBackup.Create(zip, this.settingsFile, this.settingsFolder, "1.5.0.0");

            List<string> entries;
            using (ZipArchive archive = ZipFile.OpenRead(zip))
            {
                entries = archive.Entries.Select(e => e.FullName).ToList();
            }
            ClassicAssert.AreEqual(4, count);
            CollectionAssert.AreEquivalent(new[] { "SuperPuTTY.settings", "data/Commands.XML", "data/Sessions.XML", "data/layouts/work.xml", "backup-info.txt" }, entries,
                "no logs, no dated session copies");
            StringAssert.StartsWith("SuperPuTTY 1.5.0.0 settings backup", SettingsBackup.Describe(zip));
        }

        [Test]
        public void OtherZipIsNoBackup()
        {
            string zip = Path.Combine(this.root, "other.zip");
            using (ZipArchive archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                archive.CreateEntry("readme.txt");
            }

            ClassicAssert.IsNull(SettingsBackup.Describe(zip));
            Assert.Throws<InvalidDataException>(() => SettingsBackup.StageRestore(zip, Path.Combine(this.root, "pending"), this.settingsFolder));
        }

        [Test]
        public void RestoreReplacesFilesOnNextStart()
        {
            string zip = Zip();
            string pending = Path.Combine(this.root, "pending");
            // changes after the backup
            File.WriteAllText(this.settingsFile, "<Settings><PuttyExe>new</PuttyExe></Settings>");
            File.WriteAllText(Path.Combine(this.settingsFolder, "Sessions.XML"), "changed");
            File.WriteAllText(Path.Combine(this.settingsFolder, "layouts", "later.xml"), "later");

            SettingsBackup.StageRestore(zip, pending, this.settingsFolder);
            ClassicAssert.AreEqual("changed", File.ReadAllText(Path.Combine(this.settingsFolder, "Sessions.XML")), "nothing changes before the next start");

            string target = SettingsBackup.ApplySettingsFile(pending, this.settingsFile);
            int count = SettingsBackup.ApplyData(pending, target);

            ClassicAssert.AreEqual(this.settingsFolder, target);
            ClassicAssert.AreEqual(3, count);
            StringAssert.Contains("old", File.ReadAllText(this.settingsFile));
            ClassicAssert.AreEqual("sessions", File.ReadAllText(Path.Combine(this.settingsFolder, "Sessions.XML")));
            ClassicAssert.IsFalse(File.Exists(Path.Combine(this.settingsFolder, "layouts", "later.xml")), "layouts are replaced");
            ClassicAssert.IsTrue(File.Exists(Path.Combine(this.settingsFolder, "logs", "db01.log")), "logs stay");
            ClassicAssert.IsFalse(Directory.Exists(pending));
        }

        [Test]
        public void NoPendingRestore()
        {
            ClassicAssert.IsNull(SettingsBackup.ApplySettingsFile(Path.Combine(this.root, "nothing"), this.settingsFile));
        }

        [Test]
        public void UserConfigValuesAreRead()
        {
            string file = Path.Combine(this.root, "user.config");
            File.WriteAllText(file,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><configuration><userSettings><SuperPutty.Properties.Settings>" +
                "<setting name=\"WinSCPExe\" serializeAs=\"String\"><value>C:\\WinSCP\\WinSCP.exe</value></setting>" +
                "<setting name=\"SaveCommandHistoryDays\" serializeAs=\"String\"><value>30</value></setting>" +
                "<setting name=\"PuttyExe\" serializeAs=\"String\"><value>not moved</value></setting>" +
                "</SuperPutty.Properties.Settings></userSettings></configuration>");

            Dictionary<string, string> values = UserConfigMigration.ReadValues(file);

            ClassicAssert.AreEqual(2, values.Count);
            ClassicAssert.AreEqual("C:\\WinSCP\\WinSCP.exe", values["WinSCPExe"]);
            ClassicAssert.AreEqual("30", values["SaveCommandHistoryDays"]);
        }

        [Test]
        public void UserConfigValuesAreConverted()
        {
            TestSettings settings = new TestSettings();

            int count = UserConfigMigration.Apply(settings, new Dictionary<string, string>
            {
                { "WinSCPExe", "C:\\WinSCP\\WinSCP.exe" }, { "SaveCommandHistoryDays", "30" },
                { "AutoUpdateCheck", "False" }, { "Action_OpenScriptEditor_Shortcut", "F12, Control" }, { "Unknown", "x" }
            });

            ClassicAssert.AreEqual(4, count);
            ClassicAssert.AreEqual("C:\\WinSCP\\WinSCP.exe", settings.WinSCPExe);
            ClassicAssert.AreEqual(30, settings.SaveCommandHistoryDays);
            ClassicAssert.IsFalse(settings.AutoUpdateCheck);
            ClassicAssert.AreEqual(Keys.F12 | Keys.Control, settings.Action_OpenScriptEditor_Shortcut);
        }

        [Test]
        public void NewestUserConfigOfAnotherVersionIsFound()
        {
            string exeFolder = Path.Combine(this.root, "Company", "SuperPutty.exe_Url_abc");
            string old = Path.Combine(exeFolder, "1.4.0.0", "user.config");
            Directory.CreateDirectory(Path.GetDirectoryName(old));
            File.WriteAllText(old, "<configuration/>");

            ClassicAssert.AreEqual(old, UserConfigMigration.FindUserConfig(Path.Combine(exeFolder, "1.5.0.0", "user.config")));
            ClassicAssert.AreEqual(old, UserConfigMigration.FindUserConfig(old), "the current file wins");
            ClassicAssert.IsNull(UserConfigMigration.FindUserConfig(Path.Combine(this.root, "Nothing", "x", "1.5", "user.config")));
        }

        /// <summary>Settings of the same names and types, kept in memory only (never saved)</summary>
        private class TestSettings : ApplicationSettingsBase
        {
            [UserScopedSetting, DefaultSettingValue("")]
            public string WinSCPExe { get { return (string)this["WinSCPExe"]; } }

            [UserScopedSetting, DefaultSettingValue("0")]
            public int SaveCommandHistoryDays { get { return (int)this["SaveCommandHistoryDays"]; } }

            [UserScopedSetting, DefaultSettingValue("True")]
            public bool AutoUpdateCheck { get { return (bool)this["AutoUpdateCheck"]; } }

            [UserScopedSetting, DefaultSettingValue("None")]
            public Keys Action_OpenScriptEditor_Shortcut { get { return (Keys)this["Action_OpenScriptEditor_Shortcut"]; } }
        }
    }
}
