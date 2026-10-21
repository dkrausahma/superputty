using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SuperPutty.Data;
using SuperPutty.Scp;

namespace SuperPutty.Gui
{
    /// <summary>
    /// Copy files between this PC and session hosts, or from host to host: pick source and target (this PC or a
    /// session) and their paths
    /// </summary>
    public partial class dlgTransferFiles : Form
    {
        /// <summary>An entry of the source/target lists: this PC or a session</summary>
        private class EndpointItem
        {
            public SessionData Session { get; set; }
            public string Text { get; set; }
            public override string ToString() { return this.Text; }
        }

        public TransferEndpoint Source { get; private set; }
        public TransferEndpoint Target { get; private set; }

        /// <summary>Path preset when a session is picked: its tab's current directory, or "~"</summary>
        private readonly Func<SessionData, string> remoteDirectory;

        /// <summary>Path preset when "This PC" is picked as target</summary>
        private readonly string localTargetFolder;

        /// <param name="sessions">Sessions to offer, open tabs first</param>
        /// <param name="remoteDirectory">Current directory of a session (its open tab), or "~"</param>
        public dlgTransferFiles(IList<SessionData> sessions, TransferEndpoint source, TransferEndpoint target,
            Func<SessionData, string> remoteDirectory, string localTargetFolder)
        {
            InitializeComponent();
            this.remoteDirectory = remoteDirectory;
            this.localTargetFolder = localTargetFolder;

            Fill(this.comboBoxSource, sessions, source);
            Fill(this.comboBoxTarget, sessions, target);
            this.textBoxSource.Text = source.Path;
            this.textBoxTarget.Text = target.Path;
            UpdateBrowseButtons();

            // only now: picking another endpoint resets its path
            this.comboBoxSource.SelectedIndexChanged += comboBoxSource_SelectedIndexChanged;
            this.comboBoxTarget.SelectedIndexChanged += comboBoxTarget_SelectedIndexChanged;
        }

        private static void Fill(ComboBox combo, IList<SessionData> sessions, TransferEndpoint selected)
        {
            combo.Items.Add(new EndpointItem { Session = null, Text = "This PC" });
            foreach (SessionData session in sessions)
            {
                combo.Items.Add(new EndpointItem { Session = session, Text = session.SessionId });
            }
            combo.SelectedIndex = selected.IsLocal ? 0 : Math.Max(0, sessions.IndexOf(selected.Session) + 1);

            // the list opens as wide as the longest session name
            int width = combo.Width;
            foreach (object item in combo.Items)
            {
                width = Math.Max(width, TextRenderer.MeasureText(item.ToString(), combo.Font).Width + SystemInformation.VerticalScrollBarWidth + 8);
            }
            combo.DropDownWidth = Math.Min(width, Screen.FromControl(combo).WorkingArea.Width - 40);
        }

        private void comboBoxSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            SessionData session = SessionOf(this.comboBoxSource);
            this.textBoxSource.Text = session == null ? string.Empty : this.remoteDirectory(session) + "/";
            UpdateBrowseButtons();
        }

        private void comboBoxTarget_SelectedIndexChanged(object sender, EventArgs e)
        {
            SessionData session = SessionOf(this.comboBoxTarget);
            this.textBoxTarget.Text = session == null ? this.localTargetFolder : this.remoteDirectory(session);
            UpdateBrowseButtons();
        }

        private static SessionData SessionOf(ComboBox combo)
        {
            EndpointItem item = combo.SelectedItem as EndpointItem;
            return item == null ? null : item.Session;
        }

        private void UpdateBrowseButtons()
        {
            this.buttonBrowseSource.Enabled = SessionOf(this.comboBoxSource) == null;
            this.buttonBrowseTarget.Enabled = SessionOf(this.comboBoxTarget) == null;
        }

        private void buttonBrowseSource_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog { Multiselect = true, Title = "Files to copy" })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    // several local files are separated by '|'
                    this.textBoxSource.Text = string.Join("|", dialog.FileNames);
                }
            }
        }

        private void buttonBrowseTarget_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog { Description = "Copy into folder", SelectedPath = this.textBoxTarget.Text })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    this.textBoxTarget.Text = dialog.SelectedPath;
                }
            }
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            TransferEndpoint source = new TransferEndpoint { Session = SessionOf(this.comboBoxSource), Path = this.textBoxSource.Text.Trim() };
            TransferEndpoint target = new TransferEndpoint { Session = SessionOf(this.comboBoxTarget), Path = this.textBoxTarget.Text.Trim() };
            string error;
            if (FileTransferJob.ModeOf(source, target, out error) == null)
            {
                MessageBox.Show(this, error, this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            this.Source = source;
            this.Target = target;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
