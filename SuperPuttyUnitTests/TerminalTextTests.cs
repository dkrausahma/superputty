using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Utils;

namespace SuperPuttyUnitTests
{
    /// <summary>Hand-off to Claude: control codes removed, last command found, log file names</summary>
    [TestFixture]
    public class TerminalTextTests
    {
        [Test]
        public void CleanRemovesColoursTitlesAndCursorCodes()
        {
            string raw = "\u001B]0;dk@web01: ~\u0007dk@web01:~$ ls --color\r\n\u001B[0m\u001B[01;34mapp\u001B[0m  notes.txt\r\n\u001B[?2004h";

            ClassicAssert.AreEqual("dk@web01:~$ ls --color\r\napp  notes.txt\r\n", TerminalText.Clean(raw));
        }

        [Test]
        public void CleanAppliesBackspacesAndCarriageReturns()
        {
            ClassicAssert.AreEqual("ls -a\r\n", TerminalText.Clean("ls -lx\b \b\b \ba\r\n"), "readline erases with backspace, space, backspace");
            ClassicAssert.AreEqual("ls -ax\r\n", TerminalText.Clean("ls -lx\b\ba\r\n"), "a bare backspace only moves the cursor");
            ClassicAssert.AreEqual("Downloading 100%\r\n", TerminalText.Clean("Downloading  10%\rDownloading  55%\rDownloading 100%\n"));
            ClassicAssert.AreEqual("doneing 50%\r\n", TerminalText.Clean("waiting 50%\rdone"), "a shorter overwrite keeps the rest of the line, as on screen");
        }

        [Test]
        public void RecognisesCommonPrompts()
        {
            ClassicAssert.IsTrue(TerminalText.IsPrompt("dk@web01:~/app$ docker ps"));
            ClassicAssert.IsTrue(TerminalText.IsPrompt("root@web01:/var/log# tail syslog"));
            ClassicAssert.IsTrue(TerminalText.IsPrompt("[root@db01 ~]# systemctl status postgresql"));
            ClassicAssert.IsTrue(TerminalText.IsPrompt("(venv) dk@web01:~/app$ pip list"));
            ClassicAssert.IsTrue(TerminalText.IsPrompt("dk@mac ~ % ls"));
            ClassicAssert.IsTrue(TerminalText.IsPrompt("dk@web01:~$"));
            ClassicAssert.IsFalse(TerminalText.IsPrompt("total 48"));
            ClassicAssert.IsFalse(TerminalText.IsPrompt("drwxr-xr-x 2 dk dk 4096 Oct 20 app"));
            ClassicAssert.IsFalse(TerminalText.IsPrompt("Email: dk@example.com is set"));
        }

        [Test]
        public void LastCommandIsCutBetweenPrompts()
        {
            string text =
                "dk@web01:~$ uptime\r\n" +
                " 21:04:11 up 3 days\r\n" +
                "dk@web01:~$ df -h /\r\n" +
                "Filesystem  Size  Used Avail Use% Mounted on\r\n" +
                "/dev/sda1    40G   31G  9.0G  78% /\r\n" +
                "dk@web01:~$ \r\n" +
                "dk@web01:~$ \r\n" +
                "\r\n";

            ClassicAssert.AreEqual(
                "dk@web01:~$ df -h /\r\nFilesystem  Size  Used Avail Use% Mounted on\r\n/dev/sda1    40G   31G  9.0G  78% /\r\n",
                TerminalText.LastCommand(text));
        }

        [Test]
        public void LastCommandOfARunningCommandGoesToTheEnd()
        {
            string text = "[root@db01 ~]# ls\nfoo\n[root@db01 ~]# journalctl -f\nOct 21 line 1\nOct 21 line 2\n";

            ClassicAssert.AreEqual("[root@db01 ~]# journalctl -f\r\nOct 21 line 1\r\nOct 21 line 2\r\n", TerminalText.LastCommand(text));
        }

        [Test]
        public void NoPromptGivesNull()
        {
            ClassicAssert.IsNull(TerminalText.LastCommand("some output\nwithout any prompt\n"));
            ClassicAssert.IsNull(TerminalText.LastCommand(""));
        }

        [Test]
        public void LogFileNamesAreSafe()
        {
            ClassicAssert.AreEqual("web01 _prod_-20261021-063005.log", SessionLog.FileName("web01 <prod>", new DateTime(2026, 10, 21, 6, 30, 5)));
            ClassicAssert.AreEqual("session-20261021-063005.log", SessionLog.FileName("", new DateTime(2026, 10, 21, 6, 30, 5)));
        }
    }
}
