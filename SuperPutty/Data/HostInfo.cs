using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SuperPutty.Data
{
    /// <summary>One address of the host: interface name and address</summary>
    public class HostAddress
    {
        public string Interface { get; set; }
        public IPAddress Address { get; set; }
        public bool IsPublic { get; set; }

        public override string ToString()
        {
            return this.Address == null ? string.Empty : this.Address.ToString();
        }
    }

    public class DiskUsage
    {
        /// <summary>The filesystem as df names it, e.g. "/dev/sda1"</summary>
        public string Device { get; set; }
        public string Mount { get; set; }
        public long SizeKb { get; set; }
        public long UsedKb { get; set; }
        public long AvailableKb { get; set; }

        /// <summary>Used share as df shows it: used / (used + available)</summary>
        public int Percent
        {
            get { return this.UsedKb + this.AvailableKb <= 0 ? 0 : (int)Math.Ceiling(this.UsedKb * 100.0 / (this.UsedKb + this.AvailableKb)); }
        }
    }

    public class LoggedInUser
    {
        public string User { get; set; }
        public string Terminal { get; set; }
        /// <summary>Where the login came from (IP or host name); empty for local logins</summary>
        public string From { get; set; }
        /// <summary>Login time as who prints it</summary>
        public string Since { get; set; }
    }

    /// <summary>One NVIDIA GPU as nvidia-smi reports it</summary>
    public class GpuInfo
    {
        public int Index { get; set; }
        public string Uuid { get; set; }
        public string Name { get; set; }
        /// <summary>Busy share in percent; null when the driver doesn't report it</summary>
        public int? UtilizationPercent { get; set; }
        public long MemoryUsedMb { get; set; }
        public long MemoryTotalMb { get; set; }
        public int? TemperatureC { get; set; }

        public int MemoryPercent
        {
            get { return this.MemoryTotalMb <= 0 ? 0 : (int)(this.MemoryUsedMb * 100 / this.MemoryTotalMb); }
        }
    }

    /// <summary>A process holding GPU memory</summary>
    public class GpuProcess
    {
        public int GpuIndex { get; set; }
        public string Pid { get; set; }
        /// <summary>The program's file name, without its path</summary>
        public string Name { get; set; }
        public long MemoryMb { get; set; }
    }

    public class ProcessUsage
    {
        public string Name { get; set; }
        /// <summary>CPU use in percent of one CPU, like top (can exceed 100 on several CPUs)</summary>
        public double CpuPercent { get; set; }
    }

    /// <summary>
    /// A snapshot of a host for the Host Info pane, built by <see cref="HostInfoReader"/> from the output of
    /// <see cref="Script"/>: static facts (name, OS, addresses) once, CPU/memory/processes every
    /// <see cref="FastSeconds"/> s, disks/users/failed units/unhealthy containers every <see cref="SlowEvery"/> rounds.
    /// </summary>
    public class HostInfo
    {
        public const int FastSeconds = 5;
        public const int SlowEvery = 12;

        /// <summary>Interfaces whose addresses aren't the host's own (container and VM bridges)</summary>
        private static readonly string[] BridgePrefixes = { "docker", "br-", "veth", "virbr", "cni", "flannel", "cali", "lxc", "lxd", "vxlan", "kube", "weave", "podman" };

        /// <summary>
        /// POSIX shell loop printing key=value lines, each round closed by "--end". It runs until the connection
        /// closes (writing to the closed pipe ends it). Missing tools just leave their values out.
        /// </summary>
        public static readonly string Script =
            "r=0; while :; do " +
            "if [ $r -eq 0 ]; then " +
                "echo host=$(hostname -f 2>/dev/null || hostname); " +
                "echo os=$(sed -n 's/^PRETTY_NAME=//p' /etc/os-release 2>/dev/null | tr -d '\"'); " +
                "echo kernel=$(uname -r); " +
                "echo cpus=$(nproc 2>/dev/null || grep -c ^processor /proc/cpuinfo); " +
                "ip -o addr show 2>/dev/null | awk '{print \"addr=\"$2\"=\"$4}'; " +
            "fi; " +
            "if [ $((r % " + SlowEvery + ")) -eq 0 ]; then " +
                "(df -P -k -x tmpfs -x devtmpfs -x overlay -x squashfs -x iso9660 -x udf 2>/dev/null || df -P -k) | awk 'NR>1{print \"disk=\"$1\"=\"$6\"=\"$2\"=\"$3\"=\"$4}'; " +
                "who 2>/dev/null | awk '{print \"user=\"$0}'; " +
                "systemctl list-units --state=failed --no-legend --plain 2>/dev/null | awk '$1 ~ /\\./ {print \"failedunit=\"$1}'; " +
                "echo unhealthy=$(docker ps -q --filter health=unhealthy 2>/dev/null | wc -l); " +
                "echo slow=1; " +
            "fi; " +
            // NVIDIA GPUs, only where nvidia-smi exists: one line per GPU and per process holding GPU memory
            "if command -v nvidia-smi >/dev/null 2>&1; then " +
                "nvidia-smi --query-gpu=index,uuid,name,utilization.gpu,memory.used,memory.total,temperature.gpu --format=csv,noheader,nounits 2>/dev/null | awk '{print \"gpu=\"$0}'; " +
                "nvidia-smi --query-compute-apps=gpu_uuid,pid,process_name,used_memory --format=csv,noheader,nounits 2>/dev/null | awk '{print \"gpuapp=\"$0}'; " +
            "fi; " +
            "echo uptime=$(cut -d' ' -f1 /proc/uptime); " +
            "echo loadavg=$(cat /proc/loadavg); " +
            "echo cpustat=$(head -1 /proc/stat); " +
            "awk '/^(MemTotal|MemAvailable|SwapTotal|SwapFree):/{sub(\":\",\"\",$1); print \"meminfo=\"$1\"=\"$2}' /proc/meminfo; " +
            // pid, utime+stime and name of every process; the name may contain spaces and ")", so cut at the last ")"
            "cat /proc/[0-9]*/stat 2>/dev/null | awk '{s=$0; p=index(s,\"(\"); for(i=length(s);i>p;i--) if(substr(s,i,1)==\")\") break; " +
                "n=split(substr(s,i+2),f,\" \"); if(n>=13) print \"proc=\"$1\"=\"(f[12]+f[13])\"=\"substr(s,p+1,i-p-1)}'; " +
            "echo --end; r=$((r+1)); sleep " + FastSeconds + "; done";

        public string HostName { get; set; }
        public string Os { get; set; }
        public string Kernel { get; set; }
        public int Cpus { get; set; } = 1;
        public List<HostAddress> Addresses { get; set; } = new List<HostAddress>();

        public TimeSpan? Uptime { get; set; }
        /// <summary>Load average over 1, 5 and 15 minutes; null until read</summary>
        public double[] Load { get; set; }
        public int? Processes { get; set; }
        /// <summary>CPU use of the whole host in percent; null for the first round (needs two samples)</summary>
        public double? CpuPercent { get; set; }
        /// <summary>The busiest processes since the last round, most CPU first</summary>
        public List<ProcessUsage> TopProcesses { get; set; } = new List<ProcessUsage>();

        public long MemTotalKb { get; set; }
        public long MemAvailableKb { get; set; }
        public long SwapTotalKb { get; set; }
        public long SwapFreeKb { get; set; }

        public List<DiskUsage> Disks { get; set; } = new List<DiskUsage>();
        /// <summary>NVIDIA GPUs; empty on hosts without nvidia-smi</summary>
        public List<GpuInfo> Gpus { get; set; } = new List<GpuInfo>();
        /// <summary>Processes holding GPU memory, most memory first</summary>
        public List<GpuProcess> GpuProcesses { get; set; } = new List<GpuProcess>();
        public List<LoggedInUser> Users { get; set; } = new List<LoggedInUser>();
        public List<string> FailedUnits { get; set; } = new List<string>();
        public int UnhealthyContainers { get; set; }
        /// <summary>Disks, users, units and containers have been read at least once</summary>
        public bool HasSlowData { get; set; }

        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

        public long MemUsedKb { get { return Math.Max(0, this.MemTotalKb - this.MemAvailableKb); } }
        public long SwapUsedKb { get { return Math.Max(0, this.SwapTotalKb - this.SwapFreeKb); } }

        public int? MemoryPercent
        {
            get { return this.MemTotalKb <= 0 ? (int?)null : (int)(this.MemUsedKb * 100 / this.MemTotalKb); }
        }

        public IEnumerable<HostAddress> PublicAddresses { get { return Ordered(this.Addresses.Where(a => a.IsPublic)); } }
        public IEnumerable<HostAddress> PrivateAddresses { get { return Ordered(this.Addresses.Where(a => !a.IsPublic && a.Address.AddressFamily == AddressFamily.InterNetwork)); } }

        private static IEnumerable<HostAddress> Ordered(IEnumerable<HostAddress> addresses)
        {
            // IPv4 first
            return addresses.OrderBy(a => a.Address.AddressFamily == AddressFamily.InterNetwork ? 0 : 1);
        }

        /// <summary>
        /// An interface address from "ip -o addr" ("eth0", "78.47.238.52/32"), or null when it isn't one of the
        /// host's own addresses: loopback, link-local, container/VM bridges
        /// </summary>
        public static HostAddress ParseAddress(string interfaceName, string cidr)
        {
            string text = (cidr ?? string.Empty).Split('/')[0];
            IPAddress address;
            if (string.IsNullOrEmpty(interfaceName) || !IPAddress.TryParse(text, out address))
            {
                return null;
            }
            string name = interfaceName.Split('@')[0];
            if (name == "lo" || BridgePrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }
            if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || IsIPv4LinkLocal(address))
            {
                return null;
            }
            return new HostAddress { Interface = name, Address = address, IsPublic = !IsPrivate(address) };
        }

        /// <summary>RFC 1918, carrier-grade NAT (100.64/10) and IPv6 unique-local addresses</summary>
        public static bool IsPrivate(IPAddress address)
        {
            byte[] b = address.GetAddressBytes();
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                return b[0] == 10
                    || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                    || (b[0] == 192 && b[1] == 168)
                    || (b[0] == 100 && b[1] >= 64 && b[1] <= 127);
            }
            return (b[0] & 0xFE) == 0xFC || address.IsIPv6SiteLocal;
        }

        private static bool IsIPv4LinkLocal(IPAddress address)
        {
            byte[] b = address.GetAddressBytes();
            return address.AddressFamily == AddressFamily.InterNetwork && b[0] == 169 && b[1] == 254;
        }

        /// <summary>"35 days, 16h:29m:27s"</summary>
        public static string FormatUptime(TimeSpan uptime)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} day{1}, {2}h:{3:00}m:{4:00}s",
                uptime.Days, uptime.Days == 1 ? string.Empty : "s", uptime.Hours, uptime.Minutes, uptime.Seconds);
        }

        /// <summary>"35 d 16 h 29 m", short form for the pane</summary>
        public static string FormatUptimeShort(TimeSpan uptime)
        {
            return uptime.TotalDays >= 1
                ? string.Format(CultureInfo.InvariantCulture, "{0} d {1} h {2} m", uptime.Days, uptime.Hours, uptime.Minutes)
                : string.Format(CultureInfo.InvariantCulture, "{0} h {1} m", uptime.Hours, uptime.Minutes);
        }

        /// <summary>"212 GB", "940 MB"; from KiB</summary>
        public static string FormatSize(long kb)
        {
            double gb = kb / 1024.0 / 1024.0;
            if (gb >= 1000)
            {
                return (gb / 1024).ToString(gb >= 10240 ? "0" : "0.0", CultureInfo.InvariantCulture) + " TB";
            }
            if (gb >= 1)
            {
                return gb.ToString(gb >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture) + " GB";
            }
            return (kb / 1024).ToString(CultureInfo.InvariantCulture) + " MB";
        }

        /// <summary>The snapshot as health data for the session tree's badges (needs <see cref="HasSlowData"/>)</summary>
        public HostHealth ToHealth()
        {
            HostHealth health = new HostHealth
            {
                Load = this.Load == null ? (double?)null : this.Load[0],
                Cpus = Math.Max(1, this.Cpus),
                MemoryPercent = this.MemoryPercent,
                UnhealthyContainers = this.UnhealthyContainers,
                CheckedUtc = this.UpdatedUtc
            };
            foreach (DiskUsage disk in this.Disks)
            {
                health.Disks[disk.Mount] = disk.Percent;
            }
            health.FailedUnitNames.AddRange(this.FailedUnits);
            return health;
        }

        /// <summary>The snapshot as plain text, for pasting into a ticket or chat</summary>
        public string ToText()
        {
            StringBuilder sb = new StringBuilder();
            Line(sb, "Hostname", this.HostName);
            Line(sb, "IP Address (Pub)", string.Join(", ", this.PublicAddresses));
            Line(sb, "IP Address (Priv)", string.Join(", ", this.PrivateAddresses));
            Line(sb, "OS", string.IsNullOrEmpty(this.Kernel) ? this.Os : this.Os + " (" + this.Kernel + ")");
            Line(sb, "Uptime", this.Uptime == null ? null : FormatUptime(this.Uptime.Value));
            Line(sb, "Disk Space", string.Join("; ", this.Disks.Select(d =>
                string.Format(CultureInfo.InvariantCulture, "{0} {1} free of {2} ({3} % used)", d.Mount, FormatSize(d.AvailableKb), FormatSize(d.SizeKb), d.Percent))));
            sb.AppendLine(new string('=', 54));
            Line(sb, "System load", this.Load == null ? null : string.Format(CultureInfo.InvariantCulture,
                "{0:0.00}, {1:0.00}, {2:0.00} (1, 5, 15 min) on {3} CPU(s)", this.Load[0], this.Load[1], this.Load[2], this.Cpus));
            Line(sb, "CPU usage", this.CpuPercent == null ? null : this.CpuPercent.Value.ToString("0", CultureInfo.InvariantCulture) + " %");
            Line(sb, "Memory used", this.MemTotalKb <= 0 ? null : string.Format(CultureInfo.InvariantCulture, "{0} MB / {1} MB", this.MemUsedKb / 1024, this.MemTotalKb / 1024));
            Line(sb, "Swap in use", this.SwapTotalKb <= 0 ? null : string.Format(CultureInfo.InvariantCulture, "{0} MB", this.SwapUsedKb / 1024));
            Line(sb, "Logged-in users", this.HasSlowData ? string.Format(CultureInfo.InvariantCulture, "{0}{1}", this.Users.Count,
                this.Users.Count == 0 ? string.Empty : " (" + string.Join(", ", this.Users.Select(u => u.User).Distinct()) + ")") : null);
            Line(sb, "Processes", this.Processes == null ? null : this.Processes.Value.ToString(CultureInfo.InvariantCulture));
            foreach (GpuInfo gpu in this.Gpus)
            {
                Line(sb, "GPU " + gpu.Index, string.Join(", ", new[]
                {
                    gpu.Name,
                    gpu.UtilizationPercent == null ? null : gpu.UtilizationPercent + " % busy",
                    string.Format(CultureInfo.InvariantCulture, "{0} / {1} MB", gpu.MemoryUsedMb, gpu.MemoryTotalMb),
                    gpu.TemperatureC == null ? null : gpu.TemperatureC + " °C"
                }.Where(s => !string.IsNullOrEmpty(s))));
            }
            if (this.FailedUnits.Count > 0)
            {
                Line(sb, "Failed units", string.Join(", ", this.FailedUnits));
            }
            if (this.UnhealthyContainers > 0)
            {
                Line(sb, "Unhealthy containers", this.UnhealthyContainers.ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>" - Label.............: value", like a login banner</summary>
        private static void Line(StringBuilder sb, string label, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }
            sb.Append(" - ").Append(label).Append(new string('.', Math.Max(1, 20 - label.Length))).Append(": ").AppendLine(value);
        }

        /// <summary>A shallow copy, so the pane gets a snapshot the reader no longer changes</summary>
        public HostInfo Copy()
        {
            HostInfo copy = (HostInfo)MemberwiseClone();
            copy.Addresses = new List<HostAddress>(this.Addresses);
            copy.TopProcesses = new List<ProcessUsage>(this.TopProcesses);
            copy.Disks = new List<DiskUsage>(this.Disks);
            copy.Gpus = new List<GpuInfo>(this.Gpus);
            copy.GpuProcesses = new List<GpuProcess>(this.GpuProcesses);
            copy.Users = new List<LoggedInUser>(this.Users);
            copy.FailedUnits = new List<string>(this.FailedUnits);
            copy.Load = this.Load == null ? null : (double[])this.Load.Clone();
            return copy;
        }
    }

    /// <summary>
    /// Reads the output of <see cref="HostInfo.Script"/> line by line and returns a snapshot at the end of every
    /// round. Static and slow values are kept between rounds; CPU percentages come from the difference to the
    /// previous round's /proc/stat and per-process CPU times.
    /// </summary>
    public class HostInfoReader
    {
        private readonly HostInfo current = new HostInfo();

        // slow values arrive in several lines: collected here, swapped in when "slow=1" closes them
        private List<DiskUsage> disks = new List<DiskUsage>();
        private List<LoggedInUser> users = new List<LoggedInUser>();
        private List<string> failedUnits = new List<string>();
        private List<HostAddress> addresses = new List<HostAddress>();
        private bool addressesSeen;
        private List<GpuInfo> gpus = new List<GpuInfo>();
        private List<KeyValuePair<string, GpuProcess>> gpuApps = new List<KeyValuePair<string, GpuProcess>>();

        private long[] lastCpu;
        private long[] cpu;
        private Dictionary<string, long> lastProcesses;
        private readonly Dictionary<string, long> processes = new Dictionary<string, long>();
        private readonly Dictionary<string, string> processNames = new Dictionary<string, string>();

        /// <summary>How many top processes a snapshot lists</summary>
        public int TopCount { get; set; } = 5;

        /// <summary>Feed one line; returns a snapshot when the line ends a round, otherwise null</summary>
        public HostInfo Read(string line)
        {
            line = (line ?? string.Empty).Trim();
            if (line == "--end")
            {
                return EndRound();
            }
            int equals = line.IndexOf('=');
            if (equals <= 0)
            {
                return null;
            }
            string key = line.Substring(0, equals);
            string value = line.Substring(equals + 1).Trim();
            string[] parts;
            long number;
            switch (key)
            {
                case "host": this.current.HostName = value; break;
                case "os": this.current.Os = value; break;
                case "kernel": this.current.Kernel = value; break;
                case "cpus":
                    int cpus;
                    if (int.TryParse(value, out cpus) && cpus > 0) { this.current.Cpus = cpus; }
                    break;
                case "addr":
                    // addr=<interface>=<address>/<prefix>
                    parts = value.Split('=');
                    HostAddress address = parts.Length == 2 ? HostInfo.ParseAddress(parts[0], parts[1]) : null;
                    this.addressesSeen = true;
                    if (address != null && !this.addresses.Any(a => a.Address.Equals(address.Address)))
                    {
                        this.addresses.Add(address);
                    }
                    break;
                case "disk":
                    // disk=<device>=<mount>=<size>=<used>=<available>, KiB; the mount itself may contain "="
                    parts = value.Split('=');
                    long size, used, available;
                    if (parts.Length >= 5
                        && long.TryParse(parts[parts.Length - 3], out size) && long.TryParse(parts[parts.Length - 2], out used)
                        && long.TryParse(parts[parts.Length - 1], out available) && size > 0)
                    {
                        AddDisk(new DiskUsage
                        {
                            Device = parts[0],
                            Mount = string.Join("=", parts.Skip(1).Take(parts.Length - 4)),
                            SizeKb = size, UsedKb = used, AvailableKb = available
                        });
                    }
                    break;
                case "user":
                    LoggedInUser user = ParseWho(value);
                    if (user != null) { this.users.Add(user); }
                    break;
                case "failedunit":
                    if (value.Length > 0 && !this.failedUnits.Contains(value)) { this.failedUnits.Add(value); }
                    break;
                case "unhealthy":
                    int unhealthy;
                    if (int.TryParse(value, out unhealthy)) { this.current.UnhealthyContainers = unhealthy; }
                    break;
                case "slow":
                    this.current.Disks = this.disks;
                    this.current.Users = this.users;
                    this.current.FailedUnits = this.failedUnits;
                    this.current.HasSlowData = true;
                    this.disks = new List<DiskUsage>();
                    this.users = new List<LoggedInUser>();
                    this.failedUnits = new List<string>();
                    break;
                case "uptime":
                    double seconds;
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
                    {
                        this.current.Uptime = TimeSpan.FromSeconds(Math.Floor(seconds));
                    }
                    break;
                case "loadavg":
                    // "0.05 0.08 0.08 2/671 12345"
                    parts = value.Split(' ');
                    double l1, l5, l15;
                    if (parts.Length >= 3
                        && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out l1)
                        && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out l5)
                        && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out l15))
                    {
                        this.current.Load = new[] { l1, l5, l15 };
                    }
                    break;
                case "cpustat":
                    // "cpu  user nice system idle iowait irq softirq steal ..."
                    this.cpu = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Skip(1)
                        .Select(v => long.TryParse(v, out number) ? number : 0).ToArray();
                    break;
                case "meminfo":
                    parts = value.Split('=');
                    if (parts.Length == 2 && long.TryParse(parts[1], out number))
                    {
                        switch (parts[0])
                        {
                            case "MemTotal": this.current.MemTotalKb = number; break;
                            case "MemAvailable": this.current.MemAvailableKb = number; break;
                            case "SwapTotal": this.current.SwapTotalKb = number; break;
                            case "SwapFree": this.current.SwapFreeKb = number; break;
                        }
                    }
                    break;
                case "gpu":
                    GpuInfo gpu = ParseGpu(value);
                    if (gpu != null) { this.gpus.Add(gpu); }
                    break;
                case "gpuapp":
                    // gpu_uuid, pid, process_name, used_memory (MiB); name or memory may be "[N/A]" or "[Insufficient Permissions]"
                    parts = value.Split(',').Select(p => p.Trim()).ToArray();
                    if (parts.Length >= 4 && long.TryParse(parts[parts.Length - 1], out number))
                    {
                        this.gpuApps.Add(new KeyValuePair<string, GpuProcess>(parts[0], new GpuProcess
                        {
                            Pid = parts[1],
                            Name = ProgramName(string.Join(",", parts.Skip(2).Take(parts.Length - 3))),
                            MemoryMb = number
                        }));
                    }
                    break;
                case "proc":
                    // proc=<pid>=<utime+stime>=<name>
                    parts = value.Split(new[] { '=' }, 3);
                    if (parts.Length == 3 && long.TryParse(parts[1], out number))
                    {
                        this.processes[parts[0]] = number;
                        this.processNames[parts[0]] = parts[2];
                    }
                    break;
            }
            return null;
        }

        /// <summary>
        /// One entry per filesystem: bind mounts show the same device under several mount points, the shortest
        /// (e.g. "/" rather than "/var/lib/docker") stands for it
        /// </summary>
        private void AddDisk(DiskUsage disk)
        {
            int index = this.disks.FindIndex(d => d.Device == disk.Device);
            if (index < 0)
            {
                this.disks.Add(disk);
            }
            else if (disk.Mount.Length < this.disks[index].Mount.Length)
            {
                this.disks[index] = disk;
            }
        }

        /// <summary>"0, GPU-0652…, NVIDIA H100 80GB HBM3, 45, 30100, 81559, 64"; values may be "[N/A]"</summary>
        public static GpuInfo ParseGpu(string line)
        {
            string[] parts = (line ?? string.Empty).Split(',').Select(p => p.Trim()).ToArray();
            int index;
            if (parts.Length < 7 || !int.TryParse(parts[0], out index))
            {
                return null;
            }
            // the name is everything between uuid and the four numbers (in case it contains a comma)
            int n = parts.Length;
            long used, total;
            return new GpuInfo
            {
                Index = index,
                Uuid = parts[1],
                Name = string.Join(", ", parts.Skip(2).Take(n - 6)),
                UtilizationPercent = Number(parts[n - 4]),
                MemoryUsedMb = long.TryParse(parts[n - 3], out used) ? used : 0,
                MemoryTotalMb = long.TryParse(parts[n - 2], out total) ? total : 0,
                TemperatureC = Number(parts[n - 1])
            };
        }

        private static int? Number(string text)
        {
            int value;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : (int?)null;
        }

        /// <summary>"/opt/conda/bin/python3" -> "python3"; "[Insufficient Permissions]" stays as it is</summary>
        private static string ProgramName(string path)
        {
            if (path.StartsWith("["))
            {
                return path;
            }
            int slash = path.LastIndexOfAny(new[] { '/', '\\' });
            return slash >= 0 && slash < path.Length - 1 ? path.Substring(slash + 1) : path;
        }

        private HostInfo EndRound()
        {
            // GPUs come every round; processes are matched to their GPU by its uuid
            this.current.Gpus = this.gpus;
            this.current.GpuProcesses = this.gpuApps
                .Select(app =>
                {
                    GpuInfo owner = this.gpus.FirstOrDefault(g => g.Uuid == app.Key);
                    app.Value.GpuIndex = owner == null ? -1 : owner.Index;
                    return app.Value;
                })
                .OrderByDescending(p => p.MemoryMb)
                .ToList();
            this.gpus = new List<GpuInfo>();
            this.gpuApps = new List<KeyValuePair<string, GpuProcess>>();

            if (this.addressesSeen)
            {
                this.current.Addresses = this.addresses;
                this.addresses = new List<HostAddress>();
                this.addressesSeen = false;
            }
            if (this.processes.Count > 0)
            {
                this.current.Processes = this.processes.Count;
            }

            // CPU: busy share of all jiffies since the last round; per process: its jiffies against one CPU's
            this.current.CpuPercent = null;
            this.current.TopProcesses = new List<ProcessUsage>();
            if (this.cpu != null && this.lastCpu != null && this.cpu.Length >= 4 && this.lastCpu.Length >= 4)
            {
                long total = this.cpu.Sum() - this.lastCpu.Sum();
                // idle + iowait
                long idle = (this.cpu[3] - this.lastCpu[3]) + (this.cpu.Length > 4 && this.lastCpu.Length > 4 ? this.cpu[4] - this.lastCpu[4] : 0);
                if (total > 0)
                {
                    this.current.CpuPercent = Math.Max(0, Math.Min(100, (total - idle) * 100.0 / total));
                    double perCpu = total / (double)Math.Max(1, this.current.Cpus);
                    if (this.lastProcesses != null)
                    {
                        this.current.TopProcesses = this.processes
                            .Where(p => this.lastProcesses.ContainsKey(p.Key))
                            .Select(p => new ProcessUsage { Name = this.processNames[p.Key], CpuPercent = (p.Value - this.lastProcesses[p.Key]) * 100.0 / perCpu })
                            .Where(p => p.CpuPercent >= 0.5)
                            .OrderByDescending(p => p.CpuPercent)
                            .Take(this.TopCount)
                            .ToList();
                    }
                }
            }
            this.lastCpu = this.cpu;
            this.cpu = null;
            this.lastProcesses = this.processes.Count > 0 ? new Dictionary<string, long>(this.processes) : null;
            this.processes.Clear();
            this.processNames.Clear();

            this.current.UpdatedUtc = DateTime.UtcNow;
            return this.current.Copy();
        }

        /// <summary>
        /// One line of who: "root     pts/0        2026-10-24 09:12 (10.8.0.6)" or "dk  tty1  Oct 24 09:12".
        /// The origin is the part in parentheses, the time what lies between terminal and origin.
        /// </summary>
        public static LoggedInUser ParseWho(string line)
        {
            string[] fields = (line ?? string.Empty).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 2)
            {
                return null;
            }
            string rest = string.Join(" ", fields.Skip(2));
            string from = string.Empty;
            int open = rest.LastIndexOf('(');
            if (open >= 0 && rest.EndsWith(")"))
            {
                from = rest.Substring(open + 1, rest.Length - open - 2);
                rest = rest.Substring(0, open).Trim();
            }
            return new LoggedInUser { User = fields[0], Terminal = fields[1], From = from, Since = rest };
        }
    }
}
