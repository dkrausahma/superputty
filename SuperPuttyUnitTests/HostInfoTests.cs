using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Data;

namespace SuperPuttyUnitTests
{
    /// <summary>Host Info pane: reading the script's rounds, CPU from two samples, addresses, who lines, text and health</summary>
    [TestFixture]
    public class HostInfoTests
    {
        private static readonly string[] FirstRound =
        {
            "host=gpu-dev01.spoke52.example.com",
            "os=Ubuntu 24.04.1 LTS",
            "kernel=6.8.0-45-generic",
            "cpus=4",
            "addr=lo=127.0.0.1/8",
            "addr=eth0=78.47.238.52/32",
            "addr=eth0=2a01:4f8:c012:abcd::1/64",
            "addr=eth0=fe80::9400:3ff:fe1a:2b3c/64",
            "addr=enp7s0=10.52.6.2/32",
            "addr=docker0=172.17.0.1/16",
            "addr=br-1c2d3e4f=172.18.0.1/16",
            "disk=/dev/sda1=/=41152736=21000000=18000000",
            "disk=/dev/sda1=/var/lib/docker/plugins=41152736=21000000=18000000",
            "disk=/dev/sdb=/data=103081248=82000000=16000000",
            "user=root     pts/0        2026-10-24 09:12 (10.8.0.6)",
            "user=dominique pts/1        2026-10-24 10:01 (vpn.example.com)",
            "user=admin    tty1         Oct 24 08:00",
            "failedunit=nvidia-fabricmanager.service",
            "unhealthy=1",
            "slow=1",
            "uptime=3083367.12",
            "loadavg=0.05 0.08 0.08 2/671 12345",
            "cpustat=cpu  1000 0 500 8000 500 0 0 0 0 0",
            "meminfo=MemTotal=65378304",
            "meminfo=MemAvailable=50034688",
            "meminfo=SwapTotal=8388608",
            "meminfo=SwapFree=6021120",
            "proc=1=100=systemd",
            "proc=812=5000=python3",
            "proc=900=40=tmux: server",
            "--end",
        };

        // 5 s later: 4 CPUs x 100 jiffies x 5 s = 2000 jiffies, 500 of them busy
        private static readonly string[] SecondRound =
        {
            "uptime=3083372.50",
            "loadavg=0.20 0.10 0.09 3/672 12346",
            "cpustat=cpu  1400 0 600 9400 600 0 0 0 0 0",
            "meminfo=MemTotal=65378304",
            "meminfo=MemAvailable=49000000",
            "meminfo=SwapTotal=8388608",
            "meminfo=SwapFree=6021120",
            "proc=1=100=systemd",
            "proc=812=5400=python3",
            "proc=900=50=tmux: server",
            "proc=950=10=sshd",
            "--end",
        };

        private static List<HostInfo> Feed(params string[][] rounds)
        {
            HostInfoReader reader = new HostInfoReader();
            List<HostInfo> snapshots = new List<HostInfo>();
            foreach (string line in rounds.SelectMany(r => r))
            {
                HostInfo info = reader.Read(line);
                if (info != null)
                {
                    snapshots.Add(info);
                }
            }
            return snapshots;
        }

        [Test]
        public void FirstRoundHasTheFactsButNoCpuYet()
        {
            HostInfo info = Feed(FirstRound).Single();

            ClassicAssert.AreEqual("gpu-dev01.spoke52.example.com", info.HostName);
            ClassicAssert.AreEqual("Ubuntu 24.04.1 LTS", info.Os);
            ClassicAssert.AreEqual(4, info.Cpus);
            ClassicAssert.AreEqual(TimeSpan.FromSeconds(3083367), info.Uptime);
            CollectionAssert.AreEqual(new[] { 0.05, 0.08, 0.08 }, info.Load);
            ClassicAssert.AreEqual(3, info.Processes);
            ClassicAssert.IsNull(info.CpuPercent, "needs two samples");
            CollectionAssert.IsEmpty(info.TopProcesses);
            ClassicAssert.AreEqual(23, info.MemoryPercent); // (65378304 - 50034688) / 65378304
            ClassicAssert.AreEqual(8388608 - 6021120, info.SwapUsedKb);
            ClassicAssert.IsTrue(info.HasSlowData);
            CollectionAssert.AreEqual(new[] { "nvidia-fabricmanager.service" }, info.FailedUnits);
            ClassicAssert.AreEqual(1, info.UnhealthyContainers);
        }

