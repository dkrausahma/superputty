using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SuperPutty.Data;

namespace SuperPutty.Gui
{
    /// <summary>Add or edit a command of the Commands pane</summary>
    public partial class dlgEditCommand : Form
    {
        /// <summary>The edited command; a copy of the one passed in, applied by the caller on OK</summary>
        public CommandSnippet Command { get; private set; }

        /// <param name="scopes">Where the command may apply (all sessions, folders and session of the active tab)</param>
        public dlgEditCommand(CommandSnippet command, IEnumerable<string> categories, IList<CommandScopeOption> scopes, bool isNew)
        {
            InitializeComponent();

            this.Command = command.Clone();
            this.Text = isNew ? "Add Command" : "Edit Command";

            // offer the command's current scope even if it isn't one of the active tab's
            List<CommandScopeOption> options = new List<CommandScopeOption>(scopes);
            CommandScopeOption current = options.FirstOrDefault(o => o.Matches(this.Command));
            if (current == null)
            {
                current = CommandScopeOption.Of(this.Command);
                options.Add(current);
            }
            this.comboBoxScope.Items.AddRange(options.ToArray());
            this.comboBoxScope.SelectedItem = current;

            foreach (string category in categories)
            {
                this.comboBoxCategory.Items.Add(category);
            }
            this.textBoxCommand.Text = this.Command.Command;
            this.textBoxName.Text = this.Command.Name;
            this.textBoxDescription.Text = this.Command.Description;
            this.comboBoxCategory.Text = CommandLibrary.CategoryOf(this.Command);
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            // keep trailing spaces of the command: they leave room for arguments
            if (string.IsNullOrWhiteSpace(this.textBoxCommand.Text))
            {
                MessageBox.Show(this, "Please enter a command.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.textBoxCommand.Focus();
                return;
            }

            this.Command.Command = this.textBoxCommand.Text.TrimStart();
            this.Command.Name = NullIfEmpty(this.textBoxName.Text);
            this.Command.Description = NullIfEmpty(this.textBoxDescription.Text);
            this.Command.Category = NullIfEmpty(this.comboBoxCategory.Text) ?? CommandLibrary.DefaultCategory;
            CommandScopeOption scope = (CommandScopeOption)this.comboBoxScope.SelectedItem;
            this.Command.Scope = scope.Scope;
            this.Command.ScopeKey = scope.Scope == CommandScope.Global ? null : scope.Key;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private static string NullIfEmpty(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
    }
}
