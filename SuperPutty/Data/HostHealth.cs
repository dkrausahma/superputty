using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SuperPutty.Data
{
    public enum HealthLevel
    {
        /// <summary>Not checked, or the check failed</summary>
        Unknown,
        Ok,
        Warning,
        Critical
    }

    /// <summary>
    /// A host's health as reported by <see cref="ProbeCommand"/> (one plink call): load per CPU, disk use per mount,
    /// memory use, failed systemd units, unhealthy docker containers
    /// </summary>
    public class HostHealth
    {
        /// <summary>
        /// POSIX shell command printing key=value lines. Missing tools (systemctl, docker, free) just leave their
        /// values out or at 0.
        /// </summary>
        public const string ProbeCommand =
            "echo load=$(cut -d' ' -f1 /proc/loadavg); " +
            "echo cpus=$(nproc 2>/dev/null || grep -c ^processor /proc/cpuinfo); " +
            "(df -P -x tmpfs -x devtmpfs -x overlay -x squashfs -x iso9660 -x udf 2>/dev/null || df -P) | awk 'NR>1{print \"disk=\"$6\"=\"$5}'; " +
            "free -m 2>/dev/null | awk '/^Mem:/{if($2>0) print \"mem=\"int(($2-$7)*100/$2)}'; " +
            // one line per failed unit name; counting output lines instead gave false alarms on systemd versions
            // that print a footer or blank line even with --no-legend
            "systemctl list-units --state=failed --no-legend --plain 2>/dev/null | awk '$1 ~ /\\./ {print \"failedunit=\"$1}'; " +
            "echo unhealthy=$(docker ps -q --filter health=unhealthy 2>/dev/null | wc -l)";

        public double? Load { get; set; }
        public int Cpus { get; set; } = 1;
        /// <summary>Used percentage per mount point</summary>
        public Dictionary<string, int> Disks { get; } = new Dictionary<string, int>();
        public int? MemoryPercent { get; set; }
        /// <summary>Names of the failed systemd units, e.g. "nvidia-fabricmanager.service"</summary>
        public List<string> FailedUnitNames { get; } = new List<string>();

        public int FailedUnits { get { return this.FailedUnitNames.Count; } }
        public int UnhealthyContainers { get; set; }

        /// <summary>Why the check failed; null on success</summary>
        public string Error { get; set; }
        public DateTime CheckedUtc { get; set; } = DateTime.UtcNow;

        public static HostHealth Failed(string error)
        {
            return new HostHealth { Error = error };
        }

        /// <summary>Read the probe's output</summary>
        public static HostHealth Parse(string output)
        {
            HostHealth health = new HostHealth();
            foreach (string raw in (output ?? string.Empty).Split('\n'))
            {
                string line = raw.Trim();
                int equals = line.IndexOf('=');
                if (equals <= 0)
                {
                    continue;
                }
                string key = line.Substring(0, equals);
                string value = line.Substring(equals + 1).Trim();
                int number;
                double load;
                switch (key)
                {
                    case "load":
                        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out load)) { health.Load = load; }
                        break;
                    case "cpus":
                        if (int.TryParse(value, out number) && number > 0) { health.Cpus = number; }
                        break;
                    case "mem":
                        if (int.TryParse(value, out number)) { health.MemoryPercent = number; }
                        break;
                    case "failedunit":
                        if (value.Length > 0 && !health.FailedUnitNames.Contains(value)) { health.FailedUnitNames.Add(value); }
                        break;
                    case "unhealthy":
                        if (int.TryParse(value, out number)) { health.UnhealthyContainers = number; }
                        break;
                    case "disk":
                        // disk=<mount>=<used>%
                        int split = value.LastIndexOf('=');
                        if (split > 0 && int.TryParse(value.Substring(split + 1).TrimEnd('%'), out number))
                        {
                            health.Disks[value.Substring(0, split)] = number;
                        }
                        break;
                }
            }
            if (health.Load == null && health.Disks.Count == 0 && health.MemoryPercent == null)
            {
                health.Error = "No health data in the answer";
            }
            return health;
        }

        /// <summary>The problems found, most severe first, as (level, text)</summary>
        public List<KeyValuePair<HealthLevel, string>> Problems()
        {
            List<KeyValuePair<HealthLevel, string>> problems = new List<KeyValuePair<HealthLevel, string>>();
            if (this.Error != null)
            {
                return problems;
            }
            if (this.UnhealthyContainers > 0)
            {
                problems.Add(Problem(HealthLevel.Critical, "{0} unhealthy container(s)", this.UnhealthyContainers));
            }
            foreach (KeyValuePair<string, int> disk in this.Disks.OrderByDescending(d => d.Value))
            {
                if (disk.Value >= 90) { problems.Add(Problem(HealthLevel.Critical, "disk {0} {1}% full", disk.Key, disk.Value)); }
                else if (disk.Value >= 80) { problems.Add(Problem(HealthLevel.Warning, "disk {0} {1}% full", disk.Key, disk.Value)); }
            }
            if (this.MemoryPercent >= 95) { problems.Add(Problem(HealthLevel.Critical, "memory {0}% used", this.MemoryPercent)); }
            else if (this.MemoryPercent >= 85) { problems.Add(Problem(HealthLevel.Warning, "memory {0}% used", this.MemoryPercent)); }
            if (this.Load != null)
            {
                double perCpu = this.Load.Value / this.Cpus;
                if (perCpu >= 2) { problems.Add(Problem(HealthLevel.Critical, "load {0} on {1} CPU(s)", this.Load.Value.ToString("0.0", CultureInfo.InvariantCulture), this.Cpus)); }
                else if (perCpu >= 1) { problems.Add(Problem(HealthLevel.Warning, "load {0} on {1} CPU(s)", this.Load.Value.ToString("0.0", CultureInfo.InvariantCulture), this.Cpus)); }
            }
            if (this.FailedUnits > 0)
            {
                problems.Add(Problem(HealthLevel.Warning, "{0} failed systemd unit(s): {1}", this.FailedUnits, string.Join(", ", this.FailedUnitNames)));
            }
            return problems.OrderByDescending(p => p.Key).ToList();
        }

        private static KeyValuePair<HealthLevel, string> Problem(HealthLevel level, string format, params object[] args)
        {
            return new KeyValuePair<HealthLevel, string>(level, string.Format(CultureInfo.InvariantCulture, format, args));
        }

        public HealthLevel Level
        {
            get
            {
                if (this.Error != null)
                {
                    return HealthLevel.Unknown;
                }
                List<KeyValuePair<HealthLevel, string>> problems = Problems();
                return problems.Count == 0 ? HealthLevel.Ok : problems[0].Key;
            }
        }

        /// <summary>Multi-line summary for tooltips</summary>
        public string Describe()
        {
            StringBuilder sb = new StringBuilder();
            string time = this.CheckedUtc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
            if (this.Error != null)
            {
                return string.Format("Health check failed ({0}): {1}", time, this.Error);
            }
            sb.AppendFormat("Health ({0}): {1}", time, this.Level == HealthLevel.Ok ? "OK" : this.Level.ToString().ToUpperInvariant());
            foreach (KeyValuePair<HealthLevel, string> problem in Problems())
            {
                sb.Append("\n  ! ").Append(problem.Value);
            }
            if (this.Load != null) { sb.AppendFormat(CultureInfo.InvariantCulture, "\n  load {0:0.00} / {1} CPU(s)", this.Load, this.Cpus); }
            if (this.MemoryPercent != null) { sb.AppendFormat("\n  memory {0}% used", this.MemoryPercent); }
            foreach (KeyValuePair<string, int> disk in this.Disks.OrderBy(d => d.Key))
            {
                sb.AppendFormat("\n  disk {0} {1}%", disk.Key, disk.Value);
            }
            return sb.ToString();
        }
    }
}
