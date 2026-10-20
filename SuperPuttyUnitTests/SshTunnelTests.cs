using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Data;
using SuperPutty.Utils;

namespace SuperPuttyUnitTests
{
    /// <summary>SSH tunnels on the tab: parsing, state, display, listener table</summary>
    [TestFixture]
    public class SshTunnelTests
    {
        [Test]
        public void ParsesPuttyProfileForwardings()
        {
            List<SshTunnel> tunnels = SshTunnel.ParsePuttyForwardings(
                "L5432=localhost:5432,R8080=localhost:80,D1080=,4L127.0.0.1:8443=web:443,6D1081", "PuTTY profile");

            ClassicAssert.AreEqual(5, tunnels.Count);
            ClassicAssert.AreEqual("L 5432 → localhost:5432", tunnels[0].Describe());
            ClassicAssert.AreEqual("R 8080 → localhost:80", tunnels[1].Describe());
            ClassicAssert.IsFalse(tunnels[1].IsLocal);
            ClassicAssert.AreEqual("D 1080 (SOCKS)", tunnels[2].Describe());
            ClassicAssert.AreEqual("L 127.0.0.1:8443 → web:443", tunnels[3].Describe());
            ClassicAssert.AreEqual(8443, tunnels[3].ListenPort);
            ClassicAssert.AreEqual("D 1081 (SOCKS)", tunnels[4].Describe());
            ClassicAssert.AreEqual("PuTTY profile", tunnels[0].Origin);
        }

        [Test]
        public void IgnoresEmptyAndInvalidForwardings()
        {
            ClassicAssert.AreEqual(0, SshTunnel.ParsePuttyForwardings(null, "x").Count);
            ClassicAssert.AreEqual(0, SshTunnel.ParsePuttyForwardings("", "x").Count);
            ClassicAssert.AreEqual(0, SshTunnel.ParsePuttyForwardings("X1=a:1,Labc=a:1,L99999=a:1,L5432=", "x").Count);
        }

        [Test]
        public void ParsesCommandLineForwardings()
        {
            List<SshTunnel> tunnels = SshTunnel.ParseArguments(
                "-v -L 5432:db:5432 -L127.0.0.1:8080:web:80 -R 9000:localhost:9000 -D 1080 -i key.ppk -l", "extra arguments");

            ClassicAssert.AreEqual(4, tunnels.Count);
            ClassicAssert.AreEqual("L 5432 → db:5432", tunnels[0].Describe());
            ClassicAssert.AreEqual("L 127.0.0.1:8080 → web:80", tunnels[1].Describe());
            ClassicAssert.AreEqual("R 9000 → localhost:9000", tunnels[2].Describe());
            ClassicAssert.AreEqual("D 1080 (SOCKS)", tunnels[3].Describe());
        }

        [Test]
        public void StateDependsOnWhoHoldsThePort()
        {
            SshTunnel local = new SshTunnel { Kind = TunnelKind.Local, ListenPort = 5432, Destination = "db:5432" };
            SshTunnel remote = new SshTunnel { Kind = TunnelKind.Remote, ListenPort = 9000, Destination = "localhost:9000" };
            Dictionary<int, HashSet<int>> listeners = new Dictionary<int, HashSet<int>> { { 5432, new HashSet<int> { 100 } } };
            int[] noOtherTabs = new int[0];

            ClassicAssert.AreEqual(TunnelState.Up, TunnelMonitor.StateOf(local, 100, listeners, noOtherTabs, false));
            ClassicAssert.AreEqual(TunnelState.Up, TunnelMonitor.StateOf(local, 200, listeners, new[] { 100 }, false), "held by a tab of the same host");
            ClassicAssert.AreEqual(TunnelState.Conflict, TunnelMonitor.StateOf(local, 200, listeners, noOtherTabs, false));
            ClassicAssert.AreEqual(TunnelState.Unknown, TunnelMonitor.StateOf(local, 200, listeners, noOtherTabs, true), "still connecting");
            ClassicAssert.AreEqual(TunnelState.Down, TunnelMonitor.StateOf(local, 100, new Dictionary<int, HashSet<int>>(), noOtherTabs, false));
            ClassicAssert.AreEqual(TunnelState.NotCheckable, TunnelMonitor.StateOf(remote, 100, listeners, noOtherTabs, false));
        }

