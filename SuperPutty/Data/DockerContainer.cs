using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SuperPutty.Data
{
    /// <summary>A container as listed by <c>docker ps -a</c> on a session's host</summary>
    public class DockerContainer
    {
        /// <summary>Container names as docker allows them; anything else is refused before it reaches a shell</summary>
        private static readonly Regex ValidName = new Regex(@"^[a-zA-Z0-9][a-zA-Z0-9_.-]*$", RegexOptions.Compiled);

        /// <summary>Tab-separated fields read by <see cref="ParseList"/>: name, image, status, compose project</summary>
        public const string ListFormat = "{{.Names}}\\t{{.Image}}\\t{{.Status}}\\t{{.Label \"com.docker.compose.project\"}}";

        public string Name { get; set; }
        public string Image { get; set; }
        /// <summary>Docker's status text, e.g. "Up 3 hours (healthy)" or "Exited (0) 2 days ago"</summary>
        public string Status { get; set; }
        /// <summary>Compose project, or empty for containers not started by compose</summary>
        public string Project { get; set; }

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
                    Project = fields.Length > 3 ? fields[3] : string.Empty
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