        [Test]
        public void SecondRoundComputesCpuAndBusiestProcesses()
        {
            HostInfo info = Feed(FirstRound, SecondRound)[1];

            // busy = total - idle - iowait = 2000 - 1400 - 100
            ClassicAssert.AreEqual(25.0, info.CpuPercent.Value, 0.001);
            // one CPU = 2000 / 4 = 500 jiffies: python3 400 -> 80 %, tmux 10 -> 2 %; systemd idle, sshd new
            CollectionAssert.AreEqual(new[] { "python3", "tmux: server" }, info.TopProcesses.Select(p => p.Name));
            ClassicAssert.AreEqual(80.0, info.TopProcesses[0].CpuPercent, 0.001);
            ClassicAssert.AreEqual(4, info.Processes);
        }

        [Test]
        public void StaticAndSlowValuesStayBetweenRounds()
        {
            HostInfo info = Feed(FirstRound, SecondRound)[1];

            ClassicAssert.AreEqual("gpu-dev01.spoke52.example.com", info.HostName);
            ClassicAssert.AreEqual(2, info.Disks.Count);
            ClassicAssert.AreEqual(3, info.Users.Count);
            ClassicAssert.AreEqual(3, info.Addresses.Count, "addresses are sent once, in the first round");
        }

        [Test]
        public void SnapshotsAreIndependentCopies()
        {
            List<HostInfo> snapshots = Feed(FirstRound, SecondRound);

            ClassicAssert.AreEqual(0.05, snapshots[0].Load[0]);
            ClassicAssert.AreEqual(0.20, snapshots[1].Load[0]);
        }

        [Test]
        public void AddressesAreTheHostsOwnSplitIntoPublicAndPrivate()
        {
            HostInfo info = Feed(FirstRound).Single();

            CollectionAssert.AreEqual(new[] { "78.47.238.52", "2a01:4f8:c012:abcd::1" }, info.PublicAddresses.Select(a => a.ToString()));
            CollectionAssert.AreEqual(new[] { "10.52.6.2" }, info.PrivateAddresses.Select(a => a.ToString()));
        }

        [Test]
        public void ClassifiesAddresses()
        {
            ClassicAssert.IsTrue(HostInfo.IsPrivate(IPAddress.Parse("10.1.2.3")));
            ClassicAssert.IsTrue(HostInfo.IsPrivate(IPAddress.Parse("172.31.255.1")));
            ClassicAssert.IsFalse(HostInfo.IsPrivate(IPAddress.Parse("172.32.0.1")));
            ClassicAssert.IsTrue(HostInfo.IsPrivate(IPAddress.Parse("192.168.1.10")));
            ClassicAssert.IsTrue(HostInfo.IsPrivate(IPAddress.Parse("100.64.0.1")), "carrier-grade NAT");
            ClassicAssert.IsTrue(HostInfo.IsPrivate(IPAddress.Parse("fd12:3456::1")), "IPv6 unique local");
            ClassicAssert.IsFalse(HostInfo.IsPrivate(IPAddress.Parse("78.47.238.52")));

            ClassicAssert.IsNull(HostInfo.ParseAddress("lo", "10.255.255.254/32"), "addresses on loopback");
            ClassicAssert.IsNull(HostInfo.ParseAddress("veth12ab@if5", "fe80::1/64"));
            ClassicAssert.IsNull(HostInfo.ParseAddress("eth0", "169.254.1.1/16"), "link-local");
            ClassicAssert.AreEqual("eth0", HostInfo.ParseAddress("eth0@if7", "10.0.0.5/24").Interface);
            ClassicAssert.IsNull(HostInfo.ParseAddress("eth0", "not-an-ip/24"));
        }

        [Test]
        public void OneDiskPerDeviceUnderItsShortestMount()
        {
            HostInfo info = Feed(FirstRound).Single();

            CollectionAssert.AreEquivalent(new[] { "/", "/data" }, info.Disks.Select(d => d.Mount));
            DiskUsage data = info.Disks.Single(d => d.Mount == "/data");
            ClassicAssert.AreEqual(84, data.Percent); // df rounds up: 82000000 / 98000000
        }

