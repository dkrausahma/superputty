using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using log4net;
using SuperPutty.Data;

namespace SuperPutty.Utils
{
    /// <summary>
    /// Opens a container's published port in the browser: directly at host:port when this PC reaches it,
    /// otherwise (port bound to the host's loopback, firewall, jump host) through an SSH tunnel started with plink
    /// to a free local port. Tunnels are reused per host and port and closed when SuperPuTTY exits.
    /// </summary>
    public static class PortOpener
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PortOpener));

        private static readonly TimeSpan DirectTimeout = TimeSpan.FromMilliseconds(1500);
        private static readonly TimeSpan TunnelTimeout = TimeSpan.FromSeconds(15);

        private class Tunnel
        {
            public Process Process { get; set; }
            public int LocalPort { get; set; }
            public StringBuilder Errors { get; } = new StringBuilder();
        }

        /// <summary>Open tunnels by "SessionId|target:port"</summary>
        private static readonly Dictionary<string, Tunnel> Tunnels = new Dictionary<string, Tunnel>();
        private static readonly object Sync = new object();

        /// <summary>
        /// Open the port in the browser; returns a status text for the user. Runs the network work in the
        /// background; await it from the UI thread.
        /// </summary>
        public static async Task<string> OpenAsync(SessionData session, PublishedPort port)
        {
            if (!port.CanOpen)
            {
                return "Only TCP ports can be opened in a browser.";
            }
            string host = session.Host;
            if (!port.NeedsTunnel && !string.IsNullOrEmpty(host))
            {
                string address = port.HostAddress ?? host;
                if (await CanConnectAsync(address, port.HostPort).ConfigureAwait(true))
                {
                    string url = port.Url(address, port.HostPort);
                    Browse(url);
                    return "Opened " + url;
                }
            }

            int localPort = await Task.Run(() => EnsureTunnel(session, port)).ConfigureAwait(true);
            string local = port.Url("localhost", localPort);
            Browse(local);
            return string.Format("Opened {0} (SSH tunnel to {1}:{2} on {3}, open until SuperPuTTY closes)", local, port.TunnelTarget, port.HostPort, session.SessionName);
        }

        private static async Task<bool> CanConnectAsync(string host, int port)
        {
            using (TcpClient client = new TcpClient())
            {
                try
                {
                    Task connect = client.ConnectAsync(host, port);
                    Task finished = await Task.WhenAny(connect, Task.Delay(DirectTimeout)).ConfigureAwait(false);
                    if (finished == connect && !connect.IsFaulted && client.Connected)
                    {
                        return true;
                    }
                    if (connect.IsFaulted)
                    {
                        Log.DebugFormat("{0}:{1} not reachable directly: {2}", host, port, connect.Exception.InnerException.Message);
                    }
                }
                catch (Exception ex) when (ex is SocketException || ex is ArgumentException)
                {
                    Log.DebugFormat("{0}:{1} not reachable directly: {2}", host, port, ex.Message);
                }
                return false;
            }
        }

        /// <summary>The local port of a working tunnel to the port, started if needed (blocks until it listens)</summary>
        private static int EnsureTunnel(SessionData session, PublishedPort port)
        {
            string key = session.SessionId + "|" + port.TunnelTarget + ":" + port.HostPort;
            lock (Sync)
            {
                Tunnel existing;
                if (Tunnels.TryGetValue(key, out existing))
                {
                    if (!existing.Process.HasExited)
                    {
                        return existing.LocalPort;
                    }
                    Tunnels.Remove(key);
                    existing.Process.Dispose();
                }
            }

            Tunnel tunnel = new Tunnel { LocalPort = FreeLocalPort() };
            tunnel.Process = Plink.StartTunnel(session, tunnel.LocalPort, port.TunnelTarget, port.HostPort);
            tunnel.Process.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (tunnel.Errors) tunnel.Errors.AppendLine(e.Data); };
            tunnel.Process.OutputDataReceived += delegate { };
            tunnel.Process.BeginErrorReadLine();
            tunnel.Process.BeginOutputReadLine();

            // plink listens once logged in; give up when it exits (login failed) or takes too long
            Stopwatch watch = Stopwatch.StartNew();
            while (watch.Elapsed < TunnelTimeout)
            {
                if (tunnel.Process.HasExited)
                {
                    string errors;
                    lock (tunnel.Errors) { errors = tunnel.Errors.ToString().Trim(); }
                    tunnel.Process.Dispose();
                    throw new InvalidOperationException("The SSH tunnel could not be opened: " + (errors.Length > 0 ? FirstLine(errors) : "plink exited"));
                }
                if (IsListening(tunnel.LocalPort))
                {
                    lock (Sync)
                    {
                        Tunnels[key] = tunnel;
                    }
                    return tunnel.LocalPort;
                }
                System.Threading.Thread.Sleep(250);
            }
            Kill(tunnel.Process);
            throw new InvalidOperationException("The SSH tunnel did not open within " + TunnelTimeout.TotalSeconds + " s.");
        }

        private static int FreeLocalPort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        private static bool IsListening(int port)
        {
            using (TcpClient client = new TcpClient())
            {
                try
                {
                    return client.ConnectAsync(IPAddress.Loopback, port).Wait(200) && client.Connected;
                }
                catch (AggregateException)
                {
                    return false;
                }
            }
        }

        private static void Browse(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private static string FirstLine(string text)
        {
            int newline = text.IndexOf('\n');
            return (newline < 0 ? text : text.Substring(0, newline)).Trim();
        }

        private static void Kill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
            {
                Log.Debug("Could not stop a tunnel", ex);
            }
            finally
            {
                process.Dispose();
            }
        }

        /// <summary>Close all tunnels (on exit)</summary>
        public static void CloseAll()
        {
            List<Tunnel> open;
            lock (Sync)
            {
                open = new List<Tunnel>(Tunnels.Values);
                Tunnels.Clear();
            }
            foreach (Tunnel tunnel in open)
            {
                Kill(tunnel.Process);
            }
        }
    }
}
