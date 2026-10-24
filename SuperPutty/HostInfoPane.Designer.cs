namespace SuperPutty
{
    partial class HostInfoPane
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
            if (disposing)
            {
                StopStream();
                if (components != null)
                {
                    components.Dispose();
                }
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
            this.components = new System.ComponentModel.Container();
            this.toolStripHostInfo = new SuperPutty.Gui.ClickThroughToolStrip();
            this.toolStripButtonCopy = new System.Windows.Forms.ToolStripButton();
            this.toolStripButtonReconnect = new System.Windows.Forms.ToolStripButton();
            this.toolStripLabelHost = new System.Windows.Forms.ToolStripLabel();
            this.hostInfoView = new SuperPutty.Gui.HostInfoView();
            this.timerCheck = new System.Windows.Forms.Timer(this.components);
            this.toolStripHostInfo.SuspendLayout();
            this.SuspendLayout();
            //
            // toolStripHostInfo
            //
            this.toolStripHostInfo.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStripHostInfo.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripButtonCopy,
            this.toolStripButtonReconnect,
            this.toolStripLabelHost});
            this.toolStripHostInfo.Location = new System.Drawing.Point(0, 0);
            this.toolStripHostInfo.Name = "toolStripHostInfo";
            this.toolStripHostInfo.Size = new System.Drawing.Size(300, 25);
            this.toolStripHostInfo.TabIndex = 0;
            //
            // toolStripButtonCopy
            //
            this.toolStripButtonCopy.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripButtonCopy.Enabled = false;
            this.toolStripButtonCopy.Image = global::SuperPutty.Properties.Resources.copy_session;
            this.toolStripButtonCopy.Name = "toolStripButtonCopy";
            this.toolStripButtonCopy.Size = new System.Drawing.Size(23, 22);
            this.toolStripButtonCopy.Text = "Copy";
            this.toolStripButtonCopy.ToolTipText = "Copy the host information as text";
            this.toolStripButtonCopy.Click += new System.EventHandler(this.toolStripButtonCopy_Click);
            //
            // toolStripButtonReconnect
            //
            this.toolStripButtonReconnect.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripButtonReconnect.Image = global::SuperPutty.Properties.Resources.pane_refresh;
            this.toolStripButtonReconnect.Name = "toolStripButtonReconnect";
            this.toolStripButtonReconnect.Size = new System.Drawing.Size(23, 22);
            this.toolStripButtonReconnect.Text = "Reconnect";
            this.toolStripButtonReconnect.ToolTipText = "Connect again (F5)";
            this.toolStripButtonReconnect.Click += new System.EventHandler(this.toolStripButtonReconnect_Click);
            //
            // toolStripLabelHost
            //
            this.toolStripLabelHost.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolStripLabelHost.ForeColor = System.Drawing.SystemColors.GrayText;
            this.toolStripLabelHost.Name = "toolStripLabelHost";
            this.toolStripLabelHost.Size = new System.Drawing.Size(0, 22);
            //
            // hostInfoView
            //
            this.hostInfoView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.hostInfoView.Location = new System.Drawing.Point(0, 25);
            this.hostInfoView.Name = "hostInfoView";
            this.hostInfoView.Size = new System.Drawing.Size(300, 375);
            this.hostInfoView.TabIndex = 1;
            //
            // timerCheck
            //
            this.timerCheck.Interval = 1000;
            this.timerCheck.Tick += new System.EventHandler(this.timerCheck_Tick);
            //
            // HostInfoPane
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(300, 400);
            this.Controls.Add(this.hostInfoView);
            this.Controls.Add(this.toolStripHostInfo);
            this.KeyPreview = true;
            this.Name = "HostInfoPane";
            this.TabText = "Host Info";
            this.Text = "Host Info";
            this.toolStripHostInfo.ResumeLayout(false);
            this.toolStripHostInfo.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private SuperPutty.Gui.ClickThroughToolStrip toolStripHostInfo;
        private System.Windows.Forms.ToolStripButton toolStripButtonCopy;
        private System.Windows.Forms.ToolStripButton toolStripButtonReconnect;
        private System.Windows.Forms.ToolStripLabel toolStripLabelHost;
        private SuperPutty.Gui.HostInfoView hostInfoView;
        private System.Windows.Forms.Timer timerCheck;
    }
}
