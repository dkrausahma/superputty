using System;
using System.Collections.Generic;
using System.Globalization;

namespace SuperPutty.Data
{
    /// <summary>Kind of SSH port forwarding</summary>
    public enum TunnelKind
    {
        /// <summary>-L: a local port forwarded to a destination reachable from the server</summary>
        Local,
        /// <summary>-R: a port on the server forwarded to a destination reachable from this machine</summary>
        Remote,
        /// <summary>-D: a local SOCKS proxy</summary>
        Dynamic
    }

    /// <summary>State of a tunnel's listening port on this machine</summary>
    public enum TunnelState
    {
        /// <summary>Not checked yet, or the session is still connecting</summary>
        Unknown,
        /// <summary>The PuTTY process of the tab listens on the port</summary>
        Up,
        /// <summary>Nobody listens on the port</summary>
        Down,
        /// <summary>Another process holds the port, so PuTTY couldn't bind it</summary>
        Conflict,
        /// <summary>Remote forwardings listen on the server and can't be checked here</summary>
        NotCheckable
    }

    /// <summary>An SSH port forwarding of a session, from its PuTTY profile or its -L/-R/-D arguments</summary>
    public class SshTunnel
    {
        public TunnelKind Kind { get; set; }
        /// <summary>Listen address if one was given (e.g. 127.0.0.1), otherwise null</summary>
        public string BindAddress { get; set; }
        public int ListenPort { get; set; }
        /// <summary>host:port the connection is forwarded to; null for dynamic (SOCKS) forwardings</summary>
        public string Destination { get; set; }
        /// <summary>Where the tunnel is defined, e.g. "PuTTY profile" or "extra arguments"</summary>
        public string Origin { get; set; }

        /// <summary>True for tunnels whose port listens on this machine and can be checked</summary>
        public bool IsLocal { get { return this.Kind != TunnelKind.Remote; } }

        /// <summary>Short description such as "L 5432 → db:5432" or "D 1080 (SOCKS)"</summary>
        public string Describe()
        {
            string listen = (this.BindAddress == null ? string.Empty : this.BindAddress + ":") + this.ListenPort.ToString(CultureInfo.InvariantCulture);
            switch (this.Kind)
            {
                case TunnelKind.Dynamic: return "D " + listen + " (SOCKS)";
                case TunnelKind.Remote: return "R " + listen + " → " + this.Destination;
                default: return "L " + listen + " → " + this.Destination;
            }
        }

        public override string ToString()
        {
            return Describe();
        }

        /// <summary>
        /// Parse PuTTY's saved "PortForwardings" value: comma-separated entries such as "L5432=localhost:5432",
        /// "R8080=localhost:80", "D1080=" or "4L127.0.0.1:5432=db:5432" (optional 4/6 address-family prefix)
        /// </summary>
        public static List<SshTunnel> ParsePuttyForwardings(string value, string origin)
        {
            List<SshTunnel> tunnels = new List<SshTunnel>();
            foreach (string raw in (value ?? string.Empty).Split(','))
            {
                string entry = raw.Trim();
                if (entry.Length > 0 && (entry[0] == '4' || entry[0] == '6'))
                {
                    entry = entry.Substring(1);
                }
                if (entry.Length < 2)
                {
                    continue;
                }

                TunnelKind kind;
                if (!TryKind(entry[0], out kind))
                {
                    continue;
                }
                string listen = entry.Substring(1);
                string destination = null;
                int equals = listen.IndexOf('=');
                if (equals >= 0)
                {
                    destination = listen.Substring(equals + 1).Trim();
                    listen = listen.Substring(0, equals);
                }

                SshTunnel tunnel = Create(kind, listen, destination, origin);
                if (tunnel != null)
                {
                    tunnels.Add(tunnel);
                }
            }
            return tunnels;
        }

        /// <summary>
        /// Parse -L/-R/-D options from PuTTY command-line arguments, e.g. "-L 5432:localhost:5432",
        /// "-L127.0.0.1:8080:web:80" or "-D 1080"
        /// </summary>
        public static List<SshTunnel> ParseArguments(string arguments, string origin)
        {
            List<SshTunnel> tunnels = new List<SshTunnel>();
            string[] tokens = (arguments ?? string.Empty).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                TunnelKind kind;
                if (token.Length < 2 || token[0] != '-' || !TryKind(token[1], out kind))
                {
                    continue;
                }
                string spec = token.Length > 2 ? token.Substring(2) : (i + 1 < tokens.Length ? tokens[++i] : null);
                if (spec == null)
                {
                    continue;
                }

                SshTunnel tunnel;
                if (kind == TunnelKind.Dynamic)
                {
                    tunnel = Create(kind, spec, null, origin);
                }
                else
                {
                    // [bind:]port:host:hostport -> listen part is everything before the last two fields
                    string[] parts = spec.Split(':');
                    if (parts.Length < 3)
                    {
                        continue;
                    }
                    string destination = parts[parts.Length - 2] + ":" + parts[parts.Length - 1];
                    string listen = string.Join(":", parts, 0, parts.Length - 2);
                    tunnel = Create(kind, listen, destination, origin);
                }
                if (tunnel != null)
                {
                    tunnels.Add(tunnel);
                }
            }
            return tunnels;
        }

        private static bool TryKind(char c, out TunnelKind kind)
        {
            switch (char.ToUpperInvariant(c))
            {
                case 'L': kind = TunnelKind.Local; return true;
                case 'R': kind = TunnelKind.Remote; return true;
                case 'D': kind = TunnelKind.Dynamic; return true;
                default: kind = TunnelKind.Local; return false;
            }
        }

        /// <summary>Build a tunnel from "[bind:]port" and a destination; null if the port isn't valid</summary>
        private static SshTunnel Create(TunnelKind kind, string listen, string destination, string origin)
        {
            string bind = null;
            string port = listen.Trim();
            int colon = port.LastIndexOf(':');
            if (colon >= 0)
            {
                bind = port.Substring(0, colon);
                port = port.Substring(colon + 1);
            }
            int listenPort;
            if (!int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out listenPort) || listenPort < 1 || listenPort > 65535)
            {
                return null;
            }
            if (kind != TunnelKind.Dynamic && string.IsNullOrEmpty(destination))
            {
                return null;
            }
            return new SshTunnel
            {
                Kind = kind,
                BindAddress = string.IsNullOrEmpty(bind) ? null : bind,
                ListenPort = listenPort,
                Destination = kind == TunnelKind.Dynamic ? null : destination,
                Origin = origin
            };
        }
    }
}
