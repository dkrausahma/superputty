using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using log4net;
using SuperPutty.Data;
using SuperPutty.Utils;

namespace SuperPutty.Gui
{
    /// <summary>
    /// Run on Hosts: one command on several sessions' hosts in the background (plink, a few at a time), each
    /// host's exit code and output in a table, the full output of the selected host below it. Copy All and Save
    /// produce a text report. Production hosts among the checked ones need a confirmation.
    /// </summary>
    public class dlgRunOnHosts : Form
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(dlgRunOnHosts));

        private const int Parallel = 6;
        private static readonly List<string> RecentCommands = new List<string>();

        private readonly List<HostRunResult> results;
        private readonly ComboBox comboCommand = new ComboBox();
        private readonly NumericUpDown numericTimeout = new NumericUpDown();
        private readonly Button buttonRun = new Button();
        private readonly Button buttonStop = new Button();
        private readonly ListView listResults = new ListView();
        private readonly TextBox textOutput = new TextBox();
        private readonly Label labelSummary = new Label();
        private readonly CheckBox checkProblems = new CheckBox();
        private readonly Button buttonCopy = new Button();
        private readonly Button buttonSave = new Button();
        private readonly ImageList stateImages = new ImageList();

        /// <summary>Hosts the user unchecked; kept here because the "Only failed" filter hides list items</summary>
        private readonly HashSet<HostRunResult> excluded = new HashSet<HostRunResult>();
        private bool filling;
        private CancellationTokenSource running;
        private string lastCommand;
        private DateTime lastStarted;

        /// <summary>Show the dialog for these sessions (non-modal)</summary>
        public static void ShowFor(IWin32Window owner, IEnumerable<SessionData> sessions, string targetDescription)
        {
            List<SessionData> list = sessions.Where(s => s != null).Distinct().OrderBy(s => s.SessionId, StringComparer.OrdinalIgnoreCase).ToList();
            if (list.Count == 0)
            {
                MessageBox.Show(owner, "There are no sessions to run a command on.", "Run on Hosts", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            dlgRunOnHosts dialog = new dlgRunOnHosts(list, targetDescription);
            dialog.Show(owner);
        }

        private dlgRunOnHosts(List<SessionData> sessions, string targetDescription)
        {
            this.results = sessions.Select(s => new HostRunResult
            {
                Session = s,
                State = RemoteCommandTab.IsSupported(s) ? HostRunState.Waiting : HostRunState.Skipped
            }).ToList();

            this.Text = "Run on Hosts – " + targetDescription;
            this.Size = new Size(820, 600);
            this.MinimumSize = new Size(560, 380);
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.Icon = null;
            this.ShowIcon = false;

            BuildStateImages();
            BuildLayout(targetDescription);
            FillList();
            UpdateSummary();
        }

        #region Layout

        private void BuildStateImages()
        {
            this.stateImages.ImageSize = new Size(12, 12);
            this.stateImages.ColorDepth = ColorDepth.Depth32Bit;
            foreach (HostRunState state in Enum.GetValues(typeof(HostRunState)))
            {
                Bitmap dot = new Bitmap(12, 12);
                using (Graphics g = Graphics.FromImage(dot))
                {
                    PaneStyle.DrawDot(g, new Point(6, 6), 8, StateColor(state));
                }
                this.stateImages.Images.Add(state.ToString(), dot);
            }
        }

        private static Color StateColor(HostRunState state)
        {
            switch (state)
            {
                case HostRunState.Succeeded: return PaneStyle.Green;
                case HostRunState.Running: return PaneStyle.Amber;
                case HostRunState.Failed:
                case HostRunState.Error: return PaneStyle.Red;
                default: return SystemColors.GrayText;
            }
        }

        private void BuildLayout(string targetDescription)
        {
            Font mono = new Font("Consolas", 9f);

            // top: targets, command, timeout, run/stop
            TableLayoutPanel top = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 5, Padding = new Padding(8, 8, 8, 4) };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 4; i++)
            {
                top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            }
            int ssh = this.results.Count(r => r.State != HostRunState.Skipped);
            int skipped = this.results.Count - ssh;
            Label labelTargets = new Label
            {
                AutoSize = true,
                Text = string.Format("{0}: {1} SSH host(s){2}. Uncheck hosts to leave them out.", targetDescription, ssh,
                    skipped > 0 ? ", " + skipped + " other session(s) skipped" : string.Empty),
                Margin = new Padding(3, 0, 3, 6)
            };
            top.Controls.Add(labelTargets, 0, 0);
            top.SetColumnSpan(labelTargets, 5);

            this.comboCommand.Dock = DockStyle.Fill;
            this.comboCommand.Font = mono;
            this.comboCommand.Items.AddRange(RecentCommands.Cast<object>().ToArray());
            this.comboCommand.TextChanged += delegate { UpdateButtons(); };
            top.Controls.Add(this.comboCommand, 0, 1);

            Label labelTimeout = new Label { Text = "Timeout (s)", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(8, 6, 2, 0) };
            top.Controls.Add(labelTimeout, 1, 1);
            this.numericTimeout.Minimum = 5;
            this.numericTimeout.Maximum = 3600;
            this.numericTimeout.Value = 60;
            this.numericTimeout.Width = 56;
            top.Controls.Add(this.numericTimeout, 2, 1);

            this.buttonRun.Text = "&Run";
            this.buttonRun.Click += delegate { Run(); };
            top.Controls.Add(this.buttonRun, 3, 1);
            this.buttonStop.Text = "&Stop";
            this.buttonStop.Enabled = false;
            this.buttonStop.Click += delegate { if (this.running != null) this.running.Cancel(); };
            top.Controls.Add(this.buttonStop, 4, 1);

            // middle: results above, output of the selected host below
            SplitContainer split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            this.listResults.Dock = DockStyle.Fill;
            this.listResults.View = View.Details;
            this.listResults.FullRowSelect = true;
            this.listResults.HideSelection = false;
            this.listResults.MultiSelect = false;
            this.listResults.CheckBoxes = true;
            this.listResults.SmallImageList = this.stateImages;
            this.listResults.Columns.Add("Host", 170);
            this.listResults.Columns.Add("Result", 115);
            this.listResults.Columns.Add("Time", 55, HorizontalAlignment.Right);
            this.listResults.Columns.Add("Output", 460);
            this.listResults.SelectedIndexChanged += delegate { ShowSelectedOutput(); };
            this.listResults.ItemChecked += listResults_ItemChecked;
            split.Panel1.Controls.Add(this.listResults);

            this.textOutput.Dock = DockStyle.Fill;
            this.textOutput.Multiline = true;
            this.textOutput.ReadOnly = true;
            this.textOutput.ScrollBars = ScrollBars.Both;
            this.textOutput.WordWrap = false;
            this.textOutput.Font = mono;
            this.textOutput.BackColor = SystemColors.Window;
            split.Panel2.Controls.Add(this.textOutput);

            // bottom: summary, filter, copy/save/close
            FlowLayoutPanel bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8, 4, 8, 8) };
            Button buttonClose = new Button { Text = "Close", DialogResult = DialogResult.Cancel };
            buttonClose.Click += delegate { Close(); };
            this.buttonSave.Text = "Save…";
            this.buttonSave.Click += delegate { Save(); };
            this.buttonCopy.Text = "Copy All";
            this.buttonCopy.Click += delegate { CopyAll(); };
            this.checkProblems.Text = "Only failed";
            this.checkProblems.AutoSize = true;
            this.checkProblems.Margin = new Padding(12, 6, 3, 3);
            this.checkProblems.CheckedChanged += delegate { FillList(); };
            this.labelSummary.AutoSize = true;
            this.labelSummary.Margin = new Padding(3, 7, 3, 3);
            bottom.Controls.AddRange(new Control[] { buttonClose, this.buttonSave, this.buttonCopy, this.checkProblems, this.labelSummary });

            this.Controls.Add(split);
            this.Controls.Add(bottom);
            this.Controls.Add(top);
            this.AcceptButton = this.buttonRun;
            this.CancelButton = buttonClose;
            this.Shown += delegate
            {
                split.SplitterDistance = Math.Max(120, split.Height * 3 / 5);
                this.comboCommand.Focus();
            };
            UpdateButtons();
        }

        #endregion

        #region List

        private void FillList()
        {
            HostRunResult selected = SelectedResult;
            this.filling = true;
            this.listResults.BeginUpdate();
            try
            {
                this.listResults.Items.Clear();
                foreach (HostRunResult result in this.results.Where(r => !this.checkProblems.Checked || r.IsProblem))
                {
                    ListViewItem item = new ListViewItem(new[] { result.Host, string.Empty, string.Empty, string.Empty })
                    {
                        Tag = result,
                        Checked = result.State != HostRunState.Skipped && !this.excluded.Contains(result),
                        ToolTipText = result.Session.SessionId
                    };
                    this.listResults.Items.Add(item);
                    UpdateItem(item);
                    item.Selected = result == selected;
                }
            }
            finally
            {
                this.listResults.EndUpdate();
                this.filling = false;
            }
        }

        private void listResults_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (this.filling)
            {
                return;
            }
            HostRunResult result = (HostRunResult)e.Item.Tag;
            if (result.State == HostRunState.Skipped && e.Item.Checked)
            {
                e.Item.Checked = false; // not SSH: can't run
                return;
            }
            if (e.Item.Checked)
            {
                this.excluded.Remove(result);
            }
            else
            {
                this.excluded.Add(result);
            }
            UpdateButtons();
        }

        /// <summary>The hosts a run uses: SSH sessions the user didn't uncheck</summary>
        private List<HostRunResult> Targets
        {
            get { return this.results.Where(r => r.State != HostRunState.Skipped && !this.excluded.Contains(r)).ToList(); }
        }

        private void UpdateItem(ListViewItem item)
        {
            HostRunResult result = (HostRunResult)item.Tag;
            item.ImageKey = result.State.ToString();
            item.SubItems[1].Text = result.StateText;
            item.SubItems[2].Text = result.State == HostRunState.Succeeded || result.State == HostRunState.Failed || result.State == HostRunState.Error
                ? result.Duration.TotalSeconds.ToString("0.0") + " s" : string.Empty;
            item.SubItems[3].Text = result.FirstLine;
            item.ForeColor = result.State == HostRunState.Skipped || result.State == HostRunState.Stopped || result.State == HostRunState.NotRun ? SystemColors.GrayText
                : result.IsProblem ? PaneStyle.Red : SystemColors.WindowText;
        }

        /// <summary>Run on the UI thread: the plink runs finish on pool threads</summary>
        private void OnUi(Action action)
        {
            if (this.IsDisposed || this.Disposing)
            {
                return;
            }
            if (this.InvokeRequired)
            {
                BeginInvoke(action);
            }
            else
            {
                action();
            }
        }

        private void Update(HostRunResult result)
        {
            OnUi(() => UpdateNow(result));
        }

        private void UpdateNow(HostRunResult result)
        {
            if (this.IsDisposed)
            {
                return;
            }
            ListViewItem item = this.listResults.Items.Cast<ListViewItem>().FirstOrDefault(i => i.Tag == result);
            if (item != null)
            {
                UpdateItem(item);
            }
            if (result == SelectedResult)
            {
                ShowSelectedOutput();
            }
            UpdateSummary();
        }

        private HostRunResult SelectedResult
        {
            get { return this.listResults.SelectedItems.Count == 0 ? null : (HostRunResult)this.listResults.SelectedItems[0].Tag; }
        }

        private void ShowSelectedOutput()
        {
            HostRunResult result = SelectedResult;
            this.textOutput.Text = result == null ? string.Empty
                : result.Output.Length > 0 ? result.Output.Replace("\r\n", "\n").Replace("\n", "\r\n")
                : result.IsDone ? "(no output)" : string.Empty;
        }

        private void UpdateSummary()
        {
            // the hosts of the last run
            List<HostRunResult> run = this.results.Where(r => r.State != HostRunState.Skipped && r.State != HostRunState.NotRun).ToList();
            this.labelSummary.Text = this.lastCommand == null ? string.Empty : string.Format("{0} of {1} done · {2} ok · {3} failed · {4} error(s)",
                run.Count(r => r.IsDone), run.Count,
                run.Count(r => r.State == HostRunState.Succeeded), run.Count(r => r.State == HostRunState.Failed), run.Count(r => r.State == HostRunState.Error));
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool busy = this.running != null;
            this.buttonRun.Enabled = !busy && this.comboCommand.Text.Trim().Length > 0 && Targets.Count > 0;
            this.buttonStop.Enabled = busy;
            this.comboCommand.Enabled = !busy;
            this.numericTimeout.Enabled = !busy;
            this.buttonCopy.Enabled = this.buttonSave.Enabled = this.lastCommand != null;
        }

        #endregion

        #region Running

        private async void Run()
        {
            string command = this.comboCommand.Text.Trim();
            List<HostRunResult> targets = Targets;
            if (command.Length == 0 || targets.Count == 0 || this.running != null)
            {
                return;
            }
            if (Plink.FindExecutable() == null)
            {
                MessageBox.Show(this, "plink.exe was not found next to PuTTY or on the PATH. It comes with the PuTTY installer.", "Run on Hosts",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            List<string> prod = targets.Where(r => SessionEnvironment.Of(r.Session) == EnvironmentKind.Prod).Select(r => r.Host).ToList();
            if (prod.Count > 0 && MessageBox.Show(this,
                    string.Format("Run on {0} host(s), {1} of them PRODUCTION:\n{2}\n\n{3}", targets.Count, prod.Count,
                        string.Join(", ", prod.Take(10)) + (prod.Count > 10 ? ", …" : string.Empty), command),
                    "Run on Hosts – production", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.OK)
            {
                return;
            }

            RecentCommands.Remove(command);
            RecentCommands.Insert(0, command);
            if (RecentCommands.Count > 20)
            {
                RecentCommands.RemoveAt(RecentCommands.Count - 1);
            }
            this.comboCommand.Items.Clear();
            this.comboCommand.Items.AddRange(RecentCommands.Cast<object>().ToArray());
            this.comboCommand.Text = command;

            foreach (HostRunResult result in this.results.Where(r => r.State != HostRunState.Skipped))
            {
                result.State = targets.Contains(result) ? HostRunState.Waiting : HostRunState.NotRun;
                result.ExitCode = null;
                result.Output = string.Empty;
                result.Duration = TimeSpan.Zero;
            }
            this.lastCommand = command;
            this.lastStarted = DateTime.Now;
            this.running = new CancellationTokenSource();
            CancellationToken cancel = this.running.Token;
            TimeSpan timeout = TimeSpan.FromSeconds((double)this.numericTimeout.Value);
            FillList();
            UpdateSummary();
            Log.InfoFormat("Run on Hosts: [{0}] on {1} host(s)", command, targets.Count);

            CancellationTokenSource source = this.running;
            using (SemaphoreSlim gate = new SemaphoreSlim(Parallel))
            {
                await Task.WhenAll(targets.Select(result => RunOne(result, command, timeout, gate, cancel)));
            }
            OnUi(() =>
            {
                source.Dispose();
                this.running = null;
                UpdateSummary();
                SuperPuTTY.ReportStatus("Run on Hosts: {0}", this.labelSummary.Text);
            });
        }

        private async Task RunOne(HostRunResult result, string command, TimeSpan timeout, SemaphoreSlim gate, CancellationToken cancel)
        {
            try
            {
                await gate.WaitAsync(cancel);
            }
            catch (OperationCanceledException)
            {
                result.State = HostRunState.Stopped;
                Update(result);
                return;
            }
            try
            {
                result.State = HostRunState.Running;
                Update(result);
                Stopwatch watch = Stopwatch.StartNew();
                PlinkResult plink = await Plink.RunAsync(result.Session, command, timeout, cancel);
                result.Complete(plink, watch.Elapsed);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
            {
                Log.Warn("Run on Hosts failed for " + result.Session.SessionId, ex);
                result.State = HostRunState.Error;
                result.Output = ex.Message;
            }
            finally
            {
                gate.Release();
            }
            Update(result);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (this.running != null)
            {
                this.running.Cancel();
            }
            base.OnFormClosing(e);
        }

        #endregion

        #region Report

        private string Report()
        {
            return HostRunReport.Format(this.lastCommand, this.lastStarted,
                this.results.Where(r => r.State != HostRunState.Skipped && r.State != HostRunState.NotRun && (!this.checkProblems.Checked || r.IsProblem)));
        }

        private void CopyAll()
        {
            if (this.lastCommand != null)
            {
                Clipboard.SetText(Report());
                SuperPuTTY.ReportStatus("Copied the results of {0}", this.lastCommand);
            }
        }

        private void Save()
        {
            if (this.lastCommand == null)
            {
                return;
            }
            using (SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = "run-on-hosts-" + this.lastStarted.ToString("yyyyMMdd-HHmmss") + ".txt"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                try
                {
                    File.WriteAllText(dialog.FileName, Report());
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    MessageBox.Show(this, "Could not save the results:\n" + ex.Message, "Run on Hosts", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        #endregion
    }
}
