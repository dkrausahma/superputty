using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Data;
using SuperPutty.Scp;
using SuperPutty.Utils;

namespace SuperPuttyUnitTests
{
    /// <summary>Uploads: current directory from the window title, pscp paths and targets</summary>
    [TestFixture]
    public class RemotePathTests
    {
        [Test]
        public void ReadsDirectoryFromCommonShellTitles()
        {
            ClassicAssert.AreEqual("~/projects/app", RemotePath.FromWindowTitle("dk@web01: ~/projects/app"));   // Debian/Ubuntu bash
            ClassicAssert.AreEqual("/var/log", RemotePath.FromWindowTitle("root@db01:/var/log"));                // Fedora/RHEL bash
            ClassicAssert.AreEqual("~", RemotePath.FromWindowTitle("dk@web01: ~"));
            ClassicAssert.AreEqual("/opt/my app", RemotePath.FromWindowTitle("dk@web01: /opt/my app"));          // spaces in the path
            ClassicAssert.AreEqual("/srv/data", RemotePath.FromWindowTitle("/srv/data"));                        // bare path
        }

        [Test]
        public void NoDirectoryInOtherTitles()
        {
            ClassicAssert.IsNull(RemotePath.FromWindowTitle("web01.example.com - PuTTY"));
            ClassicAssert.IsNull(RemotePath.FromWindowTitle("htop"));
            ClassicAssert.IsNull(RemotePath.FromWindowTitle("vim notes.txt"));
            ClassicAssert.IsNull(RemotePath.FromWindowTitle(""));
            ClassicAssert.IsNull(RemotePath.FromWindowTitle(null));
        }

        [Test]
        public void ConvertsHomePathsForPscp()
        {
            ClassicAssert.AreEqual(".", RemotePath.ToPscpPath("~"));
            ClassicAssert.AreEqual(".", RemotePath.ToPscpPath(""));
            ClassicAssert.AreEqual("projects/app", RemotePath.ToPscpPath("~/projects/app"));
            ClassicAssert.AreEqual("/var/www", RemotePath.ToPscpPath(" /var/www "));
        }

        [Test]
        public void PscpTargetOmitsMissingUser()
        {
            SessionData withUser = new SessionData { Host = "web01", Username = "dk", Port = 22 };
            SessionData withoutUser = new SessionData { Host = "web01", Port = 22 };

            ClassicAssert.AreEqual("dk@web01", PscpClient.UserAtHost(withUser));
            ClassicAssert.AreEqual("web01", PscpClient.UserAtHost(withoutUser));
            StringAssert.EndsWith("web01:\"/tmp\"", PscpClient.ToArgs(withoutUser, null, "/tmp"));
        }
    }
}
