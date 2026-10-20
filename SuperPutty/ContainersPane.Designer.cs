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
            this.listViewContainers = new System.Windows.Forms.ListView();
            this.columnHeaderName = new System.Windows.Forms.ColumnHeader();
            this.columnHeaderStatus = new System.Windows.Forms.ColumnHeader();
            this.columnHeaderImage = new System.Windows.Forms.ColumnHeader();
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
            this.toolStripButtonRefresh.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButtonRefresh.Name = "toolStripButtonRefresh";
            this.toolStripButtonRefresh.Size = new System.Drawing.Size(50, 22);
            this.toolStripButtonRefresh.Text = "Refresh";
            this.toolStripButtonRefresh.ToolTipText = "Reload the containers (F5)";
            this.toolStripButtonRefresh.Click += new System.EventHandler(this.toolStripButtonRefresh_Click);
            //
            // toolStripButtonShowStopped
            //
            this.toolStripButtonShowStopped.CheckOnClick = true;
            this.toolStripButtonShowStopped.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButtonShowStopped.Name = "toolStripButtonShowStopped";
            this.toolStripButtonShowStopped.Size = new System.Drawing.Size(55, 22);
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
            // listViewContainers
            //
            this.listViewContainers.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeaderName,
            this.columnHeaderStatus,
            this.columnHeaderImage});
            this.listViewContainers.ContextMenuStrip = this.contextMenuStrip;
            this.listViewContainers.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewContainers.FullRowSelect = true;
            this.listViewContainers.HideSelection = false;
            this.listViewContainers.Location = new System.Drawing.Point(0, 25);
            this.listViewContainers.MultiSelect = false;
            this.listViewContainers.Name = "listViewContainers";
            this.listViewContainers.ShowItemToolTips = true;
            this.listViewContainers.Size = new System.Drawing.Size(300, 335);
            this.listViewContainers.TabIndex = 1;
            this.listViewContainers.UseCompatibleStateImageBehavior = false;
            this.listViewContainers.View = System.Windows.Forms.View.Details;
            this.listViewContainers.DoubleClick += new System.EventHandler(this.listViewContainers_DoubleClick);
            this.listViewContainers.KeyDown += new System.Windows.Forms.KeyEventHandler(this.listViewContainers_KeyDown);
            //
            // columnHeaderName
            //
            this.columnHeaderName.Text = "Container";
            this.columnHeaderName.Width = 130;
            //
            // columnHeaderStatus
            //
            this.columnHeaderStatus.Text = "Status";
            this.columnHeaderStatus.Width = 110;
            //
            // columnHeaderImage
            //
            this.columnHeaderImage.Text = "Image";
            this.columnHeaderImage.Width = 160;
            //
            // contextMenuStrip
            //
            this.contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.shellToolStripMenuItem,
            this.rootShellToolStripMenuItem,
            this.logsToolStripMenuItem,
            this.inspectToolStripMenuItem,
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
            this.Controls.Add(this.listViewContainers);
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
        private System.Windows.Forms.ListView listViewContainers;
        private System.Windows.Forms.ColumnHeader columnHeaderName;
        private System.Windows.Forms.ColumnHeader columnHeaderStatus;
        private System.Windows.Forms.ColumnHeader columnHeaderImage;
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
        private System.Windows.Forms.Label labelStatus;
    }
}
