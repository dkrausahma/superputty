using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SuperPutty.Data
{
    /// <summary>What a container's status dot shows, derived from docker's status text</summary>
    public enum ContainerState
    {
        /// <summary>Up, without a health check</summary>
        Running,
        /// <summary>Up and its health check passes</summary>
        Healthy,
        /// <summary>Up, health check not decided yet</summary>
        Starting,
        Unhealthy,
        Paused,
        Restarting,
        /// <summary>Stopped normally (exit code 0, or the signals docker stop and Ctrl+C leave behind)</summary>
        Exited,
        /// <summary>Exited with an error code, or dead</summary>
        Failed,
        /// <summary>Created, never started</summary>
        Created,
        Other
    }

    /// <summary>A container as listed by <c>docker ps -a</c> on a session's host</summary>
    public class DockerContainer
    {
        /// <summary>Container names as docker allows them; anything else is refused before it reaches a shell</summary>
        private static readonly Regex ValidName = new Regex(@"^[a-zA-Z0-9][a-zA-Z0-9_.-]*$", RegexOptions.Compiled);

        /// <summary>Tab-separated fields read by <see cref="ParseList"/>: name, image, status, compose project, ports</summary>
        public const string ListFormat = "{{.Names}}\\t{{.Image}}\\t{{.Status}}\\t{{.Label \"com.docker.compose.project\"}}\\t{{.Ports}}";

        /// <summary>One published port as docker lists it: "0.0.0.0:9089->8080/tcp", ":::9089->8080/tcp", "127.0.0.1:80-81->80-81/udp"</summary>
        private static readonly Regex PublishedPort = new Regex(@"^(?<ip>.*):(?<host>\d+(?:-\d+)?)->(?<container>\d+(?:-\d+)?)/(?<proto>\w+)$", RegexOptions.Compiled);

        /// <summary>The health or pause marker docker appends to "Up …", e.g. "(healthy)", "(health: starting)"</summary>
        private static readonly Regex StatusMarker = new Regex(@"\s*\((?<marker>healthy|unhealthy|health: starting|paused)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ExitCode = new Regex(@"^Exited \((?<code>-?\d+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public string Name { get; set; }
        public string Image { get; set; }
        /// <summary>Docker's status text, e.g. "Up 3 hours (healthy)" or "Exited (0) 2 days ago"</summary>
        public string Status { get; set; }
        /// <summary>Compose project, or empty for containers not started by compose</summary>
        public string Project { get; set; }
        /// <summary>Docker's ports text, e.g. "0.0.0.0:9089->8080/tcp, :::9089->8080/tcp"; empty if none</summary>
        public string Ports { get; set; }

        public ContainerState State
        {
            get
            {
                string status = this.Status ?? string.Empty;
                if (status.StartsWith("Up", StringComparison.OrdinalIgnoreCase))
                {
                    Match marker = StatusMarker.Match(status);
                    switch (marker.Success ? marker.Groups["marker"].Value.ToLowerInvariant() : string.Empty)
                    {
                        case "healthy": return ContainerState.Healthy;
                        case "unhealthy": return ContainerState.Unhealthy;
                        case "health: starting": return ContainerState.Starting;
                        case "paused": return ContainerState.Paused;
                        default: return ContainerState.Running;
                    }
                }
                if (status.StartsWith("Restarting", StringComparison.OrdinalIgnoreCase))
                {
                    return ContainerState.Restarting;
                }
                Match exit = ExitCode.Match(status);
                if (exit.Success)
                {
                    // 130/137/143: Ctrl+C, SIGKILL and SIGTERM, i.e. what docker stop leaves behind
                    string code = exit.Groups["code"].Value;
                    return code == "0" || code == "130" || code == "137" || code == "143" ? ContainerState.Exited : ContainerState.Failed;
                }
                if (status.StartsWith("Dead", StringComparison.OrdinalIgnoreCase))
                {
                    return ContainerState.Failed;
                }
                if (status.StartsWith("Created", StringComparison.OrdinalIgnoreCase))
                {
                    return ContainerState.Created;
                }
                return ContainerState.Other;
            }
        }

        /// <summary>The status for display: "Up 14 hours · healthy" instead of "Up 14 hours (healthy)"</summary>
        public string DisplayStatus
        {
            get
            {
                return StatusMarker.Replace(this.Status ?? string.Empty, match =>
                {
                    string marker = match.Groups["marker"].Value.ToLowerInvariant();
                    return " · " + (marker == "health: starting" ? "starting" : marker);
                });
            }
        }

        /// <summary>The name without its compose project prefix ("verum-api" in project "verum" -> "api")</summary>
        public string ShortName
        {
            get
            {
                string name = this.Name ?? string.Empty;
                string project = this.Project ?? string.Empty;
                if (project.Length > 0 && name.Length > project.Length + 1
                    && name.StartsWith(project, StringComparison.OrdinalIgnoreCase)
                    && (name[project.Length] == '-' || name[project.Length] == '_'))
                {
                    return name.Substring(project.Length + 1);
                }
                return name;
            }
        }

        /// <summary>The published ports, e.g. "9089→8080"</summary>
        public List<string> PublishedPorts
        {
            get { return ParsePorts(this.Ports); }
        }

        /// <summary>
        /// Published ports from docker's ports text as "host→container", once even when docker lists IPv4 and IPv6;
        /// the address only when bound to a specific one, the protocol only when it isn't tcp. Exposed but
        /// unpublished ports ("8080/tcp") are left out.
        /// </summary>
        public static List<string> ParsePorts(string ports)
        {
            List<string> result = new List<string>();
            foreach (string part in (ports ?? string.Empty).Split(','))
            {
                Match match = PublishedPort.Match(part.Trim());
                if (!match.Success)
                {
                    continue;
                }
                string ip = match.Groups["ip"].Value;
                bool anyAddress = ip.Length == 0 || ip == "0.0.0.0" || ip == "::" || ip == "[::]";
                string proto = match.Groups["proto"].Value;
                string text = (anyAddress ? string.Empty : ip + ":") + match.Groups["host"].Value + "→" + match.Groups["container"].Value
                    + (proto.Equals("tcp", StringComparison.OrdinalIgnoreCase) ? string.Empty : "/" + proto);
                if (!result.Contains(text))
                {
                    result.Add(text);
                }
            }
            return result;
        }

        public bool IsRunning
        {
            get { return this.Status != null && this.Status.StartsWith("Up", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsUnhealthy
        {
            get { return this.Status != null && this.Status.IndexOf("(unhealthy)", StringComparison.OrdinalIgnoreCase) >= 0; }
        }

        public static bool IsValidName(string name)
        {
            return name != null && ValidName.IsMatch(name);
        }

        /// <summary>Parse the output of <c>docker ps -a --format ListFormat</c>; skips lines it can't read</summary>
        public static List<DockerContainer> ParseList(string output)
        {
            List<DockerContainer> containers = new List<DockerContainer>();
            foreach (string line in (output ?? string.Empty).Split('\n'))
            {
                string[] fields = line.TrimEnd('\r').Split('\t');
                if (fields.Length < 3 || !IsValidName(fields[0]))
                {
                    continue;
                }
                containers.Add(new DockerContainer
                {
                    Name = fields[0],
                    Image = fields[1],
                    Status = fields[2],
                    Project = fields.Length > 3 ? fields[3] : string.Empty,
                    Ports = fields.Length > 4 ? fields[4] : string.Empty
                });
            }
            return containers;
        }

        public override string ToString()
        {
            return this.Name;
        }
    }

    /// <summary>
    /// The docker command lines run on a host. <c>Prefix</c> is empty or "sudo -n " / "sudo " for users without
    /// docker group membership. Container names must pass <see cref="DockerContainer.IsValidName"/>.
    /// </summary>
    public static class DockerCommands
    {
        /// <summary>List all containers</summary>
        public static string List(string prefix)
        {
            return prefix + "docker ps -a --format '" + DockerContainer.ListFormat + "'";
        }

        /// <summary>Interactive shell inside the container: bash if available, otherwise sh</summary>
        public static string Shell(string prefix, string name, bool asRoot)
        {
            return string.Format("{0}docker exec -it {1}{2} sh -c 'command -v bash >/dev/null 2>&1 && exec bash || exec sh'",
                prefix, asRoot ? "-u 0 " : string.Empty, Checked(name));
        }

        /// <summary>Follow the container's log, starting with the last 200 lines</summary>
        public static string FollowLogs(string prefix, string name)
        {
            return prefix + "docker logs -f --tail 200 " + Checked(name);
        }

        /// <summary>A lifecycle action such as start, stop or restart</summary>
        public static string Action(string prefix, string verb, string name)
        {
            if (verb != "start" && verb != "stop" && verb != "restart")
            {
                throw new ArgumentException("Unsupported docker action: " + verb, nameof(verb));
            }
            return prefix + "docker " + verb + " " + Checked(name);
        }

        /// <summary>The container's configuration, paged</summary>
        public static string Inspect(string prefix, string name)
        {
            return prefix + "docker inspect " + Checked(name) + " | less";
        }

        /// <summary>True if docker refused because the user may not access the docker daemon</summary>
        public static bool IsPermissionDenied(string error)
        {
            return error != null && error.IndexOf("permission denied", StringComparison.OrdinalIgnoreCase) >= 0
                && error.IndexOf("docker", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Checked(string name)
        {
            if (!DockerContainer.IsValidName(name))
            {
                throw new ArgumentException("Invalid container name: " + name, nameof(name));
            }
            return name;
        }
    }
}
