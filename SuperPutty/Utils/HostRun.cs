using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SuperPutty.Data;

namespace SuperPutty.Utils
{
    public enum HostRunState
    {
        Waiting,
        Running,
        /// <summary>Exit code 0</summary>
        Succeeded,
        /// <summary>Exit code other than 0</summary>
        Failed,
        /// <summary>No answer, login failed, timeout</summary>
        Error,
        Stopped,
        /// <summary>Not an SSH session</summary>
        Skipped,
        /// <summary>Unchecked by the user for this run</summary>
        NotRun
    }

    /// <summary>The result of a command on one host (Run on Hosts)</summary>
    public class HostRunResult
    {
        public SessionData Session { get; set; }
        public HostRunState State { get; set; }
        public int? ExitCode { get; set; }
        public TimeSpan Duration { get; set; }
        /// <summary>Standard output and, if any, standard error, without terminal control codes</summary>
        public string Output { get; set; } = string.Empty;

        public string Host { get { return this.Session == null ? string.Empty : this.Session.SessionName; } }

        /// <summary>"ok", "exit 2", "timeout"…</summary>
        public string StateText
        {
            get
            {
                switch (this.State)
                {
                    case HostRunState.Waiting: return "waiting";
                    case HostRunState.Running: return "running…";
                    case HostRunState.Succeeded: return "ok";
                    case HostRunState.Failed: return "exit " + this.ExitCode;
                    case HostRunState.Stopped: return "stopped";
                    case HostRunState.Skipped: return "skipped (not SSH)";
                    case HostRunState.NotRun: return "not run";
                    default: return "error";
                }
            }
        }

        public bool IsDone
        {
            get { return this.State != HostRunState.Waiting && this.State != HostRunState.Running; }
        }

        public bool IsProblem
        {
            get { return this.State == HostRunState.Failed || this.State == HostRunState.Error; }
        }

        /// <summary>
        /// The first non-empty output line, for the table; a line ending in ":" ("Unable to open connection:")
        /// gets the next one appended, which says why
        /// </summary>
        public string FirstLine
        {
            get
            {
                List<string> lines = (this.Output ?? string.Empty).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
                if (lines.Count == 0)
                {
                    return string.Empty;
                }
                return lines[0].EndsWith(":") && lines.Count > 1 ? lines[0] + " " + lines[1] : lines[0];
            }
        }

        /// <summary>Fill in a finished plink run</summary>
        public void Complete(PlinkResult result, TimeSpan duration)
        {
            this.Duration = duration;
            string output = TerminalText.Clean(result.Output ?? string.Empty).TrimEnd();
            string error = TerminalText.Clean(result.Error ?? string.Empty).TrimEnd();
            if (result.Cancelled)
            {
                this.State = HostRunState.Stopped;
                this.Output = string.Empty;
                return;
            }
            if (result.TimedOut)
            {
                this.State = HostRunState.Error;
                this.Output = string.Join("\r\n", new[] { output, error }.Where(s => s.Length > 0));
                return;
            }
            this.ExitCode = result.ExitCode;
            // plink's own failures (login, host key, unreachable) end with code 1 and only stderr
            bool plinkFailed = result.ExitCode != 0 && output.Length == 0 && IsPlinkError(error);
            this.State = plinkFailed ? HostRunState.Error : result.ExitCode == 0 ? HostRunState.Succeeded : HostRunState.Failed;
            this.Output = error.Length == 0 ? output : output.Length == 0 ? error : output + "\r\n--- stderr ---\r\n" + error;
        }

        private static bool IsPlinkError(string error)
        {
            string[] markers = { "FATAL ERROR", "Cannot confirm a host key", "Access denied", "No supported authentication",
                "Network error", "Host does not exist", "Connection refused", "Connection timed out", "Unable to open connection" };
            return markers.Any(m => error.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }

    /// <summary>Text form of a Run on Hosts result, for the clipboard or a file</summary>
    public static class HostRunReport
    {
        public static string Format(string command, DateTime started, IEnumerable<HostRunResult> results)
        {
            List<HostRunResult> list = results.ToList();
            StringBuilder sb = new StringBuilder();
            sb.Append("Command: ").AppendLine(command);
            sb.AppendFormat(CultureInfo.InvariantCulture, "Run:     {0:yyyy-MM-dd HH:mm:ss} on {1} host(s): {2} ok, {3} failed, {4} error(s)",
                started, list.Count, list.Count(r => r.State == HostRunState.Succeeded), list.Count(r => r.State == HostRunState.Failed),
                list.Count(r => r.State == HostRunState.Error)).AppendLine();
            foreach (HostRunResult result in list)
            {
                sb.AppendLine();
                sb.AppendFormat(CultureInfo.InvariantCulture, "=== {0} ({1}, {2:0.0} s) ===", result.Host, result.StateText, result.Duration.TotalSeconds).AppendLine();
                if (result.Output.Length > 0)
                {
                    sb.AppendLine(result.Output);
                }
            }
            return sb.ToString();
        }
    }
}
