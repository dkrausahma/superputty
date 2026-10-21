namespace SuperPutty.Gui
{
    partial class dlgTransferFiles
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.groupBoxSource = new System.Windows.Forms.GroupBox();
            this.comboBoxSource = new System.Windows.Forms.ComboBox();
            this.textBoxSource = new System.Windows.Forms.TextBox();
            this.buttonBrowseSource = new System.Windows.Forms.Button();
            this.groupBoxTarget = new System.Windows.Forms.GroupBox();
            this.comboBoxTarget = new System.Windows.Forms.ComboBox();
            this.textBoxTarget = new System.Windows.Forms.TextBox();
            this.buttonBrowseTarget = new System.Windows.Forms.Button();
            this.labelHint = new System.Windows.Forms.Label();
            this.buttonOK = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.groupBoxSource.SuspendLayout();
            this.groupBoxTarget.SuspendLayout();
            this.SuspendLayout();
            //
            // groupBoxSource
            //
            this.groupBoxSource.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxSource.Controls.Add(this.comboBoxSource);
            this.groupBoxSource.Controls.Add(this.textBoxSource);
            this.groupBoxSource.Controls.Add(this.buttonBrowseSource);
            this.groupBoxSource.Location = new System.Drawing.Point(12, 12);
            this.groupBoxSource.Name = "groupBoxSource";
            this.groupBoxSource.Size = new System.Drawing.Size(740, 78);
            this.groupBoxSource.TabIndex = 0;
            this.groupBoxSource.TabStop = false;
            this.groupBoxSource.Text = "Copy &from";
            //
            // comboBoxSource
            //
            this.comboBoxSource.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxSource.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxSource.FormattingEnabled = true;
            this.comboBoxSource.Location = new System.Drawing.Point(10, 19);
            this.comboBoxSource.Name = "comboBoxSource";
            this.comboBoxSource.Size = new System.Drawing.Size(720, 21);
            this.comboBoxSource.TabIndex = 0;
            //
            // textBoxSource
            //
            this.textBoxSource.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxSource.Font = new System.Drawing.Font("Lucida Console", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxSource.Location = new System.Drawing.Point(10, 47);
            this.textBoxSource.Name = "textBoxSource";
            this.textBoxSource.PlaceholderText = "file, folder or pattern, e.g. ~/app/logs/*.log";
            this.textBoxSource.Size = new System.Drawing.Size(639, 18);
            this.textBoxSource.TabIndex = 1;
            //
            // buttonBrowseSource
            //
            this.buttonBrowseSource.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonBrowseSource.Location = new System.Drawing.Point(655, 44);
            this.buttonBrowseSource.Name = "buttonBrowseSource";
            this.buttonBrowseSource.Size = new System.Drawing.Size(75, 23);
            this.buttonBrowseSource.TabIndex = 2;
            this.buttonBrowseSource.Text = "Browse…";
            this.buttonBrowseSource.UseVisualStyleBackColor = true;
            this.buttonBrowseSource.Click += new System.EventHandler(this.buttonBrowseSource_Click);
            //
            // groupBoxTarget
            //
            this.groupBoxTarget.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxTarget.Controls.Add(this.comboBoxTarget);
            this.groupBoxTarget.Controls.Add(this.textBoxTarget);
            this.groupBoxTarget.Controls.Add(this.buttonBrowseTarget);
            this.groupBoxTarget.Location = new System.Drawing.Point(12, 96);
            this.groupBoxTarget.Name = "groupBoxTarget";
            this.groupBoxTarget.Size = new System.Drawing.Size(740, 78);
            this.groupBoxTarget.TabIndex = 1;
            this.groupBoxTarget.TabStop = false;
            this.groupBoxTarget.Text = "Copy &into folder";
            //
            // comboBoxTarget
            //
            this.comboBoxTarget.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxTarget.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxTarget.FormattingEnabled = true;
            this.comboBoxTarget.Location = new System.Drawing.Point(10, 19);
            this.comboBoxTarget.Name = "comboBoxTarget";
            this.comboBoxTarget.Size = new System.Drawing.Size(720, 21);
            this.comboBoxTarget.TabIndex = 0;
            //
            // textBoxTarget
            //
            this.textBoxTarget.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxTarget.Font = new System.Drawing.Font("Lucida Console", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxTarget.Location = new System.Drawing.Point(10, 47);
            this.textBoxTarget.Name = "textBoxTarget";
            this.textBoxTarget.Size = new System.Drawing.Size(639, 18);
            this.textBoxTarget.TabIndex = 1;
            //
            // buttonBrowseTarget
            //
            this.buttonBrowseTarget.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonBrowseTarget.Location = new System.Drawing.Point(655, 44);
            this.buttonBrowseTarget.Name = "buttonBrowseTarget";
            this.buttonBrowseTarget.Size = new System.Drawing.Size(75, 23);
            this.buttonBrowseTarget.TabIndex = 2;
            this.buttonBrowseTarget.Text = "Browse…";
            this.buttonBrowseTarget.UseVisualStyleBackColor = true;
            this.buttonBrowseTarget.Click += new System.EventHandler(this.buttonBrowseTarget_Click);
            //
            // labelHint
            //
            this.labelHint.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelHint.ForeColor = System.Drawing.SystemColors.GrayText;
            this.labelHint.Location = new System.Drawing.Point(12, 180);
            this.labelHint.Name = "labelHint";
            this.labelHint.Size = new System.Drawing.Size(740, 30);
            this.labelHint.TabIndex = 2;
            this.labelHint.Text = "Folders are copied recursively; ~ is the home directory. Host-to-host copies go through a temporary folder on this PC, so the hosts need no keys for each other.";
            //
            // buttonOK
            //
            this.buttonOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonOK.Location = new System.Drawing.Point(596, 217);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(75, 23);
            this.buttonOK.TabIndex = 3;
            this.buttonOK.Text = "Copy";
            this.buttonOK.UseVisualStyleBackColor = true;
            this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);
            //
            // buttonCancel
            //
            this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Location = new System.Drawing.Point(677, 217);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.TabIndex = 4;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            //
            // dlgTransferFiles
            //
            this.AcceptButton = this.buttonOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(764, 252);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.labelHint);
            this.Controls.Add(this.groupBoxTarget);
            this.Controls.Add(this.groupBoxSource);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MinimumSize = new System.Drawing.Size(560, 291);
            this.MaximumSize = new System.Drawing.Size(4000, 291);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "dlgTransferFiles";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Transfer Files";
            this.groupBoxSource.ResumeLayout(false);
            this.groupBoxSource.PerformLayout();
            this.groupBoxTarget.ResumeLayout(false);
            this.groupBoxTarget.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBoxSource;
        private System.Windows.Forms.ComboBox comboBoxSource;
        private System.Windows.Forms.TextBox textBoxSource;
        private System.Windows.Forms.Button buttonBrowseSource;
        private System.Windows.Forms.GroupBox groupBoxTarget;
        private System.Windows.Forms.ComboBox comboBoxTarget;
        private System.Windows.Forms.TextBox textBoxTarget;
        private System.Windows.Forms.Button buttonBrowseTarget;
        private System.Windows.Forms.Label labelHint;
        private System.Windows.Forms.Button buttonOK;
        private System.Windows.Forms.Button buttonCancel;
    }
}
