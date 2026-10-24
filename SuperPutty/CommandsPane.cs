using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using log4net;
using SuperPutty.Data;
using SuperPutty.Gui;

namespace SuperPutty
{
    /// <summary>
    /// Tool window with reusable commands, grouped by category. A single click types the command into the active
    /// terminal, a double click types and runs it. The single click waits for the double-click time so a double
    /// click doesn't type the command twice. Right-click selects without typing (for editing).
    /// Commands apply to all sessions, to a folder of the session tree or to one session; the pane shows the
    /// commands for the active tab in sections ("This session", its folders, "All sessions"), flat when only
    /// global commands exist. Commands of sessions or folders that no longer exist are listed as "Not assigned".
    /// </summary>
    public partial class CommandsPane : ToolWindow
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CommandsPane));

        /// <summary>Sends a command to the active terminal: (command, execute) => sent</summary>
        private readonly Func<string, bool, bool> sendToTerminal;

        /// <summary>Runs a command in all terminals that accept commands: command => number of terminals</summary>
        private readonly Func<string, int> runInAllTerminals;

        /// <summary>Runs a command in a new tab on the active terminal's host: command => opened</summary>
        private readonly Func<string, bool> runInNewTab;

        /// <summary>Returns the session of the active terminal tab, or null</summary>
        private readonly Func<SessionData> activeSession;

        /// <summary>Runs a command in the open tabs below a session-tree folder: (command, folder) => number of terminals</summary>
        private readonly Func<string, string, int> runInFolder;

        private readonly Timer singleClickTimer = new Timer();
        private CommandSnippet pendingSingleClick;

        /// <summary>The session whose commands are shown (ScopeSessionId of the active tab), or null</summary>
        private string shownSessionId;

        /// <summary>A group of commands in the tree: a scope (session, folder, all) or the unassigned ones</summary>
        private class Section
        {
            public string Title { get; set; }
            /// <summary>Scope for commands added here; null for "Not assigned"</summary>
            public CommandScopeOption Scope { get; set; }
            public Func<CommandSnippet, bool> Includes { get; set; }
            /// <summary>Show the Favorites category even when empty (only for "All sessions")</summary>
            public bool ShowEmptyFavorites { get; set; }
        }

        /// <summary>Tag of section and category nodes</summary>
        private class GroupTag
        {
            public Section Section { get; set; }
            public string Category { get; set; }
        }

        private static CommandLibrary Library { get { return CommandStore.Library; } }

        /// <summary>For the layout restore and the designer; commands can't be sent without a sender</summary>
        public CommandsPane() : this(null, null, null, null, null) { }

        public CommandsPane(Func<string, bool, bool> sendToTerminal, Func<string, int> runInAllTerminals, Func<string, bool> runInNewTab,
            Func<SessionData> activeSession, Func<string, string, int> runInFolder)
        {
            InitializeComponent();
            this.sendToTerminal = sendToTerminal;
            this.runInAllTerminals = runInAllTerminals;
            this.runInNewTab = runInNewTab;
            this.activeSession = activeSession;
            this.runInFolder = runInFolder;
            this.contextMenuStrip.Opening += delegate { FillRunInFolderMenu(); };
            // headers and unnamed commands two units high, named commands (name above command) three
            this.treeViewCommands.RowUnits = node =>
            {
                CommandSnippet command = node.Tag as CommandSnippet;
                return command != null && !string.IsNullOrEmpty(command.Name) ? 3 : 2;
            };
            this.treeViewCommands.DrawRow += treeViewCommands_DrawRow;

            this.singleClickTimer.Interval = SystemInformation.DoubleClickTime;
            this.singleClickTimer.Tick += singleClickTimer_Tick;

            CommandStore.Changed += CommandStore_Changed;
            this.shownSessionId = ActiveSessionId();
            RefreshTree();
        }

        /// <summary>Called by the main form when the active tab changes</summary>
        public void ActiveSessionChanged()
        {
            string sessionId = ActiveSessionId();
            if (sessionId != this.shownSessionId)
            {
                this.shownSessionId = sessionId;
                RefreshTree();
            }
        }

        private string ActiveSessionId()
        {
            SessionData session = this.activeSession == null ? null : this.activeSession();
            return session == null ? null : session.ScopeSessionId;
        }

        private void CommandStore_Changed(object sender, EventArgs e)
        {
            if (this.IsDisposed)
            {
                return;
            }
            if (this.InvokeRequired)
            {
                this.BeginInvoke((MethodInvoker)delegate { RefreshTree(); });
            }
            else
            {
                RefreshTree();
            }
        }

        private void SaveLibrary()
        {
            try
            {
                CommandStore.Save();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.Error("Could not save commands to " + CommandStore.LibraryPath, ex);
                MessageBox.Show(this, "Could not save the commands:\n" + ex.Message, "Commands", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        #region Tree

        /// <summary>Where a command may apply: all sessions, every folder, every session of the session tree</summary>
        private static List<CommandScopeOption> ScopeOptions()
        {
            return CommandLibrary.ScopeOptions(SuperPuTTY.GetAllSessions().Select(s => s.SessionId));
        }

        /// <summary>The sections for the active tab, most specific first</summary>
        private List<Section> Sections()
        {
            List<Section> sections = new List<Section>();
            string sessionId = this.shownSessionId;
            if (sessionId != null)
            {
                SessionData session = SuperPuTTY.GetSessionById(sessionId);
                sections.Add(new Section
                {
                    Title = "This session: " + (session == null ? sessionId : session.SessionName),
                    Scope = CommandScopeOption.Session(sessionId),
                    Includes = c => c.Scope == CommandScope.Session && c.ScopeKey == sessionId
                });
                foreach (string folder in CommandLibrary.FolderChain(sessionId))
                {
                    string key = folder;
                    sections.Add(new Section
                    {
                        Title = "Folder: " + key,
                        Scope = CommandScopeOption.Folder(key),
                        Includes = c => c.Scope == CommandScope.Folder && c.ScopeKey == key
                    });
                }
            }
            sections.Add(new Section
            {
                Title = "All sessions",
                Scope = CommandScopeOption.Global(),
                Includes = c => c.Scope == CommandScope.Global,
                ShowEmptyFavorites = true
            });

            List<string> sessionIds = SuperPuTTY.GetAllSessions().Select(s => s.SessionId).ToList();
            sections.Add(new Section
            {
                Title = "Not assigned (session or folder no longer exists)",
                Includes = c => CommandLibrary.IsOrphan(c, sessionIds)
            });
            return sections;
        }

        /// <summary>Rebuild the tree from the library and the filter, keeping the selection where possible</summary>
        private void RefreshTree(CommandSnippet select = null)
        {
            select = select ?? SelectedCommand;
            string filter = this.textBoxFilter.Text;
            bool filtering = !string.IsNullOrWhiteSpace(filter);

            // matching commands per section; sections without matches are left out (except "All sessions")
            List<KeyValuePair<Section, List<CommandSnippet>>> shown = new List<KeyValuePair<Section, List<CommandSnippet>>>();
            foreach (Section section in Sections())
            {
                List<CommandSnippet> commands = Library.Commands.Where(c => section.Includes(c) && c.Matches(filter)).ToList();
                if (commands.Count > 0 || (section.ShowEmptyFavorites && !filtering))
                {
                    shown.Add(new KeyValuePair<Section, List<CommandSnippet>>(section, commands));
                }
            }
            // only global commands: no section level, as before
            bool flat = shown.Count == 1 && shown[0].Key.ShowEmptyFavorites;

            this.treeViewCommands.BeginUpdate();
            try
            {
                this.treeViewCommands.Nodes.Clear();
                foreach (KeyValuePair<Section, List<CommandSnippet>> entry in shown)
                {
                    TreeNodeCollection parent = this.treeViewCommands.Nodes;
                    if (!flat)
                    {
                        TreeNode sectionNode = this.treeViewCommands.Nodes.Add(entry.Key.Title);
                        sectionNode.Tag = new GroupTag { Section = entry.Key };
                        parent = sectionNode.Nodes;
                    }
                    AddCategories(parent, entry.Key, entry.Value, entry.Key.ShowEmptyFavorites && !filtering, select);
                }
                this.treeViewCommands.ExpandAll();
                this.treeViewCommands.ApplyRowHeights();
            }
            finally
            {
                this.treeViewCommands.EndUpdate();
            }
            UpdateButtons();
        }

        private void AddCategories(TreeNodeCollection parent, Section section, List<CommandSnippet> commands, bool showEmptyFavorites, CommandSnippet select)
        {
            List<string> categories = new List<string>();
            if (showEmptyFavorites || commands.Any(c => CommandLibrary.IsFavorites(CommandLibrary.CategoryOf(c))))
            {
                categories.Add(CommandLibrary.FavoritesCategory);
            }
            categories.AddRange(commands.Select(c => CommandLibrary.CategoryOf(c))
                .Where(c => !CommandLibrary.IsFavorites(c)).Distinct(StringComparer.OrdinalIgnoreCase));

            foreach (string category in categories)
            {
                TreeNode categoryNode = parent.Add(category);
                categoryNode.Tag = new GroupTag { Section = section, Category = category };
                foreach (CommandSnippet command in commands.Where(c => CommandLibrary.CategoryOf(c).Equals(category, StringComparison.OrdinalIgnoreCase)))
                {
                    TreeNode node = categoryNode.Nodes.Add(command.DisplayText);
                    node.Tag = command;
                    node.ToolTipText = string.IsNullOrEmpty(command.Description) ? command.Command : command.Command + "\n" + command.Description;
                    if (command == select)
                    {
                        this.treeViewCommands.SelectedNode = node;
                    }
                }
            }
        }

        /// <summary>The section/category of the selected node (for a command: of its category node)</summary>
        private GroupTag SelectedGroup
        {
            get
            {
                TreeNode node = this.treeViewCommands.SelectedNode;
                while (node != null && !(node.Tag is GroupTag))
                {
                    node = node.Parent;
                }
                return node == null ? null : (GroupTag)node.Tag;
            }
        }

        private CommandSnippet SelectedCommand
        {
            get { return this.treeViewCommands.SelectedNode == null ? null : this.treeViewCommands.SelectedNode.Tag as CommandSnippet; }
        }

        private void UpdateButtons()
        {
            bool hasCommand = SelectedCommand != null;
            this.toolStripButtonEdit.Enabled = hasCommand;
            this.toolStripButtonDelete.Enabled = hasCommand;
            this.typeToolStripMenuItem.Enabled = hasCommand;
            this.runToolStripMenuItem.Enabled = hasCommand;
            this.runInAllToolStripMenuItem.Enabled = hasCommand && this.runInAllTerminals != null;
            this.runInNewTabToolStripMenuItem.Enabled = hasCommand && this.runInNewTab != null;
            this.editToolStripMenuItem.Enabled = hasCommand;
            this.addToFavoritesToolStripMenuItem.Enabled = hasCommand && !CommandLibrary.IsFavorites(CommandLibrary.CategoryOf(SelectedCommand));
            this.deleteToolStripMenuItem.Enabled = hasCommand;
            this.moveUpToolStripMenuItem.Enabled = hasCommand;
            this.moveDownToolStripMenuItem.Enabled = hasCommand;
        }

        private void textBoxFilter_TextChanged(object sender, EventArgs e)
        {
            RefreshTree();
        }

        private void treeViewCommands_AfterSelect(object sender, TreeViewEventArgs e)
        {
            UpdateButtons();
        }

        #endregion

        #region Drawing

        private void treeViewCommands_DrawRow(object sender, TreeRowDrawEventArgs e)
        {
            CommandSnippet command = e.Node.Tag as CommandSnippet;
            GroupTag group = e.Node.Tag as GroupTag;
            if (command != null)
            {
                DrawCommand(e, command);
            }
            else if (group != null && group.Category == null)
            {
                DrawSection(e, group.Section);
            }
            else if (group != null)
            {
                DrawCategory(e, group.Category);
            }
        }

        /// <summary>Section ("This session", "Folder …", "All sessions"): grey bold title, count, rule below</summary>
        private void DrawSection(TreeRowDrawEventArgs e, Section section)
        {
            OwnerDrawTreeView tree = this.treeViewCommands;
            int count = e.Node.Nodes.Cast<TreeNode>().Sum(category => category.Nodes.Count);
            int right = DrawCount(e, count);
            PaneStyle.DrawText(e.Graphics, section.Title, tree.BoldFont, Rectangle.FromLTRB(e.Bounds.Left, e.Bounds.Top, right, e.Bounds.Bottom), e.SubtleColor);
            PaneStyle.DrawRule(e.Graphics, e.Bounds.Left, e.RowBounds.Right - 4, e.RowBounds.Bottom - 2, tree.BackColor);
        }

        /// <summary>Category: a star for Favorites, bold name, count</summary>
        private void DrawCategory(TreeRowDrawEventArgs e, string category)
        {
            OwnerDrawTreeView tree = this.treeViewCommands;
            Rectangle b = e.Bounds;
            int left = b.Left;
            if (CommandLibrary.IsFavorites(category))
            {
                PaneStyle.DrawStar(e.Graphics, new Point(left + 6, b.Top + b.Height / 2), 6, PaneStyle.Gold);
                left += 16;
            }
            int right = DrawCount(e, e.Node.Nodes.Count);
            PaneStyle.DrawText(e.Graphics, category, tree.BoldFont, Rectangle.FromLTRB(left, b.Top, right, b.Bottom), e.ForeColor);
        }

        /// <summary>The number of commands at the right of a header; returns where the title must end</summary>
        private int DrawCount(TreeRowDrawEventArgs e, int count)
        {
            string text = count.ToString();
            int width = PaneStyle.MeasureText(e.Graphics, text, this.treeViewCommands.SmallFont);
            PaneStyle.DrawText(e.Graphics, text, this.treeViewCommands.SmallFont,
                Rectangle.FromLTRB(e.Bounds.Right - width, e.Bounds.Top, e.Bounds.Right, e.Bounds.Bottom), e.SubtleColor);
            return e.Bounds.Right - width - 8;
        }

        /// <summary>
        /// Named command: name, below it the command in grey monospace. Unnamed: the command only.
        /// Placeholders are highlighted; the row under the mouse shows a run marker at the right.
        /// </summary>
        private void DrawCommand(TreeRowDrawEventArgs e, CommandSnippet command)
        {
            OwnerDrawTreeView tree = this.treeViewCommands;
            Rectangle b = e.Bounds;
            int right = b.Right;
            if (e.Hot)
            {
                PaneStyle.DrawPlay(e.Graphics, Rectangle.FromLTRB(right - 12, b.Top, right, b.Bottom), PaneStyle.Green);
                right -= 18;
            }
            if (string.IsNullOrEmpty(command.Name))
            {
                DrawCommandText(e.Graphics, command.Command, Rectangle.FromLTRB(b.Left, b.Top, right, b.Bottom), e.ForeColor);
                return;
            }
            int lineHeight = (b.Height - 6) / 2;
            Rectangle line1 = Rectangle.FromLTRB(b.Left, b.Top + 3, right, b.Top + 3 + lineHeight);
            Rectangle line2 = Rectangle.FromLTRB(b.Left, line1.Bottom, right, line1.Bottom + lineHeight);
            PaneStyle.DrawText(e.Graphics, command.Name, tree.Font, line1, e.ForeColor);
            DrawCommandText(e.Graphics, command.Command, line2, e.SubtleColor);
        }

        /// <summary>
        /// The first line of a command in the monospace font, placeholders (without their defaults) in the accent
        /// colour; too long a command loses its middle, so the differing end stays visible
        /// </summary>
        private void DrawCommandText(Graphics g, string command, Rectangle bounds, Color color)
        {
            Font font = this.treeViewCommands.MonoFont;
            string text = command ?? string.Empty;
            int newline = text.IndexOfAny(new[] { '\r', '\n' });
            if (newline >= 0)
            {
                text = text.Substring(0, newline) + " …";
            }
            // monospace: every character has the same advance, so segments can be placed by character count
            double advance = PaneStyle.MeasureText(g, new string('m', 50), font) / 50.0;
            int fits = advance <= 0 ? 0 : (int)(bounds.Width / advance);
            text = CommandPlaceholders.WithoutDefaults(text);
            if (fits <= 0)
            {
                return;
            }
            if (text.Length <= fits)
            {
                DrawSegments(g, text, font, bounds, advance, 0, color);
                return;
            }
            // cut out the middle; the gap is drawn as centred dots (a baseline "…" reads like "_" in a command)
            int head = (fits - 1) * 2 / 5;
            int tail = fits - 1 - head;
            DrawSegments(g, text.Substring(0, head), font, bounds, advance, 0, color);
            PaneStyle.DrawGap(g, new Rectangle(bounds.Left + (int)Math.Round(head * advance), bounds.Top, (int)Math.Ceiling(advance), bounds.Height), color);
            DrawSegments(g, text.Substring(text.Length - tail), font, bounds, advance, head + 1, color);
        }

        /// <summary>Monospace text from the given column on, placeholders in the accent colour</summary>
        private static void DrawSegments(Graphics g, string text, Font font, Rectangle bounds, double advance, int column, Color color)
        {
            foreach (KeyValuePair<string, bool> segment in CommandPlaceholders.Segments(text))
            {
                Rectangle at = Rectangle.FromLTRB(bounds.Left + (int)Math.Round(column * advance), bounds.Top, bounds.Right, bounds.Bottom);
                TextRenderer.DrawText(g, segment.Key, font, at, segment.Value ? PaneStyle.Accent : color,
                    TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
                column += segment.Key.Length;
            }
        }

        #endregion

        #region Sending

        private void treeViewCommands_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            // select on any button; right-click only selects (context menu follows)
            this.treeViewCommands.SelectedNode = e.Node;
            CommandSnippet command = e.Node.Tag as CommandSnippet;
            if (e.Button != MouseButtons.Left || command == null || this.singleClickTimer.Enabled)
            {
                return;
            }

            // wait: a double click must not type the command twice
            this.pendingSingleClick = command;
            this.singleClickTimer.Start();
        }

        private void treeViewCommands_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            CommandSnippet command = e.Node.Tag as CommandSnippet;
            if (e.Button != MouseButtons.Left || command == null)
            {
                return;
            }
            this.singleClickTimer.Stop();
            this.pendingSingleClick = null;
            Send(command, true);
        }

        private void singleClickTimer_Tick(object sender, EventArgs e)
        {
            this.singleClickTimer.Stop();
            CommandSnippet command = this.pendingSingleClick;
            this.pendingSingleClick = null;
            if (command != null)
            {
                Send(command, false);
            }
        }

        private void treeViewCommands_KeyDown(object sender, KeyEventArgs e)
        {
            CommandSnippet command = SelectedCommand;
            if (command == null)
            {
                return;
            }
            if (e.KeyCode == Keys.Enter)
            {
                // Enter runs, Shift+Enter only types
                Send(command, !e.Shift);
                e.Handled = e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                DeleteSelected();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F2)
            {
                EditSelected();
                e.Handled = true;
            }
            else if (e.Control && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down))
            {
                MoveSelected(e.KeyCode == Keys.Up ? -1 : 1);
                e.Handled = true;
            }
        }

        private void Send(CommandSnippet command, bool execute)
        {
            string text = Resolve(command);
            if (text == null)
            {
                return;
            }
            if (this.sendToTerminal == null || !this.sendToTerminal(text, execute))
            {
                SuperPuTTY.ReportStatus("No terminal tab is active to send the command to");
            }
        }

        /// <summary>
        /// The command text with its placeholders filled in: asks for the values (preset with the values last
        /// used in the active session, or the defaults) and remembers them. Null if cancelled.
        /// </summary>
        private string Resolve(CommandSnippet command)
        {
            List<CommandPlaceholder> placeholders = CommandPlaceholders.Parse(command.Command);
            if (placeholders.Count == 0)
            {
                return command.Command;
            }

            Dictionary<string, string> initial = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (CommandPlaceholder placeholder in placeholders)
            {
                string value = Library.GetValue(this.shownSessionId, placeholder.Name);
                if (value != null)
                {
                    initial[placeholder.Name] = value;
                }
            }
            SessionData session = this.shownSessionId == null ? null : SuperPuTTY.GetSessionById(this.shownSessionId);
            using (dlgPlaceholderValues dialog = new dlgPlaceholderValues(command.Command, placeholders, initial, session == null ? null : session.SessionName))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return null;
                }
                foreach (KeyValuePair<string, string> value in dialog.Values)
                {
                    Library.SetValue(this.shownSessionId, value.Key, value.Value);
                }
                SaveLibrary();
                return dialog.Result;
            }
        }

        private void typeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedCommand != null) { Send(SelectedCommand, false); }
        }

        private void runToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedCommand != null) { Send(SelectedCommand, true); }
        }

        private void runInNewTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string text = SelectedCommand == null || this.runInNewTab == null ? null : Resolve(SelectedCommand);
            if (text != null)
            {
                this.runInNewTab(text);
            }
        }

        /// <summary>One submenu entry per folder of the active tab's session</summary>
        private void FillRunInFolderMenu()
        {
            this.runInFolderToolStripMenuItem.DropDownItems.Clear();
            List<string> folders = this.shownSessionId == null ? new List<string>() : CommandLibrary.FolderChain(this.shownSessionId);
            foreach (string folder in folders)
            {
                string target = folder;
                this.runInFolderToolStripMenuItem.DropDownItems.Add(target, null, delegate
                {
                    string text = SelectedCommand == null ? null : Resolve(SelectedCommand);
                    if (text != null)
                    {
                        this.runInFolder(text, target);
                    }
                });
            }
            this.runInFolderToolStripMenuItem.Enabled = SelectedCommand != null && this.runInFolder != null && folders.Count > 0;
        }

        private void runInAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // the values are asked once (preset from the active session) and used for every terminal
            string text = SelectedCommand == null || this.runInAllTerminals == null ? null : Resolve(SelectedCommand);
            if (text != null)
            {
                this.runInAllTerminals(text);
            }
        }

        #endregion

        #region Add / edit / delete

        private void add_Click(object sender, EventArgs e)
        {
            // pre-select the scope and category of the current selection
            GroupTag group = SelectedGroup;
            CommandSnippet command = new CommandSnippet { Category = group == null ? null : group.Category };
            CommandScopeOption scope = group == null || group.Section.Scope == null ? CommandScopeOption.Global() : group.Section.Scope;
            command.Scope = scope.Scope;
            command.ScopeKey = scope.Key;

            using (dlgEditCommand dialog = new dlgEditCommand(command, Library.Categories(), ScopeOptions(), true))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    Library.Commands.Add(dialog.Command);
                    this.textBoxFilter.Text = string.Empty;
                    SaveLibrary();
                    RefreshTree(dialog.Command);
                }
            }
        }

        private void addToFavoritesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CommandSnippet command = SelectedCommand;
            if (command == null)
            {
                return;
            }
            CommandSnippet favorite;
            if (Library.AddToFavorites(command, out favorite))
            {
                SaveLibrary();
            }
            RefreshTree(favorite);
        }

        private void edit_Click(object sender, EventArgs e)
        {
            EditSelected();
        }

        private void EditSelected()
        {
            CommandSnippet command = SelectedCommand;
            if (command == null)
            {
                return;
            }
            using (dlgEditCommand dialog = new dlgEditCommand(command, Library.Categories(), ScopeOptions(), false))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    int index = Library.Commands.IndexOf(command);
                    Library.Commands[index] = dialog.Command;
                    SaveLibrary();
                    RefreshTree(dialog.Command);
                }
            }
        }

        private void moveUpToolStripMenuItem_Click(object sender, EventArgs e) { MoveSelected(-1); }
        private void moveDownToolStripMenuItem_Click(object sender, EventArgs e) { MoveSelected(1); }

        private void MoveSelected(int direction)
        {
            CommandSnippet command = SelectedCommand;
            if (command != null && Library.Move(command, direction))
            {
                SaveLibrary();
                RefreshTree(command);
            }
        }

        /// <summary>Export the commands of the selected section (all commands if nothing is selected) to a file</summary>
        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GroupTag group = SelectedGroup;
            List<CommandSnippet> commands = group == null
                ? Library.Commands.ToList()
                : Library.Commands.Where(group.Section.Includes).ToList();
            using (SaveFileDialog dialog = new SaveFileDialog { Filter = "Command lists (*.xml)|*.xml|All files (*.*)|*.*", FileName = "Commands-export.xml", Title = "Export Commands" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                CommandLibrary export = new CommandLibrary { Version = CommandLibrary.CurrentVersion };
                export.Commands.AddRange(commands.Select(c => c.Clone()));
                try
                {
                    export.Save(dialog.FileName);
                    SuperPuTTY.ReportStatus("Exported {0} command(s) to {1}", commands.Count, dialog.FileName);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    MessageBox.Show(this, "Could not export the commands:\n" + ex.Message, "Export Commands", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        /// <summary>Import commands from a file; entries that exist already are skipped</summary>
        private void importToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog { Filter = "Command lists (*.xml)|*.xml|All files (*.*)|*.*", Title = "Import Commands" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                CommandLibrary imported = CommandLibrary.Load(dialog.FileName);
                int added = Library.Merge(imported);
                if (added > 0)
                {
                    SaveLibrary();
                }
                RefreshTree();
                SuperPuTTY.ReportStatus("Imported {0} of {1} command(s) from {2}", added, imported.Commands.Count, dialog.FileName);
            }
        }

        private void delete_Click(object sender, EventArgs e)
        {
            DeleteSelected();
        }

        private void DeleteSelected()
        {
            CommandSnippet command = SelectedCommand;
            if (command == null)
            {
                return;
            }
            if (MessageBox.Show(this, "Delete the command\n\n" + command.Command, "Delete Command",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.OK)
            {
                Library.Commands.Remove(command);
                SaveLibrary();
                RefreshTree();
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CommandStore.Changed -= CommandStore_Changed;
                this.singleClickTimer.Dispose();
                if (this.components != null)
                {
                    this.components.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
