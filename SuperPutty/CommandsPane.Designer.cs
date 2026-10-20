namespace SuperPutty
{
    partial class CommandsPane
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.toolStripCommands = new SuperPutty.Gui.ClickThroughToolStrip();
            this.toolStripButtonAdd = new System.Windows.Forms.ToolStripButton();
            this.toolStripButtonEdit = new System.Windows.Forms.ToolStripButton();
            this.toolStripButtonDelete = new System.Windows.Forms.ToolStripButton();
            this.textBoxFilter = new System.Windows.Forms.TextBox();
            this.treeViewCommands = new System.Windows.Forms.TreeView();
            this.contextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.typeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.runToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.runInAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.addToFavoritesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripCommands.SuspendLayout();
            this.contextMenuStrip.SuspendLayout();
            this.SuspendLayout();
            //
            // toolStripCommands
            //
            this.toolStripCommands.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStripCommands.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripButtonAdd,
            this.toolStripButtonEdit,
            this.toolStripButtonDelete});
            this.toolStripCommands.Location = new System.Drawing.Point(0, 0);
            this.toolStripCommands.Name = "toolStripCommands";
            this.toolStripCommands.Size = new System.Drawing.Size(260, 25);
            this.toolStripCommands.TabIndex = 0;
            //
            // toolStripButtonAdd
            //
            this.toolStripButtonAdd.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButtonAdd.Name = "toolStripButtonAdd";
            this.toolStripButtonAdd.Size = new System.Drawing.Size(42, 22);
            this.toolStripButtonAdd.Text = "Add…";
            this.toolStripButtonAdd.ToolTipText = "Add a command";
            this.toolStripButtonAdd.Click += new System.EventHandler(this.add_Click);
            //
            // toolStripButtonEdit
            //
            this.toolStripButtonEdit.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButtonEdit.Name = "toolStripButtonEdit";
            this.toolStripButtonEdit.Size = new System.Drawing.Size(39, 22);
            this.toolStripButtonEdit.Text = "Edit…";
            this.toolStripButtonEdit.ToolTipText = "Edit the selected command (F2)";
            this.toolStripButtonEdit.Click += new System.EventHandler(this.edit_Click);
            //
            // toolStripButtonDelete
            //
            this.toolStripButtonDelete.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButtonDelete.Name = "toolStripButtonDelete";
            this.toolStripButtonDelete.Size = new System.Drawing.Size(44, 22);
            this.toolStripButtonDelete.Text = "Delete";
            this.toolStripButtonDelete.ToolTipText = "Delete the selected command (Del)";
            this.toolStripButtonDelete.Click += new System.EventHandler(this.delete_Click);
            //
            // textBoxFilter
            //
            this.textBoxFilter.Dock = System.Windows.Forms.DockStyle.Top;
            this.textBoxFilter.Location = new System.Drawing.Point(0, 25);
            this.textBoxFilter.Name = "textBoxFilter";
            this.textBoxFilter.PlaceholderText = "Filter…";
            this.textBoxFilter.Size = new System.Drawing.Size(260, 20);
            this.textBoxFilter.TabIndex = 1;
            this.textBoxFilter.TextChanged += new System.EventHandler(this.textBoxFilter_TextChanged);
            //
            // treeViewCommands
            //
            this.treeViewCommands.ContextMenuStrip = this.contextMenuStrip;
            this.treeViewCommands.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeViewCommands.Font = new System.Drawing.Font("Lucida Console", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.treeViewCommands.HideSelection = false;
            this.treeViewCommands.Location = new System.Drawing.Point(0, 45);
            this.treeViewCommands.Name = "treeViewCommands";
            this.treeViewCommands.ShowNodeToolTips = true;
            this.treeViewCommands.Size = new System.Drawing.Size(260, 415);
            this.treeViewCommands.TabIndex = 2;
            this.treeViewCommands.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeViewCommands_AfterSelect);
            this.treeViewCommands.NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.treeViewCommands_NodeMouseClick);
            this.treeViewCommands.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.treeViewCommands_NodeMouseDoubleClick);
            this.treeViewCommands.KeyDown += new System.Windows.Forms.KeyEventHandler(this.treeViewCommands_KeyDown);
            //
            // contextMenuStrip
            //
            this.contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.typeToolStripMenuItem,
            this.runToolStripMenuItem,
            this.runInAllToolStripMenuItem,
            this.toolStripMenuItem1,
            this.addToFavoritesToolStripMenuItem,
            this.addToolStripMenuItem,
            this.editToolStripMenuItem,
            this.deleteToolStripMenuItem});
            this.contextMenuStrip.Name = "contextMenuStrip";
            this.contextMenuStrip.Size = new System.Drawing.Size(200, 120);
            //
            // typeToolStripMenuItem
            //
            this.typeToolStripMenuItem.Name = "typeToolStripMenuItem";
            this.typeToolStripMenuItem.ShortcutKeyDisplayString = "Click";
            this.typeToolStripMenuItem.Size = new System.Drawing.Size(199, 22);
            this.typeToolStripMenuItem.Text = "&Type into Terminal";
            this.typeToolStripMenuItem.Click += new System.EventHandler(this.typeToolStripMenuItem_Click);
            //
            // runToolStripMenuItem
            //
            this.runToolStripMenuItem.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.runToolStripMenuItem.Name = "runToolStripMenuItem";
            this.runToolStripMenuItem.ShortcutKeyDisplayString = "Double-click";
            this.runToolStripMenuItem.Size = new System.Drawing.Size(199, 22);
            this.runToolStripMenuItem.Text = "&Run in Terminal";
            this.runToolStripMenuItem.Click += new System.EventHandler(this.runToolStripMenuItem_Click);
            //
            // runInAllToolStripMenuItem
            //
            this.runInAllToolStripMenuItem.Name = "runInAllToolStripMenuItem";
            this.runInAllToolStripMenuItem.Size = new System.Drawing.Size(199, 22);
            this.runInAllToolStripMenuItem.Text = "Run in &All Terminals…";
            this.runInAllToolStripMenuItem.ToolTipText = "Run in every terminal tab that accepts commands (asks first)";
            this.runInAllToolStripMenuItem.Click += new System.EventHandler(this.runInAllToolStripMenuItem_Click);
            //
            // toolStripMenuItem1
            //
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(196, 6);
            //
            // addToFavoritesToolStripMenuItem
            //
            this.addToFavoritesToolStripMenuItem.Name = "addToFavoritesToolStripMenuItem";
            this.addToFavoritesToolStripMenuItem.Size = new System.Drawing.Size(199, 22);
            this.addToFavoritesToolStripMenuItem.Text = "Add to &Favorites";
            this.addToFavoritesToolStripMenuItem.Click += new System.EventHandler(this.addToFavoritesToolStripMenuItem_Click);
            //
            // addToolStripMenuItem
            //
            this.addToolStripMenuItem.Name = "addToolStripMenuItem";
            this.addToolStripMenuItem.Size = new System.Drawing.Size(199, 22);
            this.addToolStripMenuItem.Text = "&Add…";
            this.addToolStripMenuItem.Click += new System.EventHandler(this.add_Click);
            //
            // editToolStripMenuItem
            //
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            this.editToolStripMenuItem.ShortcutKeyDisplayString = "F2";
            this.editToolStripMenuItem.Size = new System.Drawing.Size(199, 22);
            this.editToolStripMenuItem.Text = "&Edit…";
            this.editToolStripMenuItem.Click += new System.EventHandler(this.edit_Click);
            //
            // deleteToolStripMenuItem
            //
            this.deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
            this.deleteToolStripMenuItem.ShortcutKeyDisplayString = "Del";
            this.deleteToolStripMenuItem.Size = new System.Drawing.Size(199, 22);
            this.deleteToolStripMenuItem.Text = "&Delete";
            this.deleteToolStripMenuItem.Click += new System.EventHandler(this.delete_Click);
            //
            // CommandsPane
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(260, 460);
            this.Controls.Add(this.treeViewCommands);
            this.Controls.Add(this.textBoxFilter);
            this.Controls.Add(this.toolStripCommands);
            this.Name = "CommandsPane";
            this.TabText = "Commands";
            this.Text = "Commands";
            this.toolStripCommands.ResumeLayout(false);
            this.toolStripCommands.PerformLayout();
            this.contextMenuStrip.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private SuperPutty.Gui.ClickThroughToolStrip toolStripCommands;
        private System.Windows.Forms.ToolStripButton toolStripButtonAdd;
        private System.Windows.Forms.ToolStripButton toolStripButtonEdit;
        private System.Windows.Forms.ToolStripButton toolStripButtonDelete;
        private System.Windows.Forms.TextBox textBoxFilter;
        private System.Windows.Forms.TreeView treeViewCommands;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem typeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem runToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem runInAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem addToFavoritesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteToolStripMenuItem;
    }
}
