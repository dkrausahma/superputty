using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SuperPutty.Gui;
using SuperPutty.Utils;

namespace SuperPutty
{
    /// <summary>
    /// The configurable icon bar: every tool can be an icon. The catalog below lists them; the setting
    /// IconBarLayout says which are shown in which order (Customize Icon Bar…, right-click on the bar or
    /// View → Toolbars). Menu entries get the same icons.
    /// </summary>
    public partial class frmSuperPutty
    {
        /// <summary>One tool the icon bar can show</summary>
        public class IconBarTool
        {
            public string Id { get; set; }
            public string Text { get; set; }
            public Image Image { get; set; }
            public ToolStripItem Button { get; set; }
            /// <summary>The menu entry the button stands for (its Enabled state is mirrored); null for pane toggles</summary>
            public ToolStripMenuItem MenuItem { get; set; }
        }

        private List<IconBarTool> iconBarTools;
        private ToolStripButton btnSessionDetailPane;
        private ToolStripButton btnLogViewerPane;
        private ToolStripDropDownButton btnRecentSessions;

        /// <summary>All tools, in the order the customize dialog offers new ones</summary>
        private List<IconBarTool> IconBarTools
        {
            get { return this.iconBarTools ?? (this.iconBarTools = CreateIconBarTools()); }
        }

        private List<IconBarTool> CreateIconBarTools()
        {
            this.btnSessionDetailPane = PaneButton("Session Detail", Properties.Resources.session_detail, "Show or close Session Detail",
                delegate { if (IsOpen(this.sessionDetail)) this.sessionDetail.Instance.Close(); else this.sessionDetailToolStripMenuItem.PerformClick(); UpdateIconBar(); });
            this.btnLogViewerPane = PaneButton("Log Viewer", Properties.Resources.log_viewer, "Show or close the Log Viewer",
                delegate { if (IsOpen(this.logViewer)) this.logViewer.Instance.Close(); else this.logViewerToolStripMenuItem.PerformClick(); UpdateIconBar(); });
            this.btnRecentSessions = new ToolStripDropDownButton("Recent Sessions", Properties.Resources.recent_sessions)
            {
                DisplayStyle = ToolStripItemDisplayStyle.Image,
                ToolTipText = "Recently opened sessions"
            };
            this.btnRecentSessions.DropDown.ShowItemToolTips = true;
            this.btnRecentSessions.DropDownOpening += delegate { RecentSessionsMenu.Fill(this.btnRecentSessions.DropDownItems); };
            // a placeholder so the arrow opens a drop-down at all
            this.btnRecentSessions.DropDownItems.Add("…");

            return new List<IconBarTool>
            {
                Existing("sessions", this.btnSessionsPane, this.sessionsToolStripMenuItem),
                Tool("sessiondetail", this.btnSessionDetailPane, this.sessionDetailToolStripMenuItem, null, false),
                Existing("hostinfo", this.btnHostInfoPane, this.hostInfoPaneToolStripMenuItem),
                Existing("layouts", this.btnLayoutsPane, this.layoutsToolStripMenuItem),
                Existing("commands", this.btnCommandsPane, this.commandsPaneToolStripMenuItem),
                Existing("containers", this.btnContainersPane, this.containersPaneToolStripMenuItem),
                Tool("logviewer", this.btnLogViewerPane, this.logViewerToolStripMenuItem, null, false),
                Tool("opensession", null, this.openSessionToolStripMenuItem, Properties.Resources.open_session),
                Tool("recent", this.btnRecentSessions, null, null, false),
                Existing("copysession", this.btnCopySession, this.copySessionToolStripMenuItem),
                Tool("copylastcommand", null, this.copyLastCommandToolStripMenuItem, Properties.Resources.copy_last_command),
                Tool("explain", null, this.explainInClaudeToolStripMenuItem, Properties.Resources.explain_claude),
                Tool("sessionlog", null, this.openSessionLogToolStripMenuItem, Properties.Resources.session_log),
                Tool("logfolder", null, this.openLogFolderToolStripMenuItem, Properties.Resources.log_folder),
                Existing("upload", this.btnUploadFiles, null),
                Existing("transfer", this.btnTransferFiles, this.transferFilesToolStripMenuItem),
                Tool("tunnels", null, this.tunnelsOverviewToolStripMenuItem, Properties.Resources.ssh_tunnels),
                Tool("runonhosts", null, this.runOnHostsToolStripMenuItem, Properties.Resources.run_on_hosts),
                Tool("puttyconfig", null, this.puTTYConfigurationToolStripMenuItem, Properties.Resources.putty_config),
                Tool("options", null, this.optionsToolStripMenuItem, Properties.Resources.options),
                Existing("kiosk", this.btnKioskToggle, this.kioskModeToolStripMenuItem),
            };
        }

