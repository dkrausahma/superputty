using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace SuperPutty.Gui
{
    /// <summary>Confirm an upload: shows the files and folders and lets the target folder be changed</summary>
    public partial class dlgUploadFiles : Form
    {
        /// <summary>The remote target folder as typed, e.g. "~/app" or "/var/www"</summary>
        public string TargetFolder { get { return this.textBoxTarget.Text.Trim(); } }

        public dlgUploadFiles(string sessionName, IEnumerable<string> paths, string targetFolder, bool targetDetected)
        {
            SuperPutty.Utils.HelpFiles.Attach(this, "file-transfer.html");
            InitializeComponent();

            this.labelHeader.Text = string.Format("Upload to {0}:", sessionName);
            foreach (string path in paths)
            {
                this.listBoxFiles.Items.Add(Directory.Exists(path) ? Path.GetFileName(path) + "\\  (folder)" : Path.GetFileName(path));
            }
            this.textBoxTarget.Text = targetFolder;
            this.labelHint.Text = targetDetected
                ? "Target folder taken from the terminal's current directory. ~ is the home directory."
                : "The current directory isn't known (the shell doesn't show it in the window title). ~ is the home directory.";
        }

        private void dlgUploadFiles_Shown(object sender, EventArgs e)
        {
            this.textBoxTarget.Focus();
            this.textBoxTarget.SelectAll();
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            if (this.TargetFolder.Length == 0)
            {
                this.textBoxTarget.Text = "~";
            }
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
