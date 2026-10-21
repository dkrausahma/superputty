/*
 * Copyright (c) 2009 - 2015 Jim Radford http://www.jimradford.com
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions: 
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using WeifenLuo.WinFormsUI.Docking;
using SuperPutty.Data;
using log4net;
using System.Runtime.InteropServices;
using SuperPutty.Utils;
using SuperPuTTY.Scripting;
using System.Configuration;
using System.Linq;
using SuperPutty.Gui;
using SuperPutty.Scp;
using log4net.Core;
using System.Text.RegularExpressions;
using System.Runtime.Serialization.Json;
using System.Threading;

namespace SuperPutty
{
    public partial class frmSuperPutty : Form
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(frmSuperPutty));

        private static string XmlEditor = ConfigurationManager.AppSettings["SuperPuTTY.XmlEditor"];

        internal DockPanel DockPanel { get; private set; }

        public ToolWindowDocument CurrentPanel { get; set; }

        private SingletonToolWindowHelper<SessionTreeview> sessions;
        private SingletonToolWindowHelper<LayoutsList> layouts;
        private SingletonToolWindowHelper<Log4netLogViewer> logViewer;
        private SingletonToolWindowHelper<SessionDetail> sessionDetail;
        private SingletonToolWindowHelper<CommandsPane> commandsPane;
        private SingletonToolWindowHelper<ContainersPane> containersPane;

        private TextBoxFocusHelper tbFocusHelperHost;
        private TextBoxFocusHelper tbFocusHelperUserName;
        private TextBoxFocusHelper tbFocusHelperPassword;
        private frmDocumentSelector sendCommandsDocumentSelector;

        private NativeMethods.LowLevelKMProc llkp;
        //private NativeMethods.LowLevelKMProc llmp;
        private static IntPtr kbHookID = IntPtr.Zero;
        private static IntPtr mHookID = IntPtr.Zero;
        private bool forceClose;
        private FormWindowState lastNonMinimizedWindowState = FormWindowState.Normal;
        private Rectangle lastNormalDesktopBounds;
        private ChildWindowFocusHelper focusHelper;
        bool isControlDown = false;
        bool isShiftDown = false;
        bool isAltDown = false;
        int commandMRUIndex = -1;

        private readonly TabSwitcher tabSwitcher;
        private readonly ViewState kioskViewState;
        private bool isKioskMode = false;

        /// <summary>
        /// The quick connection toolbar (Protocol, Host, Login, Password, Session) is hidden for now, together with
        /// its View menu entry and shortcut. Set to true to bring it back; its code and settings are unchanged.
        /// </summary>
        private static readonly bool QuickConnectionBarEnabled = false;

        private Dictionary<Keys, SuperPuttyAction> shortcuts = new Dictionary<Keys, SuperPuttyAction>();

        /// <summary>Send command history, per session</summary>
        private readonly CommandHistory commandHistory;

        /// <summary>Triggers the health checks of the open tabs' hosts (each host at most every HealthMonitor.Interval)</summary>
        private readonly System.Windows.Forms.Timer healthTimer = new System.Windows.Forms.Timer { Interval = 30000 };

        /// <summary>Checks the SSH tunnels of the open tabs and updates their tab markers</summary>
        private readonly System.Windows.Forms.Timer tunnelTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        private PageantIndicator pageantIndicator;

        /// <summary>The main SuperPuTTY application form</summary>
        public frmSuperPutty()
        {
            // TLS version is left to the OS (SystemDefault on .NET 4.7+), which allows TLS 1.2 and 1.3
            // Verify Putty is set; Prompt user if necessary; exit otherwise
            dlgFindPutty.PuttyCheck();

            InitializeComponent();

            // DockPanelSuite 3.x needs an explicit theme; VS2005 matches the look of the 2.x library
            this.DockPanel.Theme = new VS2005Theme();
            // tool windows don't auto-hide (slide in/out); their captions get no pin button
            this.DockPanel.Theme.Extender.DockPaneCaptionFactory = new NoAutoHideCaptionFactory(this.DockPanel.Theme.Extender.DockPaneCaptionFactory);
            // tab tooltips also while PuTTY has the focus (the main window then counts as inactive)
            this.DockPanel.Theme.Extender.DockPaneStripFactory = new ShowAlwaysTabToolTipFactory(this.DockPanel.Theme.Extender.DockPaneStripFactory);
            // environment marker on terminal tabs (prod red bar and badge, qual amber, dev green)
            this.DockPanel.Theme.Extender.DockPaneStripFactory = new EnvironmentTabStripFactory(this.DockPanel.Theme.Extender.DockPaneStripFactory,
                content => content is ctlPuttyPanel ? ((ctlPuttyPanel)content).Environment : EnvironmentKind.None);
            // files dropped from Explorer on a terminal tab's header are uploaded to that host
            this.DockPanel.Theme.Extender.DockPaneStripFactory = new FileDropTabStripFactory(this.DockPanel.Theme.Extender.DockPaneStripFactory,
                content => content is ctlPuttyPanel && CanUpload(((ctlPuttyPanel)content).Session),
                (content, files) => UploadFiles((ctlPuttyPanel)content, files));

            // force toolbar locations...designer likes to flip them around
            // row 1: menu; row 2: icon bar with the command bar right after it; row 3: quick connection bar (hidden)
            this.tsIcons.Location = new System.Drawing.Point(0, 24);
            this.toolStripContainer1.TopToolStripPanel.Join(this.tsCommands,
                new System.Drawing.Point(this.tsIcons.Location.X + this.tsIcons.PreferredSize.Width, this.tsIcons.Location.Y));
            this.tsConnect.Location = new System.Drawing.Point(0, 49);

            if (this.DesignMode) return;

            // setup connection bar
            this.tbTxtBoxPassword.TextBox.PasswordChar = '*';
            this.RefreshConnectionToolbarData();

            // version in status bar
            this.toolStripStatusLabelVersion.Text = SuperPuTTY.Version;

            // tool windows
            this.sessions = new SingletonToolWindowHelper<SessionTreeview>("Sessions", this.DockPanel, null, x => new SessionTreeview(x.DockPanel));
            this.layouts = new SingletonToolWindowHelper<LayoutsList>("Layouts", this.DockPanel);
            this.logViewer = new SingletonToolWindowHelper<Log4netLogViewer>("Log Viewer", this.DockPanel);
            this.sessionDetail = new SingletonToolWindowHelper<SessionDetail>("Session Detail", this.DockPanel, this.sessions,
                                                                              x => {
                                                                                  return new SessionDetail(x.InitializerResource as SingletonToolWindowHelper<SessionTreeview>);
                                                                              });
            this.sessions.InstanceChanged += SessionsInstanceChanged;
            this.commandsPane = new SingletonToolWindowHelper<CommandsPane>("Commands", this.DockPanel, null, x => new CommandsPane(SendToActiveSession, RunInAllSessions, RunInNewTab, ActiveSession, RunInFolder));
            this.containersPane = new SingletonToolWindowHelper<ContainersPane>("Containers", this.DockPanel, null, x => new ContainersPane(ActiveSession));

            // for toolbar
            this.tbFocusHelperHost = new TextBoxFocusHelper(this.tbTxtBoxHost.TextBox);
            this.tbFocusHelperUserName = new TextBoxFocusHelper(this.tbTxtBoxLogin.TextBox);
            this.tbFocusHelperPassword = new TextBoxFocusHelper(this.tbTxtBoxPassword.TextBox);
            this.sendCommandsDocumentSelector = new frmDocumentSelector(this.DockPanel) {Owner = this};

            // Send Command toolbar history: saved history, filled into the combo for the active session
            this.commandHistory = SuperPuTTY.Settings.PersistCommandBarHistory
                ? CommandHistory.DeserializeXML(SuperPuTTY.Settings.CommandBarHistory)
                : new CommandHistory();
            this.commandHistory.Purge(SuperPuTTY.Settings.SaveCommandHistoryDays, DateTime.UtcNow);
            this.RefreshCommandHistory();

            // Hook into status
            SuperPuTTY.StatusEvent += new Action<string>(delegate(String msg) { this.toolStripStatusLabelMessage.Text = msg; });
            SuperPuTTY.ReportStatus("Ready");


            // Check for updates if enabled. (disabled if compiled with DEBUG)
            if (SuperPuTTY.Settings.AutoUpdateCheck)
            {
#if DEBUG
                Log.Info("Automatic Update Check Disabled in DEBUG mode");
#else
                Log.Info("Checking for updates");
                this.checkForUpdatesToolStripMenuItem_Click(this, new EventArgs());
#endif
            }
            // Hook into LayoutChanging/Changed
            SuperPuTTY.LayoutChanging += new EventHandler<LayoutChangedEventArgs>(SuperPuTTY_LayoutChanging);

            // Low-Level Mouse and Keyboard hooks
            llkp = KBHookCallback;
            kbHookID = SetKBHook(llkp);
            //llmp = MHookCallback;
            //mHookID = SetMHook(llmp);

            this.focusHelper = new ChildWindowFocusHelper(this);
            this.focusHelper.Start();

            // Restore window location and size
            if (SuperPuTTY.Settings.RestoreWindowLocation)
            {
                FormUtils.RestoreFormPositionAndState(this, SuperPuTTY.Settings.WindowPosition, SuperPuTTY.Settings.WindowState);
            }

            this.ResizeEnd += new EventHandler(frmSuperPutty_ResizeEnd);

            // tab switching
            this.tabSwitcher = new TabSwitcher(this.DockPanel);

            // kiosk
            this.kioskViewState = new ViewState(this);

            // command files of tabs opened with a remote command and relay folders, left behind by an earlier run
            RemoteCommandTab.CleanUp();
            FileTransferJob.CleanUp();
            PasswordFile.CleanUp();

            // Pageant state and key count in the status bar, before the version
            this.pageantIndicator = new PageantIndicator(this.statusStrip1, this.statusStrip1.Items.IndexOf(this.toolStripStatusLabelVersion));
            this.FormClosed += delegate { this.pageantIndicator.Dispose(); };

            // SSH tunnel markers on the tabs
            this.tunnelTimer.Tick += delegate { UpdateTunnelDisplays(); };
            this.tunnelTimer.Start();

            // health of the open tabs' hosts (session tree colours)
            this.healthTimer.Tick += delegate { CheckOpenTabsHealth(); };
            this.healthTimer.Start();
            this.healthChecksToolStripMenuItem.Checked = SuperPuTTY.Settings.AutoHealthCheck;
            this.sessionLoggingToolStripMenuItem.Checked = SuperPuTTY.Settings.SessionLogging;
            this.notifyActivityToolStripMenuItem.Checked = SuperPuTTY.Settings.NotifyBackgroundActivity;

            // Apply Settings
            this.ApplySettings();
            this.ApplySettingsToToolbars();

            this.DockPanel.ContentAdded += DockPanel_ContentAdded;
            this.DockPanel.ContentRemoved += DockPanel_ContentRemoved;
        }

        /// <summary>Fill the send command combo with the history of the active session, keeping typed text</summary>
        private void RefreshCommandHistory()
        {
            ctlPuttyPanel panel = this.DockPanel.ActiveDocument as ctlPuttyPanel;
            string sessionKey = panel == null ? null : CommandHistory.KeyFor(panel.Session);

            string text = this.tsSendCommandCombo.Text;
            this.tsSendCommandCombo.BeginUpdate();
            try
            {
                this.tsSendCommandCombo.Items.Clear();
                this.tsSendCommandCombo.Items.AddRange(this.commandHistory.GetCommands(sessionKey).ToArray());
            }
            finally
            {
                this.tsSendCommandCombo.EndUpdate();
            }
            this.tsSendCommandCombo.Text = text;
            this.commandMRUIndex = -1;
        }

        void DockPanel_ContentAdded(object sender, DockContentEventArgs e)
        {
            ctlPuttyPanel p = e.Content as ctlPuttyPanel;
            if (p != null)
            {
                p.TextChanged += puttyPanel_TextChanged;
                p.FilesDropped += UploadFiles;
                // readable copy of the session log once the tab is closed
                p.FormClosed += delegate { SessionLog.WriteCleanCopyAfterClose(p.LogFile); };
            }
            else
            {
                e.Content.DockHandler.DockStateChanged += ToolWindow_DockStateChanged;
            }
            QueueIconBarUpdate();
        }

        void DockPanel_ContentRemoved(object sender, DockContentEventArgs e)
        {
            ctlPuttyPanel p = e.Content as ctlPuttyPanel;
            if (p != null)
            {
                p.TextChanged -= puttyPanel_TextChanged;
                p.FilesDropped -= UploadFiles;
            }
            else
            {
                e.Content.DockHandler.DockStateChanged -= ToolWindow_DockStateChanged;
            }
            QueueIconBarUpdate();
        }

        void ToolWindow_DockStateChanged(object sender, EventArgs e)
        {
            QueueIconBarUpdate();
        }

        /// <summary>Tabs opened less than this ago don't report activity (their first titles arrive while connecting)</summary>
        private static readonly TimeSpan ActivityGrace = TimeSpan.FromSeconds(15);

        void puttyPanel_TextChanged(object sender, EventArgs e)
        {
            ctlPuttyPanel p = (ctlPuttyPanel)sender;
            if (p == this.DockPanel.ActiveDocument)
            {
                UpdateWindowText(p.Text);
            }
            else if (SuperPuTTY.Settings.NotifyBackgroundActivity && !p.HasActivity && DateTime.UtcNow - p.StartedUtc > ActivityGrace)
            {
                // a background tab's title changed, e.g. a long command finished and the prompt set the title again
                p.SetActivity(true);
                if (Form.ActiveForm == null)
                {
                    ShowNotification(p.Text, "Activity in a background tab (the command may have finished).");
                }
            }
        }

        /// <summary>A Windows notification from the tray icon, shown even when the icon is normally hidden</summary>
        private void ShowNotification(string title, string text)
        {
            bool wasVisible = this.notifyicon.Visible;
            this.notifyicon.Visible = true;
            this.notifyicon.ShowBalloonTip(5000, title, text, ToolTipIcon.Info);
            if (!wasVisible)
            {
                EventHandler hide = null;
                hide = delegate
                {
                    this.notifyicon.BalloonTipClosed -= hide;
                    this.notifyicon.BalloonTipClicked -= hide;
                    if (this.WindowState != FormWindowState.Minimized || !SuperPuTTY.Settings.MinimizeToTray)
                    {
                        this.notifyicon.Visible = false;
                    }
                };
                this.notifyicon.BalloonTipClosed += hide;
                this.notifyicon.BalloonTipClicked += hide;
            }
        }

        void UpdateWindowText(string text)
        {
            this.Text = string.Format("SuperPuTTY (dk@) - {0}", text);
        }

        private void frmSuperPutty_Load(object sender, EventArgs e)
        {
            this.BeginInvoke(new Action(this.LoadLayout));
        }
        
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // free hooks
            NativeMethods.UnhookWindowsHookEx(kbHookID);
            //NativeMethods.UnhookWindowsHookEx(mHookID);

            // save window size and location if not maximized or minimized
            if (SuperPuTTY.Settings.RestoreWindowLocation)// && this.WindowState != FormWindowState.Minimized)
            {
                SuperPuTTY.Settings.WindowPosition = this.lastNormalDesktopBounds;
                SuperPuTTY.Settings.WindowState = this.WindowState == FormWindowState.Minimized ? this.WindowState = this.lastNonMinimizedWindowState : this.WindowState;
                SuperPuTTY.Settings.Save();
            }

            // save layout for auto-restore
            if (SuperPuTTY.Settings.DefaultLayoutName == LayoutData.AutoRestore)
            {
                SaveLayout(SuperPuTTY.AutoRestoreLayoutPath, "Saving auto-restore layout");
            }

            if(SuperPuTTY.Settings.PersistCommandBarHistory)
            {
                SuperPuTTY.Settings.CommandBarHistory = this.commandHistory.SerializeXML();
                SuperPuTTY.Settings.Save();
            }

            this.focusHelper.Dispose();

            base.OnFormClosed(e);
        }

        void frmSuperPutty_ResizeEnd(object sender, EventArgs e)
        {
            SaveLastWindowBounds();
        }

        private void frmSuperPutty_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (SuperPuTTY.Settings.ExitConfirmation && !forceClose)
            {
                if (MessageBox.Show("Exit SuperPuTTY?", "Confirm Exit", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation) == DialogResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
        }

        /// <summary>
        /// Handles focusing on tabs/windows which host PuTTY
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dockPanel1_ActiveDocumentChanged(object sender, EventArgs e)
        {
            FocusActiveDocument("ActiveDocumentChanged");
            RefreshCommandHistory();
            ctlPuttyPanel activated = this.DockPanel.ActiveDocument as ctlPuttyPanel;
            if (activated != null)
            {
                activated.SetActivity(false);
            }
            UpdateIconBar();
            if (this.containersPane.Instance != null)
            {
                this.containersPane.Instance.ActiveSessionChanged();
            }
            if (this.commandsPane.Instance != null)
            {
                this.commandsPane.Instance.ActiveSessionChanged();
            }
        }

        public void FocusActiveDocument(string caller)
        {
            if (this.DockPanel.ActiveDocument == null)
            {
                this.Text = "SuperPuTTY (dk@)";
            }
            else
            {
                ToolWindowDocument window = this.DockPanel.ActiveDocument as ToolWindowDocument;
                if (window != null)
                {
                    // If we aren't using Ctrl-Tab to move between panels,
                    // i.e. we got here because the operator clicked on the
                    // panel directly, then record it as the current panel.
                    if (!this.tabSwitcher.IsSwitchingTabs)
                    {
                        this.tabSwitcher.CurrentDocument = window;
                    }

                    ctlPuttyPanel p = window as ctlPuttyPanel;
                    if (p != null)
                    {
                        p.SetFocusToChildApplication(caller);
                        this.UpdateWindowText(p.Text);
                    }
                }
            }
        }

        private void frmSuperPutty_Activated(object sender, EventArgs e)
        {
            Log.DebugFormat("[{0}] Activated", this.Handle);
            //dockPanel1_ActiveDocumentChanged(null, null);
        }

        public void SetActiveDocument(ToolWindow toolWindow)
        {
            if (this.DockPanel.ActiveDocument != toolWindow)
            {
                toolWindow.Show();
            }
        }

        #region File

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "XML Files|*.xml|All files|*.*",
                FileName = "Sessions.XML",
                InitialDirectory = Application.StartupPath
            };
            if (saveDialog.ShowDialog(this) == DialogResult.OK)
            {
                SessionData.SaveSessionsToFile(SuperPuTTY.GetAllSessions(), saveDialog.FileName);
            }
        }

        private void backUpSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "Zip files|*.zip|All files|*.*",
                FileName = string.Format("SuperPuTTY-backup-{0:yyyyMMdd-HHmm}.zip", DateTime.Now),
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (saveDialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }
            try
            {
                // write the current state first, so the backup holds what is shown
                SuperPuTTY.Settings.Save();
                SuperPuTTY.SaveSessions();
                int count = SettingsBackup.Create(saveDialog.FileName, SuperPuTTY.Settings.SettingsFilePath, SuperPuTTY.Settings.SettingsFolder, SuperPuTTY.Version);
                SuperPuTTY.ReportStatus("Backed up {0} files to {1}", count, saveDialog.FileName);
            }
            catch (Exception ex)
            {
                Log.Error("Backup failed", ex);
                MessageBox.Show(this, "The backup could not be written:\n" + ex.Message, "Back up Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void restoreSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = "Zip files|*.zip|All files|*.*",
                CheckFileExists = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (openDialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }
            try
            {
                string description = SettingsBackup.Describe(openDialog.FileName);
                if (description == null)
                {
                    MessageBox.Show(this, "This file is not a SuperPuTTY settings backup.", "Restore Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (MessageBox.Show(this,
                    (description.Length > 0 ? description + "\n\n" : string.Empty) +
                    "Settings, sessions, commands and layouts are replaced by the backup the next time SuperPuTTY starts. " +
                    "A backup of the current state is saved first.\n\nRestore this backup?",
                    "Restore Settings", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                {
                    return;
                }
                string safety = Path.Combine(SuperPuTTY.Settings.SettingsFolder, "backups", string.Format("before-restore-{0:yyyyMMdd-HHmmss}.zip", DateTime.Now));
                SuperPuTTY.Settings.Save();
                SettingsBackup.Create(safety, SuperPuTTY.Settings.SettingsFilePath, SuperPuTTY.Settings.SettingsFolder, SuperPuTTY.Version);
                SettingsBackup.StageRestore(openDialog.FileName, SettingsBackup.PendingFolder, SuperPuTTY.Settings.SettingsFolder);
                if (MessageBox.Show(this,
                    "The backup is restored when SuperPuTTY starts again. The previous state was saved to\n" + safety + "\n\nClose SuperPuTTY now?",
                    "Restore Settings", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    Close();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Restore failed", ex);
                MessageBox.Show(this, "The backup could not be restored:\n" + ex.Message, "Restore Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void fromFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = "XML Files|*.xml|All files|*.*",
                FileName = "Sessions.XML",
                CheckFileExists = true,
                InitialDirectory = Application.StartupPath
            };
            if (openDialog.ShowDialog(this) == DialogResult.OK)
            {
                SuperPuTTY.ImportSessionsFromFile(openDialog.FileName);
            }
        }


        private void fromPuTTYCMExportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = "XML Files|*.xml|All files|*.*",
                FileName = "export.xml",
                CheckFileExists = true,
                InitialDirectory = Application.StartupPath
            };
            if (openDialog.ShowDialog(this) == DialogResult.OK)
            {
                SuperPuTTY.ImportSessionsFromPuttyCM(openDialog.FileName);
            }
        }


        private void fromPuTTYSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult res = MessageBox.Show(
                "Do you want to copy all sessions from PuTTY/KiTTY?  Duplicates may be created.",
                "SuperPuTTY",
                MessageBoxButtons.YesNo);
            if (res == DialogResult.Yes)
            {
                SuperPuTTY.ImportSessionsFromPuTTY();
            }
        }

        private void openSessionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            QuickSelector q = new QuickSelector();
            QuickSelectorData data = new QuickSelectorData
            {
                CaseSensitive = SuperPuTTY.Settings.QuickSelectorCaseSensitiveSearch
            };

            foreach (SessionData sd in SuperPuTTY.Sessions)
            {
                data.ItemData.AddItemDataRow(
                    sd.SessionName,
                    sd.SessionId,
                    sd.Proto == ConnectionProtocol.Cygterm || sd.Proto == ConnectionProtocol.Mintty ? Color.Blue : Color.Black,
                    null);
            }

            QuickSelectorOptions opt = new QuickSelectorOptions
            {
                Sort = data.ItemData.DetailColumn.ColumnName,
                BaseText = "Open Session"
            };

            QuickSelector d = new QuickSelector();
            if (d.ShowDialog(this, data, opt) == DialogResult.OK)
            {
                SuperPuTTY.OpenPuttySession(d.SelectedItem.Detail);
            }
        }

        private void switchSessionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            QuickSelector q = new QuickSelector();
            QuickSelectorData data = new QuickSelectorData
            {
                CaseSensitive = SuperPuTTY.Settings.QuickSelectorCaseSensitiveSearch
            };

            foreach (ToolWindow content in this.tabSwitcher.Documents)
            {
                ctlPuttyPanel panel = content as ctlPuttyPanel;
                if (content != null)
                {
                    SessionData sd = panel.Session;
                    data.ItemData.AddItemDataRow(
                        panel.Text,
                        sd.SessionId,
                        sd.Proto == ConnectionProtocol.Cygterm || sd.Proto == ConnectionProtocol.Mintty ? Color.Blue : Color.Black,
                        panel);
                }
            }

            QuickSelectorOptions opt = new QuickSelectorOptions
            {
                Sort = data.ItemData.DetailColumn.ColumnName,
                BaseText = "Switch Session",
                ShowNameColumn = true
            };

            QuickSelector d = new QuickSelector();
            if (d.ShowDialog(this, data, opt) == DialogResult.OK)
            {
                ctlPuttyPanel panel = (ctlPuttyPanel)d.SelectedItem.Tag;
                panel.Activate();
            }
        }

        private void editSessionsInNotepadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo(XmlEditor ?? "notepad", Path.Combine(SuperPuTTY.Settings.SettingsFolder, "Sessions.XML")) { UseShellExecute = true });
        }

        private void reloadSessionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SuperPuTTY.LoadSessions();
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            this.Close();
        }
        #endregion

        #region View Menu

        private void toggleCheckedState(object sender, EventArgs e)
        {
            // toggle
            ToolStripMenuItem mi = (ToolStripMenuItem)sender;
            mi.Checked = !mi.Checked;

            // save
            SuperPuTTY.Settings.ShowStatusBar = this.showStatusBarToolStripMenuItem.Checked;
            SuperPuTTY.Settings.ShowToolBarConnections = this.quickConnectionToolStripMenuItem.Checked;
            SuperPuTTY.Settings.ShowToolBarCommands = this.sendCommandsToolStripMenuItem.Checked;
            SuperPuTTY.Settings.ShowToolBarIcons = this.iconsToolStripMenuItem.Checked;
            SuperPuTTY.Settings.AlwaysOnTop = this.alwaysOnTopToolStripMenuItem.Checked;
            SuperPuTTY.Settings.ShowMenuBar = this.showMenuBarToolStripMenuItem.Checked;

            SuperPuTTY.Settings.Save();

            // apply
            ApplySettingsToToolbars();
        }

        void ApplySettingsToToolbars()
        {
            this.statusStrip1.Visible = SuperPuTTY.Settings.ShowStatusBar;
            this.showStatusBarToolStripMenuItem.Checked = SuperPuTTY.Settings.ShowStatusBar;

            this.tsConnect.Visible = QuickConnectionBarEnabled && SuperPuTTY.Settings.ShowToolBarConnections;
            this.quickConnectionToolStripMenuItem.Checked = SuperPuTTY.Settings.ShowToolBarConnections;
            this.quickConnectionToolStripMenuItem.Visible = QuickConnectionBarEnabled;

            this.tsCommands.Visible = SuperPuTTY.Settings.ShowToolBarCommands;
            this.sendCommandsToolStripMenuItem.Checked = SuperPuTTY.Settings.ShowToolBarCommands;

            this.tsIcons.Visible = SuperPuTTY.Settings.ShowToolBarIcons;
            this.iconsToolStripMenuItem.Checked = SuperPuTTY.Settings.ShowToolBarIcons;

            this.TopMost = SuperPuTTY.Settings.AlwaysOnTop;
            this.alwaysOnTopToolStripMenuItem.Checked = SuperPuTTY.Settings.AlwaysOnTop;

            this.menuStrip1.Visible = SuperPuTTY.Settings.ShowMenuBar;
            this.showMenuBarToolStripMenuItem.Checked = SuperPuTTY.Settings.ShowMenuBar;
        }

        #region Icon bar

        private static bool IsOpen<T>(SingletonToolWindowHelper<T> window) where T : ToolWindow
        {
            return window.Instance != null && !window.Instance.IsHidden;
        }

        /// <summary>Sessions icon: opens the Sessions pane (with Session Detail), or closes it when pressed</summary>
        private void btnSessionsPane_Click(object sender, EventArgs e)
        {
            if (IsOpen(this.sessions))
            {
                this.sessions.Instance.Close();
            }
            else
            {
                this.sessionsToolStripMenuItem_Click(sender, e);
            }
            UpdateIconBar();
        }

        /// <summary>Layouts icon: opens the Layouts pane, or closes it when pressed</summary>
        /// <summary>Commands icon: opens the Commands pane, or closes it when pressed</summary>
        private void btnCommandsPane_Click(object sender, EventArgs e)
        {
            if (IsOpen(this.commandsPane))
            {
                this.commandsPane.Instance.Close();
            }
            else
            {
                this.commandsPaneToolStripMenuItem_Click(sender, e);
            }
            UpdateIconBar();
        }

        /// <summary>Containers icon: opens the Containers pane, or closes it when pressed</summary>
        private void btnContainersPane_Click(object sender, EventArgs e)
        {
            if (IsOpen(this.containersPane))
            {
                this.containersPane.Instance.Close();
            }
            else
            {
                this.containersPaneToolStripMenuItem_Click(sender, e);
            }
            UpdateIconBar();
        }

        private void btnLayoutsPane_Click(object sender, EventArgs e)
        {
            if (IsOpen(this.layouts))
            {
                this.layouts.Instance.Close();
            }
            else
            {
                this.layoutsToolStripMenuItem_Click(sender, e);
            }
            UpdateIconBar();
        }

        /// <summary>Show each icon as pressed while its pane is open or its mode is on</summary>
        private void UpdateIconBar()
        {
            this.btnSessionsPane.Checked = IsOpen(this.sessions);
            this.btnLayoutsPane.Checked = IsOpen(this.layouts);
            this.btnCommandsPane.Checked = IsOpen(this.commandsPane);
            this.btnContainersPane.Checked = IsOpen(this.containersPane);
            this.btnKioskToggle.Checked = this.isKioskMode;

            ctlPuttyPanel panel = this.DockPanel.ActiveDocument as ctlPuttyPanel;
            bool canCopy = panel != null && panel.SupportsPuttyCommands;
            this.btnCopySession.Enabled = canCopy;
            this.copySessionToolStripMenuItem.Enabled = canCopy;
            this.copyLastCommandToolStripMenuItem.Enabled = canCopy;
            this.explainInClaudeToolStripMenuItem.Enabled = canCopy;
            this.openSessionLogToolStripMenuItem.Enabled = panel != null && panel.LogFile != null;
            this.btnUploadFiles.Enabled = panel != null && CanUpload(panel.Session);
        }

        /// <summary>Update the icon bar once the current docking operation has finished</summary>
        private void QueueIconBarUpdate()
        {
            if (this.IsDisposed || this.Disposing)
            {
                return;
            }
            if (this.IsHandleCreated)
            {
                this.BeginInvoke((MethodInvoker)UpdateIconBar);
            }
            else
            {
                UpdateIconBar();
            }
        }

        #endregion

        /// <summary>Show the Sessions pane; when it opens, Session Detail opens below it by default</summary>
        private void sessionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool wasOpen = this.sessions.Instance != null && !this.sessions.Instance.IsHidden;
            ShowSessionsPane();
            if (!wasOpen)
            {
                ShowSessionDetail();
            }
        }

        private void ShowSessionsPane()
        {
            if (this.layouts.IsVisibleAsToolWindow || this.sessionDetail.IsVisibleAsToolWindow)
            {
                DockPane Pane = layouts.IsVisibleAsToolWindow ? layouts.Instance.DockHandler.Pane : sessionDetail.Instance.DockHandler.Pane;
                this.sessions.ShowWindow(Pane, DockAlignment.Top, 0.5);
            }
            else
            {
                this.sessions.ShowWindow(DockState.DockRight);
            }
        }

        private void commandsPaneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.commandsPane.ShowWindow(DockState.DockLeft);
        }

        /// <summary>Containers pane: below the Commands pane if that is docked, otherwise on the left</summary>
        private void containersPaneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.containersPane.Instance == null && this.commandsPane.IsVisibleAsToolWindow
                && this.commandsPane.Instance.DockHandler.Pane.DockState != DockState.Float)
            {
                this.containersPane.ShowWindow(this.commandsPane.Instance.DockHandler.Pane, DockAlignment.Bottom, 0.5);
            }
            else
            {
                this.containersPane.ShowWindow(DockState.DockLeft);
            }
        }

        private void logViewerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.logViewer.ShowWindow(DockState.DockBottom);
        }


        private void layoutsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.sessionDetail.IsVisibleAsToolWindow)
            {
                // below Session Detail, which keeps its own pane under Sessions
                this.layouts.ShowWindow(this.sessionDetail.Instance.Pane, DockAlignment.Bottom, 0.5);
            }
            else if (this.sessions.IsVisibleAsToolWindow)
            {
                this.layouts.ShowWindow(sessions.Instance.DockHandler.Pane, DockAlignment.Bottom, 0.5);
            }
            else
            {
                this.layouts.ShowWindow(DockState.DockRight);
            }
        }

        /// <summary>
        /// Session Detail shows the session selected in the Sessions tree, so it opens the Sessions window if
        /// needed and takes the lower half of the Sessions pane.
        /// </summary>
        private void sessionDetailMenuItem_Click(object sender, EventArgs e)
        {
            ShowSessionDetail();
        }

        private void ShowSessionDetail()
        {
            if (this.sessions.Instance == null || this.sessions.Instance.IsHidden)
            {
                ShowSessionsPane();
            }

            DockPane sessionsPane = this.sessions.Instance.DockHandler.Pane;
            SessionDetail detail = this.sessionDetail.Instance ?? this.sessionDetail.Initialize();
            if (sessionsPane.DockState == DockState.Float)
            {
                // a floating pane can't be split; join it as a tab instead
                if (detail.DockHandler.Pane != sessionsPane)
                {
                    detail.Show(sessionsPane, null);
                }
            }
            else if (!IsAloneBelow(detail, sessionsPane))
            {
                detail.Show(sessionsPane, DockAlignment.Bottom, 0.5);
            }
            detail.Activate();
            SuperPuTTY.ReportStatus("Showing " + this.sessionDetail.Name);
        }

        /// <summary>True if the content has a pane of its own, docked directly below the given pane</summary>
        private static bool IsAloneBelow(DockContent content, DockPane above)
        {
            DockPane pane = content.DockHandler.Pane;
            return pane != null && pane != above && !content.IsHidden
                && pane.DisplayingContents.Count == 1
                && pane.NestedDockingStatus.PreviousPane == above
                && pane.NestedDockingStatus.Alignment == DockAlignment.Bottom;
        }

        /// <summary>Session Detail is useless without the Sessions tree, so it closes with it (except during a layout switch)</summary>
        private void SessionsInstanceChanged(SessionTreeview tree)
        {
            if (tree == null)
            {
                return;
            }
            tree.FormClosed += delegate
            {
                if (!SuperPuTTY.IsLayoutChanging && this.sessionDetail.Instance != null)
                {
                    this.sessionDetail.Instance.Close();
                }
            };
        }


        #endregion

        #region Layout

        void LoadLayout()
        {
            String dir = SuperPuTTY.LayoutsDir;
            if (Directory.Exists(dir))
            {
                this.openFileDialogLayout.InitialDirectory = dir;
                this.saveFileDialogLayout.InitialDirectory = dir;
            }

            if (SuperPuTTY.StartingSession != null)
            {
                // coming from command line, so no don't load any layout
                //SuperPuTTY.LoadLayout(null);
                SuperPuTTY.OpenSession(SuperPuTTY.StartingSession);
            }
            else
            {
                // default layout or null for hard-coded default
                SuperPuTTY.LoadLayout(SuperPuTTY.StartingLayout);
                SuperPuTTY.ApplyDockRestrictions(this.DockPanel);
            }
        }

        void SuperPuTTY_LayoutChanging(object sender, LayoutChangedEventArgs eventArgs)
        {
            if (eventArgs.IsNewLayoutAlreadyActive)
            {
                toolStripStatusLabelLayout.Text = eventArgs.New.Name;
            }
            else
            {
                // reset old layout (close old putty instances)
                foreach (DockContent dockContent in this.DockPanel.DocumentsToArray())
                {
                    Log.Debug("Unhooking document: " + dockContent);
                    dockContent.DockPanel = null;
                    // close old putty
                    if (dockContent.CloseButtonVisible)
                    {
                        dockContent.Close();
                    }
                }
                List<DockContent> contents = this.DockPanel.Contents.Cast<DockContent>().ToList();
                foreach (DockContent dockContent in contents)
                {
                    Log.Debug("Unhooking dock content: " + dockContent);
                    dockContent.DockPanel = null;
                    // close non-persistant windows
                    if (dockContent.CloseButtonVisible)
                    {
                        dockContent.Close();
                    }
                }


                if (eventArgs.New == null)
                {
                    // 1st time or reset
                    Log.Debug("Initializing default layout");
                    this.InitDefaultLayout();
                    toolStripStatusLabelLayout.Text = "";
                    SuperPuTTY.ReportStatus("Initialized default layout");
                }
                else if (!File.Exists(eventArgs.New.FilePath))
                {
                    // file missing
                    Log.WarnFormat("Layout file doesn't exist, file={0}", eventArgs.New.FilePath);
                    this.InitDefaultLayout();
                    toolStripStatusLabelLayout.Text = eventArgs.New.Name;
                    SuperPuTTY.ReportStatus("Could not load layout, file missing: {0}", eventArgs.New.FilePath);
                }
                else
                {
                    // load new one
                    Log.DebugFormat("Loading layout: {0}", eventArgs.New.FilePath);
                    this.DockPanel.LoadFromXml(eventArgs.New.FilePath, RestoreLayoutFromPersistString);
                    DockAutoHiddenWindows();
                    toolStripStatusLabelLayout.Text = eventArgs.New.Name;
                    SuperPuTTY.ReportStatus("Loaded layout: {0}", eventArgs.New.FilePath);
                }

                // after all is done, cause a repaint to 
            }
        }

        /// <summary>Layouts saved by earlier versions may contain auto-hidden tool windows; dock them instead</summary>
        void DockAutoHiddenWindows()
        {
            foreach (DockContent content in this.DockPanel.Contents.Cast<DockContent>().ToList())
            {
                if (DockHelper.IsDockStateAutoHide(content.DockState))
                {
                    Log.InfoFormat("Docking auto-hidden window from layout: {0}", content.Text);
                    content.DockState = DockHelper.ToggleAutoHideState(content.DockState);
                }
            }
        }

        void InitDefaultLayout()
        {
            this.sessionsToolStripMenuItem_Click(this, EventArgs.Empty);
            this.layoutsToolStripMenuItem_Click(this, EventArgs.Empty);
        }

        private void saveLayoutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SuperPuTTY.CurrentLayout != null)
            {
                String file = SuperPuTTY.CurrentLayout.FilePath;
                SaveLayout(file, string.Format("Saving layout: {0}", file));
            }
            else
            {
                saveLayoutAsToolStripMenuItem_Click(sender, e);
            }
        }

        private void saveLayoutAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (DialogResult.OK == this.saveFileDialogLayout.ShowDialog(this))
            {
                String file = this.saveFileDialogLayout.FileName;
                SaveLayout(file, string.Format("Saving layout as: {0}", file));
                SuperPuTTY.AddLayout(file);
            }
        }

        void SaveLayout(string file, string statusMsg)
        {
            SuperPuTTY.ReportStatus(statusMsg);
            this.DockPanel.SaveAsXml(file);
        }

        private IDockContent RestoreLayoutFromPersistString(String persistString)
        {
            if (typeof(SessionTreeview).FullName == persistString)
            {
                // session tree
                return this.sessions.Instance ?? this.sessions.Initialize();
            }
            else if (typeof(LayoutsList).FullName == persistString)
            {
                // layouts list
                return this.layouts.Instance ?? this.layouts.Initialize();
            }
            else if (typeof(Log4netLogViewer).FullName == persistString)
            {
                return this.logViewer.Instance ?? this.logViewer.Initialize();
            }
            else if (typeof(ContainersPane).FullName == persistString)
            {
                return this.containersPane.Instance ?? this.containersPane.Initialize();
            }
            else if (typeof(CommandsPane).FullName == persistString)
            {
                return this.commandsPane.Instance ?? this.commandsPane.Initialize();
            }
            else if (typeof(SessionDetail).FullName == persistString)
            {
                return this.sessionDetail.Instance ?? this.sessionDetail.Initialize();
            }
            else
            {
                // putty session
                ctlPuttyPanel puttyPanel = ctlPuttyPanel.FromPersistString(persistString);
                if (puttyPanel != null)
                {
                    return puttyPanel;
                }                
            }
            return null;
        }


        #endregion

        #region Tools

        private void puTTYConfigurationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process p = new Process {StartInfo = {FileName = SuperPuTTY.Settings.PuttyExe}};
            p.Start();

            SuperPuTTY.ReportStatus("Lauched Putty Configuration");
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SuperPuTTY.ReportStatus("Editing Options");

            dlgFindPutty dialog = new dlgFindPutty();
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                ApplySettings();
            }

            SuperPuTTY.ReportStatus("Ready");
        }

        void ApplySettings()
        {
            SuperPuTTY.ApplyDockRestrictions(this.DockPanel);

            // apply tab switching strategy change
            this.tabSwitcher.TabSwitchStrategy = TabSwitcher.StrategyFromTypeName(SuperPuTTY.Settings.TabSwitcher);

            this.SaveLastWindowBounds();
            this.UpdateShortcutsFromSettings();
            this.DockPanel.ShowDocumentIcon = SuperPuTTY.Settings.ShowDocumentIcons;
        }

        #endregion

        #region Help Menu
        private void aboutSuperPuttyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBox1 about = new AboutBox1();
            about.ShowDialog(this);
            about = null;
        }

        private void superPuttyWebsiteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/jimradford/superputty/") { UseShellExecute = true });
        }

        private void helpToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (File.Exists(Application.StartupPath + @"\superputty.chm"))
            {
                Process.Start(new ProcessStartInfo(Application.StartupPath + @"\superputty.chm") { UseShellExecute = true });
            }
            else
            {
                DialogResult result = MessageBox.Show("Local documentation could not be found. Would you like to view the documentation online instead?", "Documentation Not Found", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo("https://github.com/jimradford/superputty/wiki/Documentation") { UseShellExecute = true });
                }
            }
        }

        #endregion

        #region Toolbar


        private string oldHostName;

        private void tbBtnConnect_Click(object sender, EventArgs e)
        {
            TryConnectFromToolbar();
        }

        private void tbItemConnect_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                TryConnectFromToolbar();
                e.Handled = true;
            }
        }

        void TryConnectFromToolbar()
        {
            String host = this.tbTxtBoxHost.Text;
            String protoString = (string)this.tbComboProtocol.SelectedItem;

            if (!String.IsNullOrEmpty(host))
            {
                bool isScp = "SCP" == protoString;
                bool isVnc = "VNC" == protoString;
                HostConnectionString connStr = new HostConnectionString(host, isVnc);
                ConnectionProtocol proto = isScp
                    ? ConnectionProtocol.SSH
                    : connStr.Protocol.GetValueOrDefault((ConnectionProtocol)Enum.Parse(typeof(ConnectionProtocol), protoString));
                SessionData session = new SessionData
                {
                    Host = connStr.Host,
                    SessionName = connStr.Host,
                    SessionId = SuperPuTTY.MakeUniqueSessionId(SessionData.CombineSessionIds("ConnectBar", connStr.Host)),
                    Proto = proto,
                    Port = connStr.Port.GetValueOrDefault(dlgEditSession.GetDefaultPort(proto)),
                    Username = this.tbTxtBoxLogin.Text,
                    Password = this.tbTxtBoxPassword.Text,
                    PuttySession = (string)this.tbComboSession.SelectedItem
                };
                SuperPuTTY.OpenSession(new SessionDataStartInfo { Session = session, UseScp = isScp });
                oldHostName = this.tbTxtBoxHost.Text;
                RefreshConnectionToolbarData();
            }
        }

        void RefreshConnectionToolbarData()
        {
            if (this.tbComboProtocol.Items.Count == 0)
            {
                this.tbComboProtocol.Items.Clear();
                foreach (ConnectionProtocol protocol in Enum.GetValues(typeof(ConnectionProtocol)))
                {
                    this.tbComboProtocol.Items.Add(protocol.ToString());
                }
                this.tbComboProtocol.Items.Add("SCP");
                this.tbComboProtocol.SelectedItem = ConnectionProtocol.SSH.ToString();
            }

            String prevSession = (string)this.tbComboSession.SelectedItem;
            this.tbComboSession.Items.Clear();
            foreach (string sessionName in PuttyDataHelper.GetSessionNames())
            {
                this.tbComboSession.Items.Add(sessionName);
            }
            this.tbComboSession.SelectedItem = prevSession ?? PuttyDataHelper.SessionEmptySettings;
        }

        private void toolStripButtonClearFields_Click(object sender, EventArgs e)
        {
            this.tbComboProtocol.SelectedItem = ConnectionProtocol.SSH.ToString();
            this.tbTxtBoxHost.Clear();
            this.tbTxtBoxLogin.Clear();
            this.tbTxtBoxPassword.Clear();
            this.tbComboSession.SelectedItem = PuttyDataHelper.SessionEmptySettings;
        }

        /// <summary>
        /// Show selector below toolbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tsBtnSelectDocs_Click(object sender, EventArgs e)
        {
            Rectangle rect = this.tbBtnSelectDocs.Bounds;
            int top = this.tsCommands.Top + this.tsCommands.Height + 3;
            int left = rect.Left + rect.Width - this.sendCommandsDocumentSelector.Width + 3;
            this.sendCommandsDocumentSelector.StartPosition = FormStartPosition.Manual;
            this.sendCommandsDocumentSelector.Location = this.PointToScreen(new Point(left, top));
            this.sendCommandsDocumentSelector.Show();
        }

        private void tsSendCommandCombo_KeyDown(object sender, KeyEventArgs e)
        {
            if (Log.Logger.IsEnabledFor(Level.Trace))
            {
                Log.DebugFormat("Keys={0}, control={1}, shift={2}, keyData={3}", e.KeyCode, e.Control, e.Shift, e.KeyData);
            }
            if (e.KeyCode == Keys.Down)
            {
                if (tsSendCommandCombo.Items.Count > 0)
                {
                    commandMRUIndex--;
                    if (commandMRUIndex < 0)
                    {
                        commandMRUIndex = tsSendCommandCombo.Items.Count - 1;
                    }
                    if (commandMRUIndex >= 0)
                    {
                        tsSendCommandCombo.SelectedIndex = commandMRUIndex;                        
                        tsSendCommandCombo.SelectionStart = tsSendCommandCombo.Text.Length;
                    }
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                if (tsSendCommandCombo.Items.Count > 0)
                {
                    commandMRUIndex++;
                    if (commandMRUIndex >= tsSendCommandCombo.Items.Count)
                    {
                        commandMRUIndex = 0;
                    }
                    if (commandMRUIndex < tsSendCommandCombo.Items.Count)
                    {
                        tsSendCommandCombo.SelectedIndex = commandMRUIndex;
                        tsSendCommandCombo.SelectionStart = tsSendCommandCombo.Text.Length;
                    }
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                // send commands
                TrySendCommandsFromToolbar(new CommandData(this.tsSendCommandCombo.Text, new KeyEventArgs(Keys.Enter)), !this.tbBtnMaskText.Checked);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode != Keys.ControlKey)
            {
                // special keys
                TrySendCommandsFromToolbar(new CommandData(e), !this.tbBtnMaskText.Checked);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }


        private void tbBtnSendCommand_Click(object sender, EventArgs e)
        {
            TrySendCommandsFromToolbar(!this.tbBtnMaskText.Checked);
        }

        private void toggleCommandMaskToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.tbBtnMaskText.PerformClick();
        }

        private void tbBtnMaskText_Click(object sender, EventArgs e)
        {
            IntPtr handle = NativeMethods.GetWindow(this.tsSendCommandCombo.ComboBox.Handle, NativeMethods.GetWindowCmd.GW_CHILD);
            if (this.tbBtnMaskText.Checked)
            {
                NativeMethods.SendMessage(handle, NativeMethods.EM_SETPASSWORDCHAR, (int)'*', 0L);
            }
            else
            {
                NativeMethods.SendMessage(handle, NativeMethods.EM_SETPASSWORDCHAR, 0, 0L);
            }
            this.tsSendCommandCombo.ComboBox.Refresh();
        }

        private void notifyActivityToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.notifyActivityToolStripMenuItem.Checked = !this.notifyActivityToolStripMenuItem.Checked;
            SuperPuTTY.Settings.NotifyBackgroundActivity = this.notifyActivityToolStripMenuItem.Checked;
            SuperPuTTY.Settings.Save();
            if (!SuperPuTTY.Settings.NotifyBackgroundActivity)
            {
                foreach (ctlPuttyPanel panel in this.DockPanel.Contents.OfType<ctlPuttyPanel>())
                {
                    panel.SetActivity(false);
                }
            }
        }

        private void sessionLoggingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.sessionLoggingToolStripMenuItem.Checked = !this.sessionLoggingToolStripMenuItem.Checked;
            SuperPuTTY.Settings.SessionLogging = this.sessionLoggingToolStripMenuItem.Checked;
            SuperPuTTY.Settings.Save();
            SuperPuTTY.ReportStatus(SuperPuTTY.Settings.SessionLogging
                ? "Session logging on: new tabs write their output to " + SessionLog.Folder
                : "Session logging off for new tabs");
        }

        /// <summary>Open the active tab's session log as plain text (control codes removed)</summary>
        private void openSessionLog_Click(object sender, EventArgs e)
        {
            ctlPuttyPanel panel = this.DockPanel.ActiveDocument as ctlPuttyPanel;
            if (panel == null || panel.LogFile == null || !File.Exists(panel.LogFile))
            {
                SuperPuTTY.ReportStatus("The active tab has no session log (turn on View > Log Sessions to Files, then open the session)");
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo(SessionLog.WriteCleanCopy(panel.LogFile)) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.ComponentModel.Win32Exception)
            {
                MessageBox.Show(this, "Could not open the session log:\n" + ex.Message, "Session Log", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void openLogFolder_Click(object sender, EventArgs e)
        {
            Directory.CreateDirectory(SessionLog.Folder);
            Process.Start(new ProcessStartInfo(SessionLog.Folder) { UseShellExecute = true });
        }

        #endregion

        #region Tray
        private void frmSuperPutty_Resize(object sender, EventArgs e)
        {
            if (SuperPuTTY.Settings.MinimizeToTray)
            {
                if (FormWindowState.Minimized == this.WindowState && !notifyicon.Visible)
                {
                    notifyicon.Visible = true;
                    this.ShowInTaskbar = false;

                }
                else if (FormWindowState.Normal == this.WindowState || FormWindowState.Maximized == this.WindowState)
                {
                    notifyicon.Visible = false;
                    this.lastNonMinimizedWindowState = this.WindowState;
                }
            }

            SaveLastWindowBounds();
        }

        private void SaveLastWindowBounds()
        {
            if (this.WindowState == FormWindowState.Normal)
            {
                this.lastNormalDesktopBounds = this.DesktopBounds;
            }
        }

        private void notifyicon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.ShowInTaskbar = true;
                this.WindowState = this.lastNonMinimizedWindowState;
            }
        }

        private void exitSuperPuTTYToolStripMenuItem_Click(object sender, EventArgs e)
        {
            forceClose = true;
            this.Close();
        }

        #endregion

        #region Diagnostics

        private void logWindowLocationsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (IDockContent c in this.DockPanel.Documents)
            {
                ctlPuttyPanel panel = c as ctlPuttyPanel;
                if (c != null)
                {
                    NativeMethods.RECT rect = new NativeMethods.RECT();
                    NativeMethods.GetWindowRect(panel.AppPanel.AppWindowHandle, ref rect);
                    Point p = panel.PointToScreen(new Point());
                    Log.InfoFormat(
                        "[{0,-20} {1,8}] WindowLocations: panel={2}, putty={3}, x={4}, y={5}",
                        panel.Text + (panel == panel.DockPanel.ActiveDocument ? "*" : ""),
                        panel.AppPanel.AppWindowHandle,
                        panel.DisplayRectangle,
                        rect, p.X, p.Y);
                }
            }
        }

        private void cleanUpStrayProcessesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                Regex regex = new Regex(@"^(putty|pscp|cthelper|bash|mintty)$", RegexOptions.IgnoreCase);
                IDictionary<string, List<Process>> procs = new Dictionary<string, List<Process>>();
                foreach (Process p in Process.GetProcesses())
                {
                    if (regex.IsMatch(p.ProcessName))
                    {
                        List<Process> procList;
                        if (!procs.TryGetValue(p.ProcessName, out procList))
                        {
                            procList = new List<Process>();
                            procs.Add(p.ProcessName, procList);
                        }
                        procList.Add(p);
                    }
                }

                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<string, List<Process>> plist in procs)
                {
                    sb.AppendFormat("{0} ({1})", plist.Key, plist.Value.Count).AppendLine();
                }
                if (procs.Count > 0 && DialogResult.OK == MessageBox.Show(this, sb.ToString(), "Kill Processes?", MessageBoxButtons.OKCancel))
                {
                    int success = 0;
                    int error = 0;
                    foreach (KeyValuePair<string, List<Process>> plist in procs)
                    {
                        foreach (Process procToKill in plist.Value)
                        {
                            try
                            {
                                procToKill.Kill();
                                success++;
                            }
                            catch (Exception ex)
                            {
                                Log.ErrorFormat("Error killing proc: {0} ({1})", procToKill.ProcessName, procToKill.Id, ex);
                                error++;
                            }
                        }
                    }
                    MessageBox.Show(this, string.Format("Killed {0} processes, {1} errors", success, error), "Clean Up Complete");
                }
            }
            catch (Exception ex)
            {
                string msg = "";
                Log.Error(msg, ex);
                MessageBox.Show(this, msg, "Error Cleaning Processes");
            }
        }

        private void menuStrip1_MenuDeactivate(object sender, EventArgs e)
        {
            menuStrip1.Visible = SuperPuTTY.Settings.ShowMenuBar;
        }

        /// <summary>Check for a newer version of the SuperPuTTY Application</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkForUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Log.Info("Checking for application update");
            try {
                httpRequest httpUpdateRequest = new httpRequest();
                // a manual check (menu) reports every outcome; the automatic one at start-up only logs failures
                bool manual = sender == this.checkForUpdatesToolStripMenuItem;
                httpUpdateRequest.MakeRequest(UpdateCheck.ReleasesUrl, delegate (bool success, string content)
                {
                    if (success)
                    {
                        DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(GitRelease));
                        MemoryStream ms = new MemoryStream(Encoding.ASCII.GetBytes(content));
                        GitRelease latest = (GitRelease)js.ReadObject(ms);
                        ms.Close();

                        if (UpdateCheck.IsNewer(latest.version, SuperPuTTY.Version))
                        {
                            Log.Info("New Application version found! " + latest.version);

                            if (MessageBox.Show("An updated version of SuperPuTTY (" + latest.version + ") is Available Would you like to visit the download page to upgrade?",
                                "SuperPutty Update Found",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question,
                                MessageBoxDefaultButton.Button1,
                                MessageBoxOptions.DefaultDesktopOnly) == DialogResult.Yes)
                            {
                                Process.Start(new ProcessStartInfo(latest.release_url) { UseShellExecute = true });
                            }
                        }
                        else
                        {
                            if (manual)
                            {
                                MessageBox.Show("You are running the latest version of SuperPutty", "SuperPuTTY Update Check", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    }
                    else
                    {
                        Log.Warn("An Error occurred trying to check for program updates: " + content);
                        if (manual)
                        {
                            MessageBox.Show("There was an error while checking for updates. Please try again later.", "Error during update check", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                });
            }
            catch (System.Net.WebException ex)
            {
                Log.Warn("An Exception occurred while trying to check for program updates: " + ex.ToString());
            }
            catch (System.FormatException ex)
            {
                Log.Warn("An Exception occurred while trying to check for program updates: " + ex.ToString());
            }
        }
        #endregion

        protected override void WndProc(ref Message m)
        {
            bool callBase = this.focusHelper.WndProcForFocus(ref m);
            if (callBase)
            {
                base.WndProc(ref m);
            }
        }

        public enum TabTextBehavior
        {
            Static,
            Dynamic,
            Mixed
        }

        /// <summary>Open a window which will allow multiline scripts (commands) to be sent to hosts.</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButtonRunScript_Click(object sender, EventArgs e)
        {            
            dlgScriptEditor editor = new dlgScriptEditor();
            editor.ScriptReady += Editor_ScriptReady;
            editor.SetDesktopLocation(MousePosition.X, MousePosition.Y);                       
            editor.Show();           
        }
        
        /// <summary>Process the script.</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Editor_ScriptReady(object sender, ExecuteScriptEventArgs e)
        {            
            if (!String.IsNullOrEmpty(e.Script))
            {
                string[] scriptlines = e.Script.Split('\n');
                if (scriptlines.Length > 0 
                    && e.IsSPSL)
                {
                    new Thread(delegate()
                    {
                        foreach (string line in scriptlines)
                        {
                            CommandData command;                                                                                    
                            SPSL.TryParseScriptLine(line, out command);
                            if (command != null)
                            {
                                TrySendCommandsFromToolbar(command, false);
                            }
                        }
                    }).Start();
                }
                else // Not a spsl script
                {
                    foreach (string line in scriptlines)
                    {
                        TrySendCommandsFromToolbar(new CommandData(line.TrimEnd('\n'), new KeyEventArgs(Keys.Enter)), !this.tbBtnMaskText.Checked);
                    }
                }
            }
        }
    }
}
