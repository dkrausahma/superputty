using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Data;
using SuperPutty.Utils;

namespace SuperPuttyUnitTests
{
    /// <summary>Stored passwords: DPAPI encryption in Sessions.XML, password files instead of -pw</summary>
    [TestFixture]
    public class PasswordProtectionTests
    {
        private string file;

        [SetUp]
        public void SetUp()
        {
            this.file = Path.Combine(Path.GetTempPath(), "SuperPuTTY.PasswordTests." + Guid.NewGuid().ToString("N") + ".xml");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string f in Directory.GetFiles(Path.GetDirectoryName(this.file), Path.GetFileName(this.file) + "*"))
            {
                File.Delete(f);
            }
        }

        private static SessionData Session(string extraArgs)
        {
            return new SessionData { SessionId = "Prod/db01", SessionName = "db01", Host = "db01", Port = 22, Proto = ConnectionProtocol.SSH, ExtraArgs = extraArgs };
        }

        [Test]
        public void DpapiRoundTrip()
        {
            string protectedValue = Dpapi.Protect("s3cr3t äöü");

            StringAssert.StartsWith(Dpapi.Prefix, protectedValue);
            StringAssert.DoesNotContain("s3cr3t", protectedValue);
            ClassicAssert.AreEqual("s3cr3t äöü", Dpapi.Unprotect(protectedValue));
            ClassicAssert.IsNull(Dpapi.Protect(""));
            ClassicAssert.AreEqual("plain", Dpapi.Unprotect("plain"), "values from older files pass through");
        }

        [Test]
        public void SavedFileHasNoPlainPassword()
        {
            SessionData.SaveSessionsToFile(new List<SessionData> { Session("-L 5432:localhost:5432 -pw s3cr3t") }, this.file);
            string xml = File.ReadAllText(this.file);

            StringAssert.DoesNotContain("s3cr3t", xml);
            StringAssert.DoesNotContain("-pw", xml);
            StringAssert.Contains("ProtectedPassword=\"dpapi:", xml);
            StringAssert.Contains("ExtraArgs=\"-L 5432:localhost:5432\"", xml);
        }

        [Test]
        public void LoadedSessionHasItsPasswordBack()
        {
            SessionData.SaveSessionsToFile(new List<SessionData> { Session("-L 5432:localhost:5432 -pw \"two words\"") }, this.file);

            SessionData loaded = SessionData.LoadSessionsFromFile(this.file)[0];

            ClassicAssert.AreEqual("two words", loaded.Password);
            StringAssert.StartsWith("-L 5432:localhost:5432", loaded.ExtraArgs);
        }

        [Test]
        public void OldPlainTextFileIsConvertedOnSave()
        {
            File.WriteAllText(this.file,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><ArrayOfSessionData xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">" +
                "<SessionData SessionId=\"web01\" SessionName=\"web01\" Host=\"web01\" Port=\"22\" Proto=\"SSH\" ExtraArgs=\"-pw old\" /></ArrayOfSessionData>");

            List<SessionData> sessions = SessionData.LoadSessionsFromFile(this.file);
            ClassicAssert.AreEqual("old", sessions[0].Password);

            SessionData.SaveSessionsToFile(sessions, this.file);
            StringAssert.DoesNotContain("-pw old", File.ReadAllText(this.file));
            ClassicAssert.AreEqual("old", SessionData.LoadSessionsFromFile(this.file)[0].Password);
        }

        [Test]
        public void SessionWithoutPasswordSavesNoProtectedPassword()
        {
            SessionData.SaveSessionsToFile(new List<SessionData> { Session(null) }, this.file);

            StringAssert.DoesNotContain("ProtectedPassword", File.ReadAllText(this.file));
        }

        [Test]
        public void CloneKeepsThePassword()
        {
            SessionData clone = (SessionData)Session("-L 1:a:1 -pw s3cr3t").Clone();

            ClassicAssert.AreEqual("s3cr3t", clone.Password);
            StringAssert.Contains("-pw s3cr3t", clone.ExtraArgs);
        }

        [Test]
        public void PasswordFileHoldsThePassword()
        {
            string path = PasswordFile.Create("s3cr3t");
            try
            {
                ClassicAssert.AreEqual("s3cr3t", File.ReadAllText(path));
                StringAssert.StartsWith(PasswordFile.Folder, path);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