        [Test]
        public void ReadsWhoLines()
        {
            LoggedInUser remote = HostInfoReader.ParseWho("root     pts/0        2026-10-24 09:12 (10.8.0.6)");
            ClassicAssert.AreEqual("root", remote.User);
            ClassicAssert.AreEqual("pts/0", remote.Terminal);
            ClassicAssert.AreEqual("10.8.0.6", remote.From);
            ClassicAssert.AreEqual("2026-10-24 09:12", remote.Since);

            LoggedInUser local = HostInfoReader.ParseWho("admin    tty1         Oct 24 08:00");
            ClassicAssert.AreEqual("", local.From);
            ClassicAssert.AreEqual("Oct 24 08:00", local.Since);

            ClassicAssert.IsNull(HostInfoReader.ParseWho("garbage"));
        }

        [Test]
        public void TextLooksLikeALoginBanner()
        {
            string text = Feed(FirstRound, SecondRound)[1].ToText();

            StringAssert.Contains(" - Hostname............: gpu-dev01.spoke52.example.com", text);
            StringAssert.Contains(" - IP Address (Pub)....: 78.47.238.52, 2a01:4f8:c012:abcd::1", text);
            StringAssert.Contains(" - IP Address (Priv)...: 10.52.6.2", text);
            StringAssert.Contains(" - Uptime..............: 35 days, 16h:29m:32s", text);
            StringAssert.Contains(" - System load.........: 0.20, 0.10, 0.09 (1, 5, 15 min) on 4 CPU(s)", text);
            StringAssert.Contains(" - CPU usage...........: 25 %", text);
            StringAssert.Contains(" - Swap in use.........: 2312 MB", text);
            StringAssert.Contains(" - Logged-in users.....: 3 (root, dominique, admin)", text);
            StringAssert.Contains(" - Processes...........: 4", text);
            StringAssert.Contains(" - Failed units........: nvidia-fabricmanager.service", text);
        }

        [Test]
        public void FeedsTheHealthBadge()
        {
            HostHealth health = Feed(FirstRound).Single().ToHealth();

            ClassicAssert.AreEqual(0.05, health.Load);
            ClassicAssert.AreEqual(4, health.Cpus);
            ClassicAssert.AreEqual(23, health.MemoryPercent);
            ClassicAssert.AreEqual(84, health.Disks["/data"]);
            ClassicAssert.AreEqual(1, health.UnhealthyContainers);
            ClassicAssert.AreEqual(HealthLevel.Critical, health.Level, "an unhealthy container is critical");
        }

        [Test]
        public void FormatsSizesAndUptime()
        {
            ClassicAssert.AreEqual("940 MB", HostInfo.FormatSize(940 * 1024));
            ClassicAssert.AreEqual("14.6 GB", HostInfo.FormatSize((long)(14.6 * 1024 * 1024)));
            ClassicAssert.AreEqual("212 GB", HostInfo.FormatSize(212L * 1024 * 1024));
            ClassicAssert.AreEqual("3.6 TB", HostInfo.FormatSize((long)(3.6 * 1024 * 1024 * 1024)));
            ClassicAssert.AreEqual("1 day, 0h:00m:05s", HostInfo.FormatUptime(TimeSpan.FromSeconds(86405)));
            ClassicAssert.AreEqual("35 d 16 h 29 m", HostInfo.FormatUptimeShort(new TimeSpan(35, 16, 29, 27)));
            ClassicAssert.AreEqual("2 h 5 m", HostInfo.FormatUptimeShort(new TimeSpan(2, 5, 0)));
        }

        [Test]
        public void ScriptEndsEveryRoundAndCarriesAllKeys()
        {
            foreach (string key in new[] { "host=", "os=", "kernel=", "cpus=", "addr=", "disk=", "user=", "failedunit=", "unhealthy=",
                "slow=1", "uptime=", "loadavg=", "cpustat=", "meminfo=", "proc=", "--end" })
            {
                StringAssert.Contains(key, HostInfo.Script);
            }
        }
    }
}
