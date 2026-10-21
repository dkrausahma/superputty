using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Data;

namespace SuperPuttyUnitTests
{
    /// <summary>Health badges: parsing the probe output and rating it</summary>
    [TestFixture]
    public class HostHealthTests
    {
        private const string HealthyOutput =
            "load=0.42\ncpus=4\ndisk=/=41%\ndisk=/var=55%\nmem=37\nunhealthy=0\n";

        [Test]
        public void ParsesProbeOutput()
        {
            HostHealth health = HostHealth.Parse(HealthyOutput.Replace("\n", "\r\n"));

            ClassicAssert.IsNull(health.Error);
            ClassicAssert.AreEqual(0.42, health.Load.Value, 0.001);
            ClassicAssert.AreEqual(4, health.Cpus);
            ClassicAssert.AreEqual(41, health.Disks["/"]);
            ClassicAssert.AreEqual(55, health.Disks["/var"]);
            ClassicAssert.AreEqual(37, health.MemoryPercent);
            ClassicAssert.AreEqual(HealthLevel.Ok, health.Level);
            CollectionAssert.IsEmpty(health.Problems());
        }

        [Test]
        public void RatesProblems()
        {
            ClassicAssert.AreEqual(HealthLevel.Warning, HostHealth.Parse("load=0.1\ncpus=2\ndisk=/=83%\n").Level);
            ClassicAssert.AreEqual(HealthLevel.Critical, HostHealth.Parse("load=0.1\ncpus=2\ndisk=/data=95%\n").Level);
            ClassicAssert.AreEqual(HealthLevel.Warning, HostHealth.Parse("load=0.1\ncpus=2\nmem=90\n").Level);
            ClassicAssert.AreEqual(HealthLevel.Warning, HostHealth.Parse("load=4.5\ncpus=4\n").Level, "load per CPU >= 1");
            ClassicAssert.AreEqual(HealthLevel.Critical, HostHealth.Parse("load=9\ncpus=4\n").Level, "load per CPU >= 2");
            ClassicAssert.AreEqual(HealthLevel.Warning, HostHealth.Parse("load=0.1\nfailedunit=a.service\nfailedunit=b.mount\n").Level);
            ClassicAssert.AreEqual(HealthLevel.Critical, HostHealth.Parse("load=0.1\nunhealthy=1\n").Level);
        }

        [Test]
        public void ProblemsAreSortedBySeverity()
        {
            HostHealth health = HostHealth.Parse("load=0.1\ncpus=1\nfailedunit=nvidia-fabricmanager.service\ndisk=/=97%\n");

            ClassicAssert.AreEqual(HealthLevel.Critical, health.Problems()[0].Key);
            StringAssert.Contains("disk / 97% full", health.Problems()[0].Value);
            StringAssert.Contains("1 failed systemd unit(s): nvidia-fabricmanager.service", health.Problems()[1].Value);
        }

        [Test]
        public void NoFailedUnitLinesMeansOk()
        {
            // the old line-counting check reported 1 failed unit here (gpu-dev01, 2026-10-21)
            HostHealth health = HostHealth.Parse(HealthyOutput);

            ClassicAssert.AreEqual(0, health.FailedUnits);
            ClassicAssert.AreEqual(HealthLevel.Ok, health.Level);
        }

        [Test]
        public void UnreadableOutputIsAFailedCheck()
        {
            HostHealth health = HostHealth.Parse("bash: something odd\n");

            ClassicAssert.IsNotNull(health.Error);
            ClassicAssert.AreEqual(HealthLevel.Unknown, health.Level);
            StringAssert.StartsWith("Health check failed", health.Describe());
        }

        [Test]
        public void DescribeListsProblemsAndValues()
        {
            string text = HostHealth.Parse("load=1.50\ncpus=1\ndisk=/=50%\nmem=40\n").Describe();

            StringAssert.Contains("WARNING", text);
            StringAssert.Contains("! load 1.5 on 1 CPU(s)", text);
            StringAssert.Contains("memory 40% used", text);
            StringAssert.Contains("disk / 50%", text);
        }
    }
}
