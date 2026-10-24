using System.Collections.Generic;
using System.Linq;
using SuperPutty.Data;

namespace SuperPutty.Utils
{
    /// <summary>A notification about a host's health</summary>
    public class HealthAlert
    {
        public string SessionId { get; set; }
        public string Host { get; set; }
        /// <summary>True: the host turned critical; false: it is no longer critical</summary>
        public bool Critical { get; set; }
        /// <summary>The problems (critical ones first), or what is left after recovering</summary>
        public List<string> Problems { get; set; } = new List<string>();
    }

    /// <summary>
    /// Remembers each host's last health level and reports changes worth a notification: a host turning
    /// critical, and a critical host recovering. Warnings alone don't notify (too frequent), a failed check
    /// (level Unknown) changes nothing, and a host that stays critical is reported once.
    /// </summary>
    public class HealthAlerts
    {
        private readonly Dictionary<string, HealthLevel> levels = new Dictionary<string, HealthLevel>();

        /// <summary>The alert for a new result, or null</summary>
        public HealthAlert Update(string sessionId, string host, HostHealth health)
        {
            if (sessionId == null || health == null || health.Level == HealthLevel.Unknown)
            {
                return null;
            }
            HealthLevel previous;
            bool known = this.levels.TryGetValue(sessionId, out previous);
            HealthLevel level = health.Level;
            this.levels[sessionId] = level;

            bool wasCritical = known && previous == HealthLevel.Critical;
            if (level == HealthLevel.Critical && !wasCritical)
            {
                return new HealthAlert { SessionId = sessionId, Host = host, Critical = true, Problems = health.Problems().Select(p => p.Value).ToList() };
            }
            if (level != HealthLevel.Critical && wasCritical)
            {
                return new HealthAlert { SessionId = sessionId, Host = host, Critical = false, Problems = health.Problems().Select(p => p.Value).ToList() };
            }
            return null;
        }

        /// <summary>
        /// Title and text of one notification for alerts that arrived together (e.g. the first checks after
        /// start-up), most important first; a balloon shows only a few lines
        /// </summary>
        public static KeyValuePair<string, string> Compose(IList<HealthAlert> alerts)
        {
            List<HealthAlert> critical = alerts.Where(a => a.Critical).ToList();
            List<HealthAlert> recovered = alerts.Where(a => !a.Critical).ToList();
            string title;
            if (alerts.Count == 1)
            {
                title = alerts[0].Host + (alerts[0].Critical ? ": critical" : ": back to normal");
            }
            else if (recovered.Count == 0)
            {
                title = critical.Count + " hosts critical";
            }
            else if (critical.Count == 0)
            {
                title = recovered.Count + " hosts back to normal";
            }
            else
            {
                title = critical.Count + " critical, " + recovered.Count + " back to normal";
            }

            List<string> lines = new List<string>();
            if (alerts.Count == 1)
            {
                HealthAlert alert = alerts[0];
                lines.AddRange(alert.Critical ? alert.Problems.Take(3)
                    : new[] { alert.Problems.Count == 0 ? "No problems left." : "Left: " + string.Join(", ", alert.Problems.Take(2)) });
            }
            else
            {
                lines.AddRange(critical.Select(a => a.Host + ": " + (a.Problems.Count == 0 ? "critical" : a.Problems[0])));
                lines.AddRange(recovered.Select(a => a.Host + ": back to normal"));
                if (lines.Count > 4)
                {
                    int more = lines.Count - 3;
                    lines = lines.Take(3).ToList();
                    lines.Add("… and " + more + " more");
                }
            }
            return new KeyValuePair<string, string>(title, string.Join("\n", lines));
        }
    }
}
