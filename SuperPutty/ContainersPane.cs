using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using log4net;
using SuperPutty.Data;
using SuperPutty.Gui;
using SuperPutty.Utils;

namespace SuperPutty
{
    /// <summary>
    /// Tool window listing the docker containers on the active terminal's host (via plink, in the background).
    /// Double-click opens a shell inside a container in a new tab; the context menu follows logs, starts, stops,
    /// restarts and inspects containers.
    /// </summary>
    public partial class ContainersPane : ToolWindow
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ContainersPane));

        private static readonly TimeSpan PlinkTimeout = TimeSpan.FromSeconds(20);

        /// <summary>Returns the session of the active terminal tab, or null</summary>
        private readonly Func<SessionData> activeSession;

        /// <summary>"sudo -n " for hosts where the user can't talk to docker directly, keyed by SessionId</summary>
        private readonly Dictionary<string, string> sudoPrefixes = new Dictionary<string, string>();

        private SessionData session;
        private List<DockerContainer> containers = new List<DockerContainer>();
        private int requestId;

        /// <summary>Where each container row drew its port chips (client coordinates of the last paint), for clicks</summary>
        private readonly Dictionary<TreeNode, List<KeyValuePair<Rectangle, PublishedPort>>> chipAreas =
            new Dictionary<TreeNode, List<KeyValuePair<Rectangle, PublishedPort>>>();

        /// <summary>Compose projects the user collapsed; kept across refreshes</summary>
        private readonly HashSet<string> collapsedProjects = new HashSet<string>();

        /// <summary>Tag of a compose project's header row: counts over all its containers, shown or not</summary>
        private class ProjectGroup
        {
            public string Key { get; set; }
            public string Title { get; set; }
            public int Total { get; set; }
            public int Running { get; set; }
            /// <summary>Unhealthy or failed containers</summary>
            public int Problems { get; set; }
        }

        /// <summary>For the layout restore and the designer</summary>
        public ContainersPane() : this(null) { }

        public ContainersPane(Func<SessionData> activeSession)
        {
            InitializeComponent();
            this.activeSession = activeSession;
            // header rows two units high, container rows (name/status above image/ports) three
            this.treeViewContainers.RowUnits = node => node.Tag is DockerContainer ? 3 : 2;
            this.treeViewContainers.DrawRow += treeViewContainers_DrawRow;
            this.treeViewContainers.MouseMove += treeViewContainers_MouseMove;
            this.treeViewContainers.NodeMouseClick += treeViewContainers_NodeMouseClick;
            this.treeViewContainers.AfterCollapse += (s, e) =>
            {
                ProjectGroup group = e.Node.Tag as ProjectGroup;
                if (group != null)
                {
                    this.collapsedProjects.Add(group.Key);
                }
            };
            this.treeViewContainers.AfterExpand += (s, e) =>
            {
                ProjectGroup group = e.Node.Tag as ProjectGroup;
                if (group != null)
                {
                    this.collapsedProjects.Remove(group.Key);
                }
            };
            // Load fires when the docked window is shown for the first time
            this.Load += delegate { RefreshContainers(); };
        }

        /// <summary>Called by the main form when the active tab changes</summary>
        public void ActiveSessionChanged()
        {
            SessionData current = this.activeSession == null ? null : this.activeSession();
            if (!ReferenceEquals(current, this.session))
            {
                RefreshContainers();
            }
        }

        #region Loading

        private async void RefreshContainers()
        {
            int request = ++this.requestId;
            this.session = this.activeSession == null ? null : this.activeSession();
            if (this.session == null || !RemoteCommandTab.IsSupported(this.session))
            {
                ShowContainers(new List<DockerContainer>(), "No SSH terminal tab is active.");
                this.toolStripLabelHost.Text = string.Empty;
                return;
            }
            if (Plink.FindExecutable() == null)
            {
                ShowContainers(new List<DockerContainer>(), "plink.exe was not found next to PuTTY or on the PATH. It comes with the PuTTY installer.");
                return;
            }

            SessionData target = this.session;
            this.toolStripLabelHost.Text = target.SessionName;
            this.labelStatus.Text = "Loading containers…";
            this.toolStripButtonRefresh.Enabled = false;
            try
            {
                string prefix = ListPrefix(target);
                PlinkResult result = await Plink.RunAsync(target, DockerCommands.List(prefix), PlinkTimeout);
                if (!result.Success && prefix.Length == 0 && DockerCommands.IsPermissionDenied(result.Error + result.Output))
                {
                    // not in the docker group: try sudo without a password prompt
                    result = await Plink.RunAsync(target, DockerCommands.List("sudo -n "), PlinkTimeout);
                    if (result.Success)
                    {
                        this.sudoPrefixes[target.SessionId] = "sudo -n ";
                    }
                }
                if (request != this.requestId)
                {
                    return; // the active tab changed meanwhile
                }

                if (result.Success)
                {
                    List<DockerContainer> list = DockerContainer.ParseList(result.Output);
                    ShowContainers(list, string.Format("{0} container(s), {1} running", list.Count, list.Count(c => c.IsRunning)));
                }
                else
                {
                    ShowContainers(new List<DockerContainer>(), Explain(result));
                }
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is System.ComponentModel.Win32Exception)
            {
                Log.Error("Could not run plink", ex);
                ShowContainers(new List<DockerContainer>(), "Could not run plink: " + ex.Message);
            }
            finally
            {
                if (request == this.requestId)
                {
                    this.toolStripButtonRefresh.Enabled = true;
                }
            }
        }

        /// <summary>A readable reason for a failed listing</summary>
        private static string Explain(PlinkResult result)
        {
            string text = (result.Error + "\n" + result.Output).Trim();
            if (result.TimedOut)
            {
                return "No answer from the host (timeout).";
            }
            if (DockerCommands.IsPermissionDenied(text))
            {
                return "No access to docker on this host: add the user to the 'docker' group, or allow 'sudo docker' without a password.";
            }
            if (text.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0 && text.IndexOf("docker", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Docker is not installed on this host.";
            }
            if (text.IndexOf("Cannot confirm a host key in batch mode", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "The host key is not known yet. Connect once with PuTTY and accept it.";
            }
            if (text.IndexOf("authentication", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Background login failed. The containers list needs key-based login (e.g. Pageant).\n" + FirstLine(text);
            }
            return "Could not list containers: " + FirstLine(text);
        }

        private static string FirstLine(string text)
        {
            string line = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? string.Empty;
            return line.Length > 200 ? line.Substring(0, 200) + "…" : line;
        }

        private void ShowContainers(List<DockerContainer> list, string status)
        {
            this.containers = list;
            this.labelStatus.Text = status;
            FillList();
        }

        private void FillList()
        {
            string selected = SelectedContainer == null ? null : SelectedContainer.Name;
            bool showStopped = this.toolStripButtonShowStopped.Checked;

            this.treeViewContainers.BeginUpdate();
            try
            {
                this.treeViewContainers.Nodes.Clear();
                this.chipAreas.Clear();
                foreach (IGrouping<string, DockerContainer> project in this.containers
                    .OrderBy(c => string.IsNullOrEmpty(c.Project) ? 1 : 0).ThenBy(c => c.Project).ThenBy(c => c.Name)
                    .GroupBy(c => c.Project ?? string.Empty))
                {
                    List<DockerContainer> shown = project.Where(c => showStopped || c.IsRunning).ToList();
                    if (shown.Count == 0)
                    {
                        continue;
                    }
                    ProjectGroup group = new ProjectGroup
                    {
                        Key = project.Key,
                        Title = string.IsNullOrEmpty(project.Key) ? "(no compose project)" : project.Key,
                        Total = project.Count(),
                        Running = project.Count(c => c.IsRunning),
                        Problems = project.Count(c => c.State == ContainerState.Unhealthy || c.State == ContainerState.Failed)
                    };
                    TreeNode groupNode = this.treeViewContainers.Nodes.Add(group.Title);
                    groupNode.Tag = group;
                    foreach (DockerContainer container in shown)
                    {
                        // Text is what the tree's type-ahead search matches
                        TreeNode node = groupNode.Nodes.Add(container.ShortName);
                        node.Tag = container;
                        node.ToolTipText = ToolTip(container);
                        if (container.Name == selected)
                        {
                            this.treeViewContainers.SelectedNode = node;
                        }
                    }
                    if (!this.collapsedProjects.Contains(group.Key))
                    {
                        groupNode.Expand();
                    }
                }
                this.treeViewContainers.ApplyRowHeights();
            }
            finally
            {
                this.treeViewContainers.EndUpdate();
            }
        }

        private static string ToolTip(DockerContainer container)
        {
            List<string> lines = new List<string> { container.Name, container.Image, container.Status };
            List<string> ports = container.PublishedPorts;
            if (ports.Count > 0)
            {
                lines.Add("Ports: " + string.Join(", ", ports) + " (click one to open it in the browser)");
            }
            return string.Join("\n", lines);
        }

        private string ListPrefix(SessionData target)
        {
            string prefix;
            return this.sudoPrefixes.TryGetValue(target.SessionId, out prefix) ? prefix : string.Empty;
        }

        /// <summary>Interactive commands in a tab may prompt for the sudo password, so they use plain "sudo "</summary>
        private string TabPrefix(SessionData target)
        {
            return ListPrefix(target).Length > 0 ? "sudo " : string.Empty;
        }

        #endregion

        #region Drawing

        private void treeViewContainers_DrawRow(object sender, TreeRowDrawEventArgs e)
        {
            DockerContainer container = e.Node.Tag as DockerContainer;
            if (container != null)
            {
                DrawContainer(e, container);
            }
            else if (e.Node.Tag is ProjectGroup)
            {
                DrawGroup(e, (ProjectGroup)e.Node.Tag);
            }
        }

        /// <summary>Project name, then "3/4 running" at the right (problems in red before it)</summary>
        private void DrawGroup(TreeRowDrawEventArgs e, ProjectGroup group)
        {
            OwnerDrawTreeView tree = this.treeViewContainers;
            Graphics g = e.Graphics;
            Rectangle b = e.Bounds;
            if (e.Node.Index > 0)
            {
                PaneStyle.DrawRule(g, e.RowBounds.Left + 4, e.RowBounds.Right - 4, e.RowBounds.Top, tree.BackColor);
            }

            string summary = string.Format("{0}/{1} running", group.Running, group.Total);
            int right = b.Right;
            int width = PaneStyle.MeasureText(g, summary, tree.SmallFont);
            PaneStyle.DrawText(g, summary, tree.SmallFont, Rectangle.FromLTRB(right - width, b.Top, right, b.Bottom), e.SubtleColor);
            right -= width + 6;
            if (group.Problems > 0)
            {
                string problems = group.Problems == 1 ? "1 problem" : group.Problems + " problems";
                width = PaneStyle.MeasureText(g, problems, tree.SmallFont);
                PaneStyle.DrawText(g, problems, tree.SmallFont, Rectangle.FromLTRB(right - width, b.Top, right, b.Bottom), PaneStyle.Red);
                right -= width + 6;
            }
            PaneStyle.DrawText(g, group.Title, tree.BoldFont, Rectangle.FromLTRB(b.Left, b.Top, right, b.Bottom), e.ForeColor);
        }

        /// <summary>
        /// Line 1: status dot, short name (bold), status at the right.
        /// Line 2: image (grey), published ports as chips at the right.
        /// </summary>
        private void DrawContainer(TreeRowDrawEventArgs e, DockerContainer container)
        {
            OwnerDrawTreeView tree = this.treeViewContainers;
            Graphics g = e.Graphics;
            Rectangle b = e.Bounds;
            int lineHeight = (b.Height - 6) / 2;
            Rectangle line1 = new Rectangle(b.Left, b.Top + 3, b.Width, lineHeight);
            Rectangle line2 = new Rectangle(b.Left, line1.Bottom, b.Width, lineHeight);
            ContainerState state = container.State;

            PaneStyle.DrawDot(g, new Point(b.Left + 5, line1.Top + line1.Height / 2), 8, PaneStyle.StateColor(state));
            int textLeft = b.Left + 14;

            // status at the right, at most half the width; coloured when something needs attention
            Color statusColor = state == ContainerState.Unhealthy || state == ContainerState.Failed ? PaneStyle.Red
                : state == ContainerState.Starting || state == ContainerState.Restarting || state == ContainerState.Paused ? PaneStyle.Amber
                : e.SubtleColor;
            int statusWidth = Math.Min(PaneStyle.MeasureText(g, container.DisplayStatus, tree.SmallFont), (b.Right - textLeft) / 2);
            PaneStyle.DrawText(g, container.DisplayStatus, tree.SmallFont,
                Rectangle.FromLTRB(b.Right - statusWidth, line1.Top, b.Right, line1.Bottom), statusColor, TextFormatFlags.Right);
            PaneStyle.DrawText(g, container.ShortName, tree.BoldFont,
                Rectangle.FromLTRB(textLeft, line1.Top, b.Right - statusWidth - 8, line1.Bottom), container.IsRunning ? e.ForeColor : e.SubtleColor);

            // ports from the right, as long as they leave 40 % of the width for the image, the rest as "+N"
            List<PublishedPort> ports = container.PublishedPortList;
            List<KeyValuePair<Rectangle, PublishedPort>> areas = new List<KeyValuePair<Rectangle, PublishedPort>>();
            this.chipAreas[e.Node] = areas;
            int chipRight = b.Right;
            int chipLimit = textLeft + (b.Right - textLeft) * 2 / 5;
            int chipTop = line2.Top + 1;
            int chipHeight = line2.Height - 2;
            for (int i = ports.Count - 1; i >= 0; i--)
            {
                int width = PaneStyle.ChipWidth(g, ports[i].Text, tree.SmallFont);
                int reserve = i > 0 ? PaneStyle.ChipWidth(g, "+" + i, tree.SmallFont) + 3 : 0;
                if (chipRight - width - reserve < chipLimit)
                {
                    string rest = "+" + (i + 1);
                    int restWidth = PaneStyle.ChipWidth(g, rest, tree.SmallFont);
                    if (chipRight - restWidth >= chipLimit)
                    {
                        PaneStyle.DrawChip(g, new Rectangle(chipRight - restWidth, chipTop, restWidth, chipHeight), rest, tree.SmallFont, tree.BackColor, e.SubtleColor);
                        chipRight -= restWidth + 3;
                    }
                    break;
                }
                Rectangle chip = new Rectangle(chipRight - width, chipTop, width, chipHeight);
                PaneStyle.DrawChip(g, chip, ports[i].Text, tree.SmallFont, tree.BackColor, e.ForeColor);
                areas.Add(new KeyValuePair<Rectangle, PublishedPort>(chip, ports[i]));
                chipRight -= width + 3;
            }
            PaneStyle.DrawText(g, container.Image, tree.SmallFont, Rectangle.FromLTRB(textLeft, line2.Top, chipRight - 5, line2.Bottom), e.SubtleColor);
        }

        #endregion

        #region Actions

        private DockerContainer SelectedContainer
        {
            get { return this.treeViewContainers.SelectedNode == null ? null : this.treeViewContainers.SelectedNode.Tag as DockerContainer; }
        }

        private void OpenTab(string command, string what)
        {
            DockerContainer container = SelectedContainer;
            if (container == null || this.session == null)
            {
                return;
            }
            RemoteCommandTab.Open(this.session, command, string.Format("{0}: {1}{2}", this.session.SessionName, container.Name, what));
        }

        private void shellToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedContainer != null && !SelectedContainer.IsRunning)
            {
                SuperPuTTY.ReportStatus("Container {0} is not running", SelectedContainer.Name);
            }
            else if (SelectedContainer != null)
            {
                OpenTab(DockerCommands.Shell(TabPrefix(this.session), SelectedContainer.Name, false), string.Empty);
            }
        }

        private void rootShellToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedContainer != null)
            {
                OpenTab(DockerCommands.Shell(TabPrefix(this.session), SelectedContainer.Name, true), " (root)");
            }
        }

        private void logsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedContainer != null)
            {
                // keep a shell after Ctrl+C so the log stays readable
                OpenTab(RemoteCommandTab.KeepShellAfter(DockerCommands.FollowLogs(TabPrefix(this.session), SelectedContainer.Name)), " logs");
            }
        }

        private void inspectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedContainer != null)
            {
                OpenTab(DockerCommands.Inspect(TabPrefix(this.session), SelectedContainer.Name), " inspect");
            }
        }

        private void startToolStripMenuItem_Click(object sender, EventArgs e) { RunAction("start", false); }
        private void stopToolStripMenuItem_Click(object sender, EventArgs e) { RunAction("stop", true); }
        private void restartToolStripMenuItem_Click(object sender, EventArgs e) { RunAction("restart", true); }

        /// <summary>Run docker start/stop/restart in the background, then reload the list</summary>
        private async void RunAction(string verb, bool confirm)
        {
            DockerContainer container = SelectedContainer;
            SessionData target = this.session;
            if (container == null || target == null)
            {
                return;
            }
            if (confirm && MessageBox.Show(this, string.Format("{0} container {1} on {2}?", Capitalize(verb), container.Name, target.SessionName),
                "Containers", MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.OK)
            {
                return;
            }

            this.labelStatus.Text = string.Format("{0} {1}…", Capitalize(verb), container.Name);
            PlinkResult result = await Plink.RunAsync(target, DockerCommands.Action(ListPrefix(target), verb, container.Name), TimeSpan.FromSeconds(60));
            SuperPuTTY.ReportStatus(result.Success
                ? string.Format("docker {0} {1}: done", verb, container.Name)
                : string.Format("docker {0} {1} failed: {2}", verb, container.Name, FirstLine(result.Error + "\n" + result.Output)));
            RefreshContainers();
        }

        private static string Capitalize(string text)
        {
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        private void copyNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedContainer != null)
            {
                Clipboard.SetText(SelectedContainer.Name);
            }
        }

        /// <summary>The openable port chip at a point of the tree, or null</summary>
        private PublishedPort ChipAt(Point location)
        {
            TreeNode node = this.treeViewContainers.GetNodeAt(location);
            List<KeyValuePair<Rectangle, PublishedPort>> areas;
            if (node == null || !this.chipAreas.TryGetValue(node, out areas))
            {
                return null;
            }
            foreach (KeyValuePair<Rectangle, PublishedPort> area in areas)
            {
                if (area.Key.Contains(location) && area.Value.CanOpen)
                {
                    return area.Value;
                }
            }
            return null;
        }

        private void treeViewContainers_MouseMove(object sender, MouseEventArgs e)
        {
            Cursor cursor = ChipAt(e.Location) != null ? Cursors.Hand : Cursors.Default;
            if (this.treeViewContainers.Cursor != cursor)
            {
                this.treeViewContainers.Cursor = cursor;
            }
        }

        private void treeViewContainers_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            PublishedPort port = e.Button == MouseButtons.Left ? ChipAt(e.Location) : null;
            if (port != null)
            {
                OpenPort(port);
            }
        }

        /// <summary>Open a published port of the active host in the browser (directly or through an SSH tunnel)</summary>
        private async void OpenPort(PublishedPort port)
        {
            SessionData target = this.session;
            if (target == null)
            {
                return;
            }
            this.labelStatus.Text = "Opening " + port.Text + "…";
            string result;
            try
            {
                result = await PortOpener.OpenAsync(target, port);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is FileNotFoundException || ex is System.ComponentModel.Win32Exception)
            {
                Log.Warn("Could not open port " + port.Text, ex);
                result = "Could not open " + port.Text + ": " + ex.Message;
            }
            this.labelStatus.Text = result;
            SuperPuTTY.ReportStatus(result);
        }

        private void treeViewContainers_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (ChipAt(e.Location) != null)
            {
                return; // the click already opened the port
            }
            // on a project header the tree itself expands/collapses
            if (e.Button == MouseButtons.Left && e.Node.Tag is DockerContainer)
            {
                shellToolStripMenuItem_Click(sender, e);
            }
        }

        private void treeViewContainers_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && SelectedContainer != null)
            {
                shellToolStripMenuItem_Click(sender, e);
                e.Handled = e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                RefreshContainers();
                e.Handled = true;
            }
        }

        private void contextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            DockerContainer container = SelectedContainer;
            if (container == null)
            {
                e.Cancel = true;
                return;
            }
            this.shellToolStripMenuItem.Enabled = container.IsRunning;
            this.rootShellToolStripMenuItem.Enabled = container.IsRunning;
            this.startToolStripMenuItem.Enabled = !container.IsRunning;
            this.stopToolStripMenuItem.Enabled = container.IsRunning;
            this.restartToolStripMenuItem.Enabled = container.IsRunning;

            // one entry per openable port
            this.openPortToolStripMenuItem.DropDownItems.Clear();
            foreach (PublishedPort port in container.PublishedPortList.Where(p => p.CanOpen))
            {
                PublishedPort open = port;
                this.openPortToolStripMenuItem.DropDownItems.Add(port.Text + (port.NeedsTunnel ? "  (via SSH tunnel)" : string.Empty), null, delegate { OpenPort(open); });
            }
            this.openPortToolStripMenuItem.Enabled = container.IsRunning && this.openPortToolStripMenuItem.DropDownItems.Count > 0;
        }

        private void toolStripButtonRefresh_Click(object sender, EventArgs e)
        {
            RefreshContainers();
        }

        private void toolStripButtonShowStopped_Click(object sender, EventArgs e)
        {
            FillList();
        }

        #endregion
    }
}
