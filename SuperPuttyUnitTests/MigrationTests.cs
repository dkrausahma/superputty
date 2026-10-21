using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Threading;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Utils;

namespace SuperPuttyUnitTests
{
    /// <summary>Checks for behaviour that changed when moving from .NET Framework 4.8 to .NET 10</summary>
    [TestFixture]
    public class MigrationTests
    {
        /// <summary>
        /// Form resources (ImageListStreamer, Bitmap, Icon...) were binary-serialized by .NET Framework.
        /// .NET 9+ has no BinaryFormatter, so every embedded resource must still deserialize.
        /// </summary>
        [Test]
        public void AllEmbeddedResourcesDeserialize()
        {
            var assembly = typeof(SuperPutty.SuperPuTTY).Assembly;
            string[] resourceFiles = assembly.GetManifestResourceNames().Where(n => n.EndsWith(".resources")).ToArray();
            ClassicAssert.Greater(resourceFiles.Length, 20);

            int icons = 0, bitmaps = 0;
            foreach (string resourceFile in resourceFiles)
            {
                string baseName = resourceFile.Substring(0, resourceFile.Length - ".resources".Length);
                ResourceSet set = new ResourceManager(baseName, assembly).GetResourceSet(CultureInfo.InvariantCulture, true, true);
                foreach (DictionaryEntry entry in set)
                {
                    ClassicAssert.IsNotNull(entry.Value, baseName + ":" + entry.Key);
                    if (entry.Value is System.Drawing.Icon) icons++;
                    if (entry.Value is System.Drawing.Bitmap) bitmaps++;
                }
            }
            // the forms' window icons and the toolbar/menu images
            ClassicAssert.Greater(icons, 0);
            ClassicAssert.Greater(bitmaps, 0);
        }

        /// <summary>Single instance mode now forwards the command line over a named pipe instead of .NET Remoting</summary>
        [Test]
        public void SingleInstancePipeForwardsArguments()
        {
            string pipeName = "SuperPuTTY.Test." + Guid.NewGuid().ToString("N");
            string[] received = null;
            using (ManualResetEvent done = new ManualResetEvent(false))
            {
                SingleInstanceHelper.StartListener(pipeName, args => { received = args; done.Set(); });

                string[] sent = { "-session", "Folder/My Session", "", "ünïcødé \"quoted\"" };
                SingleInstanceHelper.Send(pipeName, sent);

                ClassicAssert.IsTrue(done.WaitOne(5000), "listener did not receive arguments");
                CollectionAssert.AreEqual(sent, received);

                // the listener keeps serving after the first command
                done.Reset();
                SingleInstanceHelper.Send(pipeName, new[] { "-layout", "second" });
                ClassicAssert.IsTrue(done.WaitOne(5000), "listener stopped after the first command");
                CollectionAssert.AreEqual(new[] { "-layout", "second" }, received);
            }
        }

        /// <summary>Program.SetDefaultIcon overwrites this private WinForms field via reflection</summary>
        [Test]
        public void DefaultIconFieldExists()
        {
            ClassicAssert.IsNotNull(typeof(System.Windows.Forms.Form).GetField(
                "s_defaultIcon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static));
        }

        [Test]
        public void LegacyUserConfigIsFoundForNewLocation()
        {
            string company = Path.Combine(Path.GetTempPath(), "superputty-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                string older = WriteConfig(company, @"SuperPutty.exe_Url_aaa\1.4.0.8\user.config", DateTime.UtcNow.AddDays(-2));
                string newer = WriteConfig(company, @"SuperPutty.exe_Url_bbb\1.4.0.9\user.config", DateTime.UtcNow.AddDays(-1));
                WriteConfig(company, @"OtherApp.exe_Url_ccc\1.0.0.0\user.config", DateTime.UtcNow);
                string current = Path.Combine(company, @"SuperPutty_Url_ddd\1.4.0.9\user.config");

                ClassicAssert.AreEqual(newer, LegacyUserConfig.FindLegacyUserConfig(current, "SuperPutty.exe"));

                // nothing to import once the new location has its own user.config
                WriteConfig(company, @"SuperPutty_Url_ddd\1.4.0.9\user.config", DateTime.UtcNow);
                ClassicAssert.IsNull(LegacyUserConfig.FindLegacyUserConfig(current, "SuperPutty.exe"));
                ClassicAssert.IsTrue(File.Exists(older));
            }
            finally
            {
                Directory.Delete(company, true);
            }
        }

        private static string WriteConfig(string root, string relativePath, DateTime lastWriteUtc)
        {
            string path = Path.Combine(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "<configuration/>");
            File.SetLastWriteTimeUtc(path, lastWriteUtc);
            return path;
        }
    }
}
