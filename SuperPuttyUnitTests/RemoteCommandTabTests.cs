using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Data;
using SuperPutty.Utils;

namespace SuperPuttyUnitTests
{
    /// <summary>Tabs that run a remote command on the host of an existing session</summary>
    [TestFixture]
    public class RemoteCommandTabTests
    {
        private static SessionData Session()
        {
            return new SessionData
            {
                SessionId = "Prod/web01",
                SessionName = "web01",
                Host = "web01.example.com",
                Port = 22,
                Proto = ConnectionProtocol.SSH,
                Username = "root",
                ExtraArgs = "-L 5432:localhost:5432",
                SPSLFileName = @"C:\scripts\login.spsl"
            };
        }

        [Test]
        public void CommandSessionAddsRemoteCommandArgumentsAndTitle()
        {
            SessionData original = Session();

            SessionData copy = RemoteCommandTab.CreateCommandSession(original, @"C:\Temp\cmd 1.sh", "logs nginx");

            ClassicAssert.AreEqual("-L 5432:localhost:5432 -t -m \"C:\\Temp\\cmd 1.sh\"", copy.ExtraArgs);
            ClassicAssert.AreEqual("logs nginx", copy.SessionName);
            ClassicAssert.AreEqual("Prod/web01 > logs nginx", copy.SessionId);
            ClassicAssert.IsNull(copy.SPSLFileName, "the login script must not run in a command tab");
            ClassicAssert.AreEqual("web01.example.com", copy.Host);
            ClassicAssert.AreEqual("root", copy.Username);
        }

        [Test]
        public void OriginalSessionIsUnchanged()
        {
            SessionData original = Session();

            RemoteCommandTab.CreateCommandSession(original, @"C:\Temp\cmd.sh", "shell");

            ClassicAssert.AreEqual("-L 5432:localhost:5432", original.ExtraArgs);
            ClassicAssert.AreEqual("Prod/web01", original.SessionId);
            ClassicAssert.AreEqual(@"C:\scripts\login.spsl", original.SPSLFileName);
        }

        [Test]
        public void WorksWithoutExtraArguments()
        {
            SessionData original = Session();
            original.ExtraArgs = null;

            ClassicAssert.AreEqual("-t -m \"C:\\Temp\\cmd.sh\"", RemoteCommandTab.CreateCommandSession(original, @"C:\Temp\cmd.sh", "x").ExtraArgs);
        }

        [Test]
        public void KeepShellAfterStartsALoginShell()
        {
            ClassicAssert.AreEqual("df -h; exec \"${SHELL:-sh}\" -l", RemoteCommandTab.KeepShellAfter("df -h  "));
        }

        [Test]
        public void OnlySshSessionsAreSupported()
        {
            SessionData session = Session();
            ClassicAssert.IsTrue(RemoteCommandTab.IsSupported(session));
            session.Proto = ConnectionProtocol.SSH2;
            ClassicAssert.IsTrue(RemoteCommandTab.IsSupported(session));
            session.Proto = ConnectionProtocol.Telnet;
            ClassicAssert.IsFalse(RemoteCommandTab.IsSupported(session));
            ClassicAssert.IsFalse(RemoteCommandTab.IsSupported(null));
        }
    }
}
