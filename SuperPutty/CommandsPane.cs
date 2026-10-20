using System;
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
    /// </summary>
    public partial class CommandsPane : ToolWindow
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CommandsPane));

        /// <summary>Sends a command to the active terminal: (command, execute) => sent</summary>
        private readonly Func<string, bool, bool> sendToTerminal;

        /// <summary>Runs a command in all terminals that accept commands: command => number of terminals</summary>
        private readonly Func<string, int> runInAllTerminals;

        private readonly Timer singleClickTimer = new Timer();
        private CommandSnippet pendingSingleClick;

        private CommandLibrary library;

        /// <summary>For the layout restore and the designer; commands can't be sent without a sender</summary>
        public CommandsPane() : this(null, null) { }

        public CommandsPane(Func<string, bool, bool> sendToTerminal, Func<string, int> runInAllTerminals)
        {
            InitializeComponent();
            this.sendToTerminal = sendToTerminal;
            this.runInAllTerminals = runInAllTerminals;

            this.singleClickTimer.Interval = SystemInformation.DoubleClickTime;
            this.singleClickTimer.Tick += singleClickTimer_Tick;

            this.library = CommandLibrary.Load(LibraryPath);
            if (this.library.Upgrade() || !File.Exists(LibraryPath))
            {
                SaveLibrary();
            }
            RefreshTree();
        }

        private static string LibraryPath
        {
            get { return Path.Combine(SuperPuTTY.Settings.SettingsFolder, CommandLibrary.FileName); }
        }

        private void SaveLibrary()
        {
            try
            {
                this.library.Save(LibraryPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.Error("Could not save commands to " + LibraryPath, ex);
                MessageBox.Show(this, "Could not save the commands:\n" + ex.Message, "Commands", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        #region Tree

        /// <summary>Rebuild the tree from the library and the filter, keeping the selection where possible</summary>
        private void RefreshTree(CommandSnippet select = null)
        {
            select = select ?? SelectedCommand;
            string filter = this.textBoxFilter.Text;

            this.treeViewCommands.BeginUpdate();
            try
            {
                this.treeViewCommands.Nodes.Clear();
                foreach (string category in this.library.Categories())
                {
                    // Favorites stays visible even when empty, unless a filter is active
                    TreeNode categoryNode = CommandLibrary.IsFavorites(category) && string.IsNullOrWhiteSpace(filter)
                        ? this.treeViewCommands.Nodes.Add(category) : null;
                    foreach (CommandSnippet command in this.library.Commands.Where(c => CommandLibrary.CategoryOf(c).Equals(category, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!command.Matches(filter))
                        {
                            continue;
                        }
                        if (categoryNode == null)
                        {
                            categoryNode = this.treeViewCommands.Nodes.Add(category);
                        }
                        TreeNode node = categoryNode.Nodes.Add(command.DisplayText);
                        node.Tag = command;
                        node.ToolTipText = string.IsNullOrEmpty(command.Description) ? command.Command : command.Command + "\n" + command.Description;
                        if (command == select)
                        {
                            this.treeViewCommands.SelectedNode = node;
                        }
                    }
                }
                this.treeViewCommands.ExpandAll();
            }
            finally
            {
                this.treeViewCommands.EndUpdate();
            }
            UpdateButtons();
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
            this.editToolStripMenuItem.Enabled = hasCommand;
            this.addToFavoritesToolStripMenuItem.Enabled = hasCommand && !CommandLibrary.IsFavorites(CommandLibrary.CategoryOf(SelectedCommand));
            this.deleteToolStripMenuItem.Enabled = hasCommand;
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
        }

        private void Send(CommandSnippet command, bool execute)
        {
            if (this.sendToTerminal == null || !this.sendToTerminal(command.Command, execute))
            {
                SuperPuTTY.ReportStatus("No terminal tab is active to send the command to");
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

        private void runInAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedCommand != null && this.runInAllTerminals != null)
            {
                this.runInAllTerminals(SelectedCommand.Command);
            }
        }

        #endregion

        #region Add / edit / delete

        private void add_Click(object sender, EventArgs e)
        {
            // pre-select the category of the current selection
            string category = this.treeViewCommands.SelectedNode == null ? null
                : SelectedCommand != null ? CommandLibrary.CategoryOf(SelectedCommand) : this.treeViewCommands.SelectedNode.Text;
            using (dlgEditCommand dialog = new dlgEditCommand(new CommandSnippet { Category = category }, this.library.Categories(), true))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    this.library.Commands.Add(dialog.Command);
                    SaveLibrary();
                    this.textBoxFilter.Text = string.Empty;
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
            if (this.library.AddToFavorites(command, out favorite))
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
            using (dlgEditCommand dialog = new dlgEditCommand(command, this.library.Categories(), false))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    int index = this.library.Commands.IndexOf(command);
                    this.library.Commands[index] = dialog.Command;
                    SaveLibrary();
                    RefreshTree(dialog.Command);
                }
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
                this.library.Commands.Remove(command);
                SaveLibrary();
                RefreshTree();
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
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
