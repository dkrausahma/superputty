using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using SuperPutty.Data;

namespace SuperPutty.Utils
{
    /// <summary>
    /// One long-running plink connection to a host that runs <see cref="HostInfo.Script"/> and reports a snapshot
    /// every few seconds. A single login instead of one per refresh: every SSH login is an entry in the host's
    /// auth log. Events are raised on the thread that created the stream (the UI thread).
    /// </summary>
    public sealed class HostInfoStream : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(HostInfoStream));

        private readonly SessionData session;
        private readonly SynchronizationContext ui;
        private Process process;
        private volatile bool stopped;

        /// <summary>A round of values arrived</summary>
        public event Action<HostInfo> Snapshot;

        /// <summary>The connection ended without Stop: the reason (first error line, or the exit code)</summary>
        public event Action<string> Failed;

        public HostInfoStream(SessionData session)
        {
            this.session = session;
            this.ui = SynchronizationContext.Current;
        }

        public SessionData Session { get { return this.session; } }

        public void Start()
        {
            Process started = Plink.Start(this.session, HostInfo.Script);
            this.process = started;
            Task<string> errors = started.StandardError.ReadToEndAsync();
            Task.Run(() => ReadLoop(started, errors));
        }

        private void ReadLoop(Process started, Task<string> errors)
        {
            HostInfoReader reader = new HostInfoReader();
            bool gotData = false;
            try
            {
                string line;
                while (!this.stopped && (line = started.StandardOutput.ReadLine()) != null)
                {
                    HostInfo info = reader.Read(line);
                    if (info != null)
                    {
                        gotData = true;
                        Post(() => { if (!this.stopped && this.Snapshot != null) this.Snapshot(info); });
                    }
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.IO.IOException)
            {
                Log.Debug("Host info stream of " + this.session.SessionId + " ended", ex);
            }
            if (this.stopped)
            {
                return;
            }

            string error = string.Empty;
            try
            {
                started.WaitForExit(2000);
                error = errors.Wait(2000) ? errors.Result : string.Empty;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is AggregateException)
            {
                Log.Debug("Could not read the end of the host info stream", ex);
            }
            string reason = FirstLine(error);
            if (reason.Length == 0)
            {
                reason = gotData ? "The connection was closed." : "No answer from the host.";
            }
            Log.InfoFormat("Host info stream of {0} ended: {1}", this.session.SessionId, reason);
            Post(() => { if (!this.stopped && this.Failed != null) this.Failed(reason); });
        }

        private void Post(Action action)
        {
            if (this.ui != null)
            {
                this.ui.Post(delegate { action(); }, null);
            }
            else
            {
                action();
            }
        }

        private static string FirstLine(string text)
        {
            foreach (string line in (text ?? string.Empty).Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    return trimmed.Length > 200 ? trimmed.Substring(0, 200) + "…" : trimmed;
                }
            }
            return string.Empty;
        }

        /// <summary>End the connection; no more events afterwards</summary>
        public void Stop()
        {
            this.stopped = true;
            Process running = this.process;
            this.process = null;
            if (running == null)
            {
                return;
            }
            try
            {
                if (!running.HasExited)
                {
                    running.Kill(true);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
            {
                Log.Debug("Could not stop plink", ex);
            }
            finally
            {
                running.Dispose();
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