        [Test]
        public void SameTargetComparesHostPortAndProfile()
        {
            SessionData a = new SessionData { Host = "DB01.example.com", Port = 22, PuttySession = "db", Proto = ConnectionProtocol.SSH };
            SessionData b = new SessionData { Host = "db01.example.com", Port = 22, PuttySession = "db", Proto = ConnectionProtocol.SSH };
            SessionData otherHost = new SessionData { Host = "db02.example.com", Port = 22, PuttySession = "db", Proto = ConnectionProtocol.SSH };
            SessionData otherProfile = new SessionData { Host = "db01.example.com", Port = 22, PuttySession = "db-admin", Proto = ConnectionProtocol.SSH };

            ClassicAssert.IsTrue(TunnelMonitor.SameTarget(a, b));
            ClassicAssert.IsFalse(TunnelMonitor.SameTarget(a, otherHost));
            ClassicAssert.IsFalse(TunnelMonitor.SameTarget(a, otherProfile));
        }

        [Test]
        public void TabSuffixMarksBrokenTunnels()
        {
            ClassicAssert.AreEqual(string.Empty, TunnelMonitor.TabSuffix(new TunnelState[0]));
            ClassicAssert.AreEqual(" ↔2", TunnelMonitor.TabSuffix(new[] { TunnelState.Up, TunnelState.NotCheckable }));
            ClassicAssert.AreEqual(" ↔1", TunnelMonitor.TabSuffix(new[] { TunnelState.Unknown }));
            ClassicAssert.AreEqual(" ↔2!", TunnelMonitor.TabSuffix(new[] { TunnelState.Up, TunnelState.Down }));
            ClassicAssert.AreEqual(" ↔1!", TunnelMonitor.TabSuffix(new[] { TunnelState.Conflict }));
        }

        [Test]
        public void ToolTipListsTunnelsWithState()
        {
            SshTunnel[] tunnels = {
                new SshTunnel { Kind = TunnelKind.Local, ListenPort = 5432, Destination = "db:5432" },
                new SshTunnel { Kind = TunnelKind.Local, ListenPort = 8080, Destination = "web:80" } };

            string toolTip = TunnelMonitor.ToolTip(tunnels, new[] { TunnelState.Up, TunnelState.Conflict },
                new Dictionary<int, string> { { 8080, "nginx (PID 42)" } });

            ClassicAssert.AreEqual("SSH tunnels:\nL 5432 → db:5432  —  up\nL 8080 → web:80  —  port in use by nginx (PID 42)", toolTip);
        }

        [Test]
        public void ListenerTableFindsOwnSockets()
        {
            TcpListener v4 = new TcpListener(IPAddress.Loopback, 0);
            TcpListener v6 = Socket.OSSupportsIPv6 ? new TcpListener(IPAddress.IPv6Loopback, 0) : null;
            v4.Start();
            if (v6 != null) { v6.Start(); }
            try
            {
                int pid = Process.GetCurrentProcess().Id;
                Dictionary<int, HashSet<int>> listeners = TcpListeners.Snapshot();

                int port4 = ((IPEndPoint)v4.LocalEndpoint).Port;
                ClassicAssert.IsTrue(listeners.ContainsKey(port4) && listeners[port4].Contains(pid), "IPv4 listener on port " + port4);
                if (v6 != null)
                {
                    int port6 = ((IPEndPoint)v6.LocalEndpoint).Port;
                    ClassicAssert.IsTrue(listeners.ContainsKey(port6) && listeners[port6].Contains(pid), "IPv6 listener on port " + port6);
                }
            }
            finally
            {
                v4.Stop();
                if (v6 != null) { v6.Stop(); }
            }
        }
    }
}
