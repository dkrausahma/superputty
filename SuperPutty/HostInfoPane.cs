using System;
using System.Globalization;
using System.Windows.Forms;
using log4net;
using SuperPutty.Data;
using SuperPutty.Utils;

namespace SuperPutty
{
    /// <summary>
    /// Tool window with live facts about the active terminal's host: name, OS, addresses, uptime, CPU, memory,
    /// disks, logged-in users and the busiest processes. One plink connection per shown host
    /// (<see cref="HostInfoStream"/>) updates it every few seconds; it runs only while the pane is visible and
    /// SuperPuTTY isn't minimised, and the values also refresh the host's health badge.
    /// </summary>
    public partial class HostInfoPane : ToolWindow
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(HostInfoPane));

        /// <summary>After a failed connection, wait this long before trying the same host again on its own</summary>
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

        /// <summary>The health badge doesn't need more than one update per slow round</summary>
        private static readonly TimeSpan HealthReportInterval = TimeSpan.FromSeconds(HostInfo.FastSeconds * HostInfo.SlowEvery - 1);

        /// <summary>Returns the session of the active terminal tab, or null</summary>
        private readonly Func<SessionData> activeSession;

        private HostInfoStream stream;
        private HostInfo last;
        /// <summary>The session whose values the view shows (or is connecting to)</summary>
        private SessionData shownSession;
        private SessionData failedSession;
        private DateTime retryAtUtc;
        private DateTime lastHealthReportUtc;

        /// <summary>For the layout restore and the designer</summary>
        public HostInfoPane() : this(null) { }

        public HostInfoPane(Func<SessionData> activeSession)
        {
            InitializeComponent();
            this.activeSession = activeSession;
            this.VisibleChanged += delegate { Evaluate(); };
            this.timerCheck.Start();
        }

        /// <summary>Called by the main form when the active tab changes</summary>
        public void ActiveSessionChanged()
        {
            Evaluate();
        }

        private void timerCheck_Tick(object sender, EventArgs e)
        {
            Evaluate();
        }

        /// <summary>The pane is on screen: not hidden, the front tab of its pane, the main window not minimised</summary>
        private bool IsShowing
        {
            get
            {
                if (!this.Visible || this.IsHidden || (this.Pane != null && this.Pane.ActiveContent != this))
                {
                    return false;
                }
                Form main = this.DockPanel == null ? null : this.DockPanel.FindForm();
                return main == null || main.WindowState != FormWindowState.Minimized;
            }
        }

        /// <summary>Start, keep, switch or stop the connection for the current state</summary>
        private void Evaluate(bool force = false)
        {
            SessionData target = this.activeSession == null ? null : this.activeSession();
            if (target != null && !RemoteCommandTab.IsSupported(target))
            {
                target = null;
            }

            if (!IsShowing)
            {
                if (this.stream != null)
                {
                    StopStream();
                    this.hostInfoView.Status = "Paused while the pane isn't visible";
                }
                return;
            }
            if (this.stream != null && SameHost(this.stream.Session, target))
            {
                return; // running for the right host
            }

            StopStream();
            if (target == null)
            {
                ShowNothing("No SSH terminal tab is active.");
                return;
            }
            if (Plink.FindExecutable() == null)
            {
                ShowNothing("plink.exe was not found next to PuTTY or on the PATH. It comes with the PuTTY installer.");
                return;
            }
            if (!force && SameHost(this.failedSession, target) && DateTime.UtcNow < this.retryAtUtc)
            {
                return; // keep showing the error until the retry time
            }

            if (!SameHost(this.shownSession, target))
            {
                // another host: its old values would mislead; the same host keeps them until fresh ones arrive
                this.last = null;
                this.hostInfoView.Info = null;
                this.toolStripButtonCopy.Enabled = false;
                this.shownSession = target;
            }
            this.toolStripLabelHost.Text = target.SessionName;
            this.hostInfoView.SetMessage(this.last == null ? "Connecting to " + target.SessionName + "…" : null, false);
            this.hostInfoView.Status = null;
            try
            {
                this.stream = new HostInfoStream(target);
                this.stream.Snapshot += Stream_Snapshot;
                this.stream.Failed += Stream_Failed;
                this.stream.Start();
            }
            catch (Exception ex) when (ex is System.IO.FileNotFoundException || ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
            {
                Log.Error("Could not start plink for the host info", ex);
                this.stream = null;
                Stream_Failed("Could not run plink: " + ex.Message, target);
            }
        }

        private static bool SameHost(SessionData a, SessionData b)
        {
            return a != null && b != null && (ReferenceEquals(a, b) || a.SessionId == b.SessionId);
        }

        private void ShowNothing(string message)
        {
            this.last = null;
            this.shownSession = null;
            this.toolStripLabelHost.Text = string.Empty;
            this.toolStripButtonCopy.Enabled = false;
            this.hostInfoView.Info = null;
            this.hostInfoView.Status = null;
            this.hostInfoView.SetMessage(message, false);
        }

        private void Stream_Snapshot(HostInfo info)
        {
            this.last = info;
            this.failedSession = null;
            this.hostInfoView.SetMessage(null, false);
            this.hostInfoView.Info = info;
            this.hostInfoView.Status = string.Format(CultureInfo.InvariantCulture, "Live · updated {0:HH:mm:ss} · every {1} s",
                info.UpdatedUtc.ToLocalTime(), HostInfo.FastSeconds);
            this.toolStripButtonCopy.Enabled = true;

            if (info.HasSlowData && DateTime.UtcNow - this.lastHealthReportUtc >= HealthReportInterval && this.stream != null)
            {
                // the badge belongs to the session in the tree (command tabs are copies of it)
                SessionData session = this.stream.Session;
                SessionData original = SuperPuTTY.GetSessionById(session.ScopeSessionId);
                HealthMonitor.Report((original ?? session).SessionId, info.ToHealth());
                this.lastHealthReportUtc = DateTime.UtcNow;
            }
        }

        private void Stream_Failed(string reason)
        {
            SessionData session = this.stream == null ? null : this.stream.Session;
            StopStream();
            Stream_Failed(reason, session);
        }

        private void Stream_Failed(string reason, SessionData session)
        {
            this.failedSession = session;
            this.retryAtUtc = DateTime.UtcNow + RetryDelay;
            this.hostInfoView.SetMessage(Explain(reason) + "\nTrying again in " + RetryDelay.TotalSeconds + " s; Reconnect (F5) tries now.", true);
            this.hostInfoView.Status = this.last == null ? null : "Last values from " + this.last.UpdatedUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }

        /// <summary>A readable reason for a failed connection</summary>
        private static string Explain(string reason)
        {
            if (reason.IndexOf("Cannot confirm a host key in batch mode", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "The host key is not known yet. Connect once with PuTTY and accept it.";
            }
            if (reason.IndexOf("authentication", StringComparison.OrdinalIgnoreCase) >= 0 || reason.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Background login failed. Host Info needs key-based login (e.g. Pageant).\n" + reason;
            }
            return "Host Info lost the connection: " + reason;
        }

        private void StopStream()
        {
            HostInfoStream running = this.stream;
            this.stream = null;
            if (running != null)
            {
                running.Snapshot -= Stream_Snapshot;
                running.Failed -= Stream_Failed;
                running.Stop();
            }
        }

        private void toolStripButtonCopy_Click(object sender, EventArgs e)
        {
            if (this.last != null)
            {
                Clipboard.SetText(this.last.ToText());
                SuperPuTTY.ReportStatus("Copied the host information of {0}", this.last.HostName);
            }
        }

        private void toolStripButtonReconnect_Click(object sender, EventArgs e)
        {
            Reconnect();
        }

        private void Reconnect()
        {
            StopStream();
            this.failedSession = null;
            Evaluate(true);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5)
            {
                Reconnect();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            this.timerCheck.Stop();
            StopStream();
            base.OnFormClosed(e);
        }
    }
}
