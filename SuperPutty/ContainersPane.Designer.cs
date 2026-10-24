namespace SuperPutty
{
    partial class ContainersPane
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
            this.components = new System.ComponentModel.Container();
            this.toolStripContainers = new SuperPutty.Gui.ClickThroughToolStrip();
            this.toolStripButtonRefresh = new System.Windows.Forms.ToolStripButton();
            this.toolStripButtonShowStopped = new System.Windows.Forms.ToolStripButton();
            this.toolStripLabelHost = new System.Windows.Forms.ToolStripLabel();
            this.treeViewContainers = new SuperPutty.Gui.OwnerDrawTreeView();
            this.contextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.shellToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rootShellToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.logsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.inspectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.startToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.stopToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.restartToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem2 = new System.Windows.Forms.ToolStripSeparator();
            this.copyNameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openPortToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.labelStatus = new System.Windows.Forms.Label();
            this.toolStripContainers.SuspendLayout();
            this.contextMenuStrip.SuspendLayout();
            this.SuspendLayout();
            //
            // toolStripContainers
            //
            this.toolStripContainers.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStripContainers.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripButtonRefresh,
            this.toolStripButtonShowStopped,
            this.toolStripLabelHost});
            this.toolStripContainers.Location = new System.Drawing.Point(0, 0);
            this.toolStripContainers.Name = "toolStripContainers";
            this.toolStripContainers.Size = new System.Drawing.Size(300, 25);
            this.toolStripContainers.TabIndex = 0;
            //
            // toolStripButtonRefresh
            //
            this.toolStripButtonRefresh.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripButtonRefresh.Image = global::SuperPutty.Properties.Resources.pane_refresh;
            this.toolStripButtonRefresh.Name = "toolStripButtonRefresh";
            this.toolStripButtonRefresh.Size = new System.Drawing.Size(23, 22);
            this.toolStripButtonRefresh.Text = "Refresh";
            this.toolStripButtonRefresh.ToolTipText = "Reload the containers (F5)";
            this.toolStripButtonRefresh.Click += new System.EventHandler(this.toolStripButtonRefresh_Click);
            //
            // toolStripButtonShowStopped
            //
            this.toolStripButtonShowStopped.CheckOnClick = true;
            this.toolStripButtonShowStopped.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripButtonShowStopped.Image = global::SuperPutty.Properties.Resources.pane_show_stopped;
            this.toolStripButtonShowStopped.Name = "toolStripButtonShowStopped";
            this.toolStripButtonShowStopped.Size = new System.Drawing.Size(23, 22);
            this.toolStripButtonShowStopped.Text = "Stopped";
            this.toolStripButtonShowStopped.ToolTipText = "Also show stopped containers";
            this.toolStripButtonShowStopped.Click += new System.EventHandler(this.toolStripButtonShowStopped_Click);
            //
            // toolStripLabelHost
            //
            this.toolStripLabelHost.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolStripLabelHost.ForeColor = System.Drawing.SystemColors.GrayText;
            this.toolStripLabelHost.Name = "toolStripLabelHost";
            this.toolStripLabelHost.Size = new System.Drawing.Size(0, 22);
            //
            // treeViewContainers
            //
            this.treeViewContainers.ContextMenuStrip = this.contextMenuStrip;
            this.treeViewContainers.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeViewContainers.Location = new System.Drawing.Point(0, 25);
            this.treeViewContainers.Name = "treeViewContainers";
            this.treeViewContainers.ShowNodeToolTips = true;
            this.treeViewContainers.Size = new System.Drawing.Size(300, 335);
            this.treeViewContainers.TabIndex = 1;
            this.treeViewContainers.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.treeViewContainers_NodeMouseDoubleClick);
            this.treeViewContainers.KeyDown += new System.Windows.Forms.KeyEventHandler(this.treeViewContainers_KeyDown);
            //
            // contextMenuStrip
            //
            this.contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.shellToolStripMenuItem,
            this.rootShellToolStripMenuItem,
            this.logsToolStripMenuItem,
            this.inspectToolStripMenuItem,
            this.openPortToolStripMenuItem,
            this.toolStripMenuItem1,
            this.startToolStripMenuItem,
            this.stopToolStripMenuItem,
            this.restartToolStripMenuItem,
            this.toolStripMenuItem2,
            this.copyNameToolStripMenuItem});
            this.contextMenuStrip.Name = "contextMenuStrip";
            this.contextMenuStrip.Size = new System.Drawing.Size(200, 192);
            this.contextMenuStrip.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuStrip_Opening);
            //
            // shellToolStripMenuItem
            //
            this.shellToolStripMenuItem.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.shellToolStripMenuItem.Name = "shellToolStripMenuItem";
            this.shellToolStripMenuItem.ShortcutKeyDisplayString = "Double-click";
            this.shellToolStripMenuItem.Size = new System.Drawing.Size(199, 22);
            this.shellToolStripMenuItem.Text = "&Shell in New Tab";
            this.shellToolStripMenuItem.Click += new System.EventHandler(this.shellToolStripMenuItem_Click);
            //
            // rootShellToolStripMenuItem
            //
            this.rootShellToolStripMenuItem.Name = "rootShellToolStripMenuItem";
            this.rootShellToolStripMenuItem.Size = new System.Drawing.Size(199, 22);
            this.rootShellToolStripMenuItem.Text = "Shell as &Root";
            this.rootShellToolStripMenuItem.Click += new System.EventHandler(this.rootShellToolStripMenuItem_Click);
            //
            // logsToolStripMenuItem
            //
            this.logsToolStripMenuItem.Name = "logsToolStripMenuItem";
            this.logsToolStripMenuItem.Size = new System.Drawing.Size(199, 22);
            this.logsToolStripMenuItem.Text = "Follow &Logs";
            this.logsToolStripMenuItem.Click += new System.EventHandler(this.logsToolStripMenuItem_Click);
            //
            // inspectToolStripMenuItem
            //
            this.inspectToolStripMenuItem.Name = "inspectToolStripMenuItem";
            this.inspectToolStripMenuItem.Size = new System.Drawing.Size(199, 22);
            this.inspectToolStripMenuItem.Text = "&Inspect";
            this.inspectToolStripMenuItem.Click += new System.EventHandler(this.inspectToolStripMenuItem_Click);
            //
            // toolStripMenuItem1
            //
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(196, 6);
            //
            // startToolStripMenuItem
            //
            this.startToolStripMenuItem.Name = "startToolStripMenuItem";
            this.startToolStripMenuItem.Size = new System.Drawing.Size(199, 22);
            this.startToolStripMenuItem.Text = "S&tart";
            this.startToolStripMenuItem.Click += new System.EventHandler(this.startToolStripMenuItem_Click);
            //
            // stopToolStripMenuItem
            //
            this.stopToolStripMenuItem.Name = "stopToolStripMenuItem";
            this.stopToolStripMenuItem.Size = new System.Drawing.Size(199, 22);
            this.stopToolStripMenuItem.Text = "St&op…";
            this.stopToolStripMenuItem.Click += new System.EventHandler(this.stopToolStripMenuItem_Click);
            //
            // restartToolStripMenuItem
            //
            this.restartToolStripMenuItem.Name = "restartToolStripMenuItem";
            this.restartToolStripMenuItem.Size = new System.Drawing.Size(199, 22);
            this.restartToolStripMenuItem.Text = "R&estart…";
            this.restartToolStripMenuItem.Click += new System.EventHandler(this.restartToolStripMenuItem_Click);
            //
            // toolStripMenuItem2
            //
            this.toolStripMenuItem2.Name = "toolStripMenuItem2";
            this.toolStripMenuItem2.Size = new System.Drawing.Size(196, 6);
            //
            // copyNameToolStripMenuItem
            //
            this.copyNameToolStripMenuItem.Name = "copyNameToolStripMenuItem";
            this.copyNameToolStripMenuItem.Size = new System.Drawing.Size(199, 22);
            this.copyNameToolStripMenuItem.Text = "&Copy Name";
            this.copyNameToolStripMenuItem.Click += new System.EventHandler(this.copyNameToolStripMenuItem_Click);
            //
            // openPortToolStripMenuItem
            //
            this.openPortToolStripMenuItem.Name = "openPortToolStripMenuItem";
            this.openPortToolStripMenuItem.Size = new System.Drawing.Size(199, 22);
            this.openPortToolStripMenuItem.Text = "Open in &Browser";
            //
            // labelStatus
            //
            this.labelStatus.AutoEllipsis = true;
            this.labelStatus.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.labelStatus.ForeColor = System.Drawing.SystemColors.GrayText;
            this.labelStatus.Location = new System.Drawing.Point(0, 360);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Padding = new System.Windows.Forms.Padding(3);
            this.labelStatus.Size = new System.Drawing.Size(300, 40);
            this.labelStatus.TabIndex = 2;
            //
            // ContainersPane
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(300, 400);
            this.Controls.Add(this.treeViewContainers);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.toolStripContainers);
            this.Name = "ContainersPane";
            this.TabText = "Containers";
            this.Text = "Containers";
            this.toolStripContainers.ResumeLayout(false);
            this.toolStripContainers.PerformLayout();
            this.contextMenuStrip.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private SuperPutty.Gui.ClickThroughToolStrip toolStripContainers;
        private System.Windows.Forms.ToolStripButton toolStripButtonRefresh;
        private System.Windows.Forms.ToolStripButton toolStripButtonShowStopped;
        private System.Windows.Forms.ToolStripLabel toolStripLabelHost;
        private SuperPutty.Gui.OwnerDrawTreeView treeViewContainers;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem shellToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem rootShellToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem logsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem inspectToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem startToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem stopToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem restartToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem2;
        private System.Windows.Forms.ToolStripMenuItem copyNameToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openPortToolStripMenuItem;
        private System.Windows.Forms.Label labelStatus;
    }
}
