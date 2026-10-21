using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using log4net;
using Microsoft.Win32;
using SuperPutty.Data;

namespace SuperPutty.Utils
{
    /// <summary>
    /// Finds the SSH tunnels of a session (PuTTY profile + -L/-R/-D arguments) and checks on this machine whether
    /// their local ports are held by the tab's PuTTY process. The result is shown on the tab (e.g. "↔2", "↔2!")
    /// and in its tooltip.
    /// </summary>
    public static class TunnelMonitor
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(TunnelMonitor));

        /// <summary>PuTTY binds the ports only after login; until then a missing port is not reported as broken</summary>
        public static readonly TimeSpan StartupGrace = TimeSpan.FromSeconds(20);

        /// <summary>The PuTTY profile PuTTY loads for a session: its saved profile, otherwise "Default Settings"</summary>
        public static string ProfileName(SessionData session)
        {
            return string.IsNullOrEmpty(session.PuttySession) ? PuttyDataHelper.SessionDefaultSettings : session.PuttySession;
        }

        /// <summary>All tunnels of an SSH session; empty for other protocols or when the profile can't be read</summary>
        public static List<SshTunnel> ReadTunnels(SessionData session)
        {
            List<SshTunnel> tunnels = new List<SshTunnel>();
            if (session == null || (session.Proto != ConnectionProtocol.SSH && session.Proto != ConnectionProtocol.SSH2))
            {
                return tunnels;
            }
            try
            {
                tunnels.AddRange(SshTunnel.ParsePuttyForwardings(ReadProfileForwardings(ProfileName(session)), "PuTTY profile"));
            }
            catch (Exception ex) when (ex is System.Security.SecurityException || ex is UnauthorizedAccessException || ex is System.IO.IOException)
            {
                Log.Warn("Could not read port forwardings of profile " + ProfileName(session), ex);
            }
            tunnels.AddRange(SshTunnel.ParseArguments(session.ExtraArgs, "extra arguments"));
            return tunnels;
        }

        /// <summary>The "PortForwardings" value of a saved PuTTY/KiTTY profile (registry key names are URL-encoded)</summary>
        private static string ReadProfileForwardings(string profile)
        {
            using (RegistryKey sessions = PuttyDataHelper.RootAppKey)
            {
                if (sessions == null)
                {
                    return null;
                }
                string keyName = sessions.GetSubKeyNames().FirstOrDefault(name => HttpUtility.UrlDecode(name) == profile);
                if (keyName == null)
                {
                    return null;
                }
                using (RegistryKey key = sessions.OpenSubKey(keyName))
                {
                    return key == null ? null : key.GetValue("PortForwardings") as string;
                }
            }
        }

        /// <summary>
        /// State of one tunnel, given the listening ports and the tab's PuTTY process. A port held by another tab
        /// connected to the same host (a command tab, a duplicate) counts as up: it leads to the same place.
        /// A port held by a tab of a different host is a conflict.
        /// </summary>
        public static TunnelState StateOf(SshTunnel tunnel, int puttyProcessId, Dictionary<int, HashSet<int>> listeners,
            ICollection<int> sameHostTabProcessIds, bool inStartup)
        {
            if (!tunnel.IsLocal)
            {
                return TunnelState.NotCheckable;
            }
            HashSet<int> pids;
            if (listeners.TryGetValue(tunnel.ListenPort, out pids) && pids.Count > 0)
            {
                if (pids.Contains(puttyProcessId) || pids.Any(sameHostTabProcessIds.Contains))
                {
                    return TunnelState.Up;
                }
                return inStartup ? TunnelState.Unknown : TunnelState.Conflict;
            }
            return inStartup ? TunnelState.Unknown : TunnelState.Down;
        }

        /// <summary>True if two sessions connect to the same SSH server with the same PuTTY profile</summary>
        public static bool SameTarget(SessionData a, SessionData b)
        {
            return a != null && b != null
                && string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase)
                && a.Port == b.Port
                && ProfileName(a) == ProfileName(b);
        }

        /// <summary>Tab text suffix: " ↔N", with "!" if a local tunnel is down or its port is taken; empty without tunnels</summary>
        public static string TabSuffix(IList<TunnelState> states)
        {
            if (states.Count == 0)
            {
                return string.Empty;
            }
            bool broken = states.Any(s => s == TunnelState.Down || s == TunnelState.Conflict);
            return " ↔" + states.Count + (broken ? "!" : string.Empty);
        }

        /// <summary>Tooltip lines describing each tunnel and its state</summary>
        public static string ToolTip(IList<SshTunnel> tunnels, IList<TunnelState> states, IDictionary<int, string> portHolders)
        {
            if (tunnels.Count == 0)
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder("SSH tunnels:");
            for (int i = 0; i < tunnels.Count; i++)
            {
                sb.Append('\n').Append(tunnels[i].Describe()).Append("  —  ");
                switch (states[i])
                {
                    case TunnelState.Up: sb.Append("up"); break;
                    case TunnelState.Down: sb.Append("NOT listening"); break;
                    case TunnelState.Conflict:
                        string holder;
                        sb.Append("port in use by ").Append(portHolders != null && portHolders.TryGetValue(tunnels[i].ListenPort, out holder) ? holder : "another program");
                        break;
                    case TunnelState.NotCheckable: sb.Append("on the server (not checked)"); break;
                    default: sb.Append("connecting…"); break;
                }
            }
            return sb.ToString();
        }
    }
}