        private static ToolStripButton PaneButton(string text, Image image, string tip, EventHandler click)
        {
            ToolStripButton button = new ToolStripButton(text, image, click) { DisplayStyle = ToolStripItemDisplayStyle.Image, ToolTipText = tip };
            return button;
        }

        /// <summary>A button from the designer; the menu entry gets its icon if it has none</summary>
        private static IconBarTool Existing(string id, ToolStripItem button, ToolStripMenuItem menuItem)
        {
            if (menuItem != null && menuItem.Image == null)
            {
                menuItem.Image = button.Image;
            }
            return new IconBarTool { Id = id, Text = button.Text, Image = button.Image, Button = button };
        }

        /// <summary>
        /// A tool: a given button (pane toggles, drop-downs) or a new one that clicks the menu entry. The menu entry
        /// gets the icon; with <paramref name="mirrorEnabled"/> the button follows the entry's Enabled state.
        /// </summary>
        private IconBarTool Tool(string id, ToolStripItem button, ToolStripMenuItem menuItem, Image image, bool mirrorEnabled = true)
        {
            image = image ?? (button == null ? null : button.Image);
            string text = menuItem == null ? button.Text : menuItem.Text.Replace("&", string.Empty).TrimEnd('.', '…');
            if (button == null)
            {
                ToolStripMenuItem target = menuItem;
                button = new ToolStripButton(text, image, delegate { target.PerformClick(); })
                {
                    DisplayStyle = ToolStripItemDisplayStyle.Image,
                    ToolTipText = string.IsNullOrEmpty(menuItem.ToolTipText) ? text : text + " – " + menuItem.ToolTipText
                };
            }
            if (menuItem != null)
            {
                menuItem.Image = menuItem.Image ?? image;
            }
            return new IconBarTool { Id = id, Text = text, Image = image, Button = button, MenuItem = mirrorEnabled ? menuItem : null };
        }

        /// <summary>Show the icons of the IconBarLayout setting, in its order</summary>
        private void ApplyIconBarLayout()
        {
            Dictionary<string, IconBarTool> tools = IconBarTools.ToDictionary(t => t.Id);
            List<string> layout = IconBarLayout.Parse(SuperPuTTY.Settings.IconBarLayout, tools.Keys);
            this.tsIcons.SuspendLayout();
            try
            {
                this.tsIcons.Items.Clear();
                foreach (string entry in layout)
                {
                    this.tsIcons.Items.Add(entry == IconBarLayout.Separator ? new ToolStripSeparator() : tools[entry].Button);
                }
            }
            finally
            {
                this.tsIcons.ResumeLayout(true);
            }
            if (this.tsIcons.ContextMenuStrip == null)
            {
                ContextMenuStrip menu = new ContextMenuStrip();
                menu.Items.Add("&Customize Icon Bar…", null, delegate { CustomizeIconBar(); });
                this.tsIcons.ContextMenuStrip = menu;
                ToolStripMenuItem customize = new ToolStripMenuItem("&Customize Icon Bar…", null, delegate { CustomizeIconBar(); });
                this.toolbarsToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
                this.toolbarsToolStripMenuItem.DropDownItems.Add(customize);
            }
            if (this.sessions != null)
            {
                UpdateIconBar(); // not during construction: the panes' helpers come later
            }
        }

        private void CustomizeIconBar()
        {
            Dictionary<string, IconBarTool> tools = IconBarTools.ToDictionary(t => t.Id);
            List<string> current = IconBarLayout.Parse(SuperPuTTY.Settings.IconBarLayout, tools.Keys);
            using (dlgCustomizeIconBar dialog = new dlgCustomizeIconBar(IconBarTools, current, IconBarLayout.Parse(IconBarLayout.Default, tools.Keys)))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                string layout = IconBarLayout.Format(IconBarLayout.Normalize(dialog.Entries, tools.Keys));
                SuperPuTTY.Settings.IconBarLayout = layout == IconBarLayout.Format(IconBarLayout.Parse(IconBarLayout.Default, tools.Keys)) ? string.Empty : layout;
                SuperPuTTY.Settings.Save();
            }
            ApplyIconBarLayout();
        }

        /// <summary>Pressed state of the extra pane icons and Enabled state of the menu-backed icons</summary>
        private void UpdateExtraIconBarButtons()
        {
            if (this.iconBarTools == null)
            {
                return;
            }
            this.btnSessionDetailPane.Checked = IsOpen(this.sessionDetail);
            this.btnLogViewerPane.Checked = IsOpen(this.logViewer);
            foreach (IconBarTool tool in this.iconBarTools.Where(t => t.MenuItem != null))
            {
                tool.Button.Enabled = tool.MenuItem.Enabled;
            }
        }
    }
}
