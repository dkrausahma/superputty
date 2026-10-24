using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using SuperPutty.Data;

namespace SuperPutty.Utils
{
    /// <summary>
    /// Checks hosts' health in the background with one plink call per host (<see cref="HostHealth.ProbeCommand"/>)
    /// and keeps the latest result per session. Open tabs are checked periodically by the main form; other
    /// sessions only on request (session tree → Check Health), so SuperPuTTY never logs in to hosts unasked.
    /// </summary>
    public static class HealthMonitor
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(HealthMonitor));

        /// <summary>
        /// Minimum time between two automatic checks of the same session. Every check is an SSH login that shows up
        /// in the host's auth log, and health doesn't change by the minute.
        /// </summary>
        public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

        private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(25);
        private static readonly SemaphoreSlim Parallel = new SemaphoreSlim(4);
        private static readonly Dictionary<string, HostHealth> Results = new Dictionary<string, HostHealth>();
        private static readonly HashSet<string> Running = new HashSet<string>();
        private static readonly object Sync = new object();

        /// <summary>Raised on the UI thread with the SessionId whose health changed</summary>
        public static event Action<string> Updated;

        /// <summary>The latest result for a session, or null</summary>
        public static HostHealth Get(string sessionId)
        {
            lock (Sync)
            {
                HostHealth health;
                return sessionId != null && Results.TryGetValue(sessionId, out health) ? health : null;
            }
        }

        /// <summary>
        /// Check the sessions (SSH only) in the background; without <paramref name="force"/> only those not checked
        /// within <see cref="Interval"/>. Call from the UI thread.
        /// </summary>
        public static void Check(IEnumerable<SessionData> sessions, bool force)
        {
            if (Plink.FindExecutable() == null)
            {
                return;
            }
            SynchronizationContext ui = SynchronizationContext.Current;
            foreach (SessionData session in sessions)
            {
                if (!RemoteCommandTab.IsSupported(session))
                {
                    continue;
                }
                string id = session.SessionId;
                lock (Sync)
                {
                    HostHealth last;
                    if (Running.Contains(id) || (!force && Results.TryGetValue(id, out last) && DateTime.UtcNow - last.CheckedUtc < Interval))
                    {
                        continue;
                    }
                    Running.Add(id);
                }
                Task.Run(() => Probe(session, id, ui));
            }
        }

        /// <summary>
        /// A result measured elsewhere (the Host Info pane's live connection); counts as a fresh check, so the
        /// periodic check skips the host. Call from the UI thread.
        /// </summary>
        public static void Report(string sessionId, HostHealth health)
        {
            if (sessionId == null || health == null)
            {
                return;
            }
            lock (Sync)
            {
                Results[sessionId] = health;
            }
            Raise(sessionId);
        }

        private static async Task Probe(SessionData session, string id, SynchronizationContext ui)
        {
            HostHealth health;
            await Parallel.WaitAsync().ConfigureAwait(false);
            try
            {
                PlinkResult result = await Plink.RunAsync(session, HostHealth.ProbeCommand, ProbeTimeout).ConfigureAwait(false);
                health = result.TimedOut ? HostHealth.Failed("no answer (timeout)")
                    : result.Success || result.Output.Contains("load=") ? HostHealth.Parse(result.Output)
                    : HostHealth.Failed(FirstLine(result.Error + "\n" + result.Output));
            }
            catch (Exception ex)
            {
                Log.Warn("Health check of " + id + " failed", ex);
                health = HostHealth.Failed(ex.Message);
            }
            finally
            {
                Parallel.Release();
            }

            lock (Sync)
            {
                Results[id] = health;
                Running.Remove(id);
            }
            if (ui != null)
            {
                ui.Post(delegate { Raise(id); }, null);
            }
            else
            {
                Raise(id);
            }
        }

        private static void Raise(string id)
        {
            if (Updated != null)
            {
                Updated(id);
            }
        }

        private static string FirstLine(string text)
        {
            foreach (string line in text.Split('\n'))
            {
                if (line.Trim().Length > 0)
                {
                    return line.Trim().Length > 150 ? line.Trim().Substring(0, 150) + "…" : line.Trim();
                }
            }
            return "unknown error";
        }
    }
}
