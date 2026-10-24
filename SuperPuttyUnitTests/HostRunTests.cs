using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Data;
using SuperPutty.Utils;

namespace SuperPuttyUnitTests
{
    /// <summary>Run on Hosts: turning plink results into host results, and the text report</summary>
    [TestFixture]
    public class HostRunTests
    {
        private static HostRunResult Complete(PlinkResult plink, double seconds = 1.2)
        {
            HostRunResult result = new HostRunResult { Session = new SessionData { SessionId = "Prod/web01", SessionName = "web01" }, State = HostRunState.Running };
            result.Complete(plink, TimeSpan.FromSeconds(seconds));
            return result;
        }

        [Test]
        public void ExitCodeZeroSucceeds()
        {
            HostRunResult result = Complete(new PlinkResult { ExitCode = 0, Output = "\u001b[32m/dev/sda1  40G  21G  52% /\u001b[0m\n", Error = "" });

            ClassicAssert.AreEqual(HostRunState.Succeeded, result.State);
            ClassicAssert.AreEqual("ok", result.StateText);
            ClassicAssert.AreEqual("/dev/sda1  40G  21G  52% /", result.Output, "colour codes removed");
            ClassicAssert.AreEqual("/dev/sda1  40G  21G  52% /", result.FirstLine);
        }

        [Test]
        public void OtherExitCodesFailWithStderrAppended()
        {
            HostRunResult result = Complete(new PlinkResult { ExitCode = 3, Output = "partial\n", Error = "systemctl: unit not found\n" });

            ClassicAssert.AreEqual(HostRunState.Failed, result.State);
            ClassicAssert.AreEqual("exit 3", result.StateText);
            ClassicAssert.AreEqual("partial\r\n--- stderr ---\r\nsystemctl: unit not found", result.Output);
            ClassicAssert.IsTrue(result.IsProblem);
        }

        [Test]
        public void PlinksOwnFailuresAreErrorsNotCommandFailures()
        {
            HostRunResult result = Complete(new PlinkResult { ExitCode = 1, Output = "", Error = "FATAL ERROR: Cannot confirm a host key in batch mode\n" });
            ClassicAssert.AreEqual(HostRunState.Error, result.State);
            ClassicAssert.AreEqual("FATAL ERROR: Cannot confirm a host key in batch mode", result.FirstLine);

            HostRunResult unknown = Complete(new PlinkResult { ExitCode = 1, Output = "", Error = "Unable to open connection:\nHost does not exist\n" });
            ClassicAssert.AreEqual(HostRunState.Error, unknown.State);
            ClassicAssert.AreEqual("Unable to open connection: Host does not exist", unknown.FirstLine, "the reason from the next line");

            // a command that fails with code 1 and writes to stderr stays a failure
            HostRunResult grep = Complete(new PlinkResult { ExitCode = 1, Output = "", Error = "grep: /etc/nothing: No such file or directory\n" });
            ClassicAssert.AreEqual(HostRunState.Failed, grep.State);
        }

        [Test]
        public void TimeoutAndStop()
        {
            HostRunResult timeout = Complete(new PlinkResult { TimedOut = true, ExitCode = -1, Output = "", Error = "Timed out after 60 s" });
            ClassicAssert.AreEqual(HostRunState.Error, timeout.State);
            ClassicAssert.AreEqual("Timed out after 60 s", timeout.Output);

            HostRunResult stopped = Complete(new PlinkResult { Cancelled = true, ExitCode = -1, Output = "", Error = "Stopped" });
            ClassicAssert.AreEqual(HostRunState.Stopped, stopped.State);
            ClassicAssert.IsFalse(stopped.IsProblem);
        }

        [Test]
        public void ReportListsEveryHostWithItsOutput()
        {
            HostRunResult ok = Complete(new PlinkResult { ExitCode = 0, Output = "up 3 days\n", Error = "" });
            HostRunResult failed = Complete(new PlinkResult { ExitCode = 2, Output = "", Error = "boom\n" }, 0.25);
            failed.Session.SessionName = "db01";

            string report = HostRunReport.Format("uptime", new DateTime(2026, 10, 24, 11, 5, 0), new[] { ok, failed });

            ClassicAssert.AreEqual(
                "Command: uptime\r\n" +
                "Run:     2026-10-24 11:05:00 on 2 host(s): 1 ok, 1 failed, 0 error(s)\r\n" +
                "\r\n=== web01 (ok, 1.2 s) ===\r\nup 3 days\r\n" +
                "\r\n=== db01 (exit 2, 0.3 s) ===\r\nboom\r\n",
                report.Replace(Environment.NewLine, "\r\n"));
        }
    }
}
