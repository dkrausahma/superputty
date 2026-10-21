/*
 * Copyright (c) 2009 - 2015 Jim Radford http://www.jimradford.com
 * Copyright (c) 2012 John Peterson
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
using System.Linq;
using System.Windows.Forms;
using log4net;
using System.Diagnostics;
using System.Web;
using System.Collections.Specialized;
using SuperPutty.Data;
using WeifenLuo.WinFormsUI.Docking;
using SuperPutty.Utils;
using System.Threading;
using System.Configuration;
using SuperPutty.Gui;
using log4net.Core;


namespace SuperPutty
{
    /// <summary>A control that hosts a putty window</summary>
    public partial class ctlPuttyPanel : ToolWindowDocument
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ctlPuttyPanel));

        private static int RefocusAttempts = Convert.ToInt32(ConfigurationManager.AppSettings["SuperPuTTY.RefocusAttempts"] ?? "5");
        private static int RefocusIntervalMs = Convert.ToInt32(ConfigurationManager.AppSettings["SuperPuTTY.RefocusIntervalMs"] ?? "80");

        private PuttyStartInfo m_puttyStartInfo;
        private ApplicationPanel m_AppPanel;
        private SessionData m_Session;
        private PuttyClosedCallback m_ApplicationExit;

        public ctlPuttyPanel(SessionData session, PuttyClosedCallback callback)
        {
            m_Session = session;
            m_ApplicationExit = callback;
            this.LogFile = SessionLog.NewLogFile(session);
            m_puttyStartInfo = new PuttyStartInfo(session, this.LogFile);

            InitializeComponent();

            this.Tunnels = TunnelMonitor.ReadTunnels(session);
            this.StartedUtc = DateTime.UtcNow;

            this.Text = session.SessionName;
            this.TextOverride = session.SessionName;

            CreatePanel();
            EnableFileDrop();
            AdjustMenu();
        }

        /// <summary>Gets or sets the text displayed on a tab.</summary>
        public override string Text
        {
            get
            {
                return base.Text;
            }
            set
            {
                base.Text = value;
                ApplyTabText();

                if (Log.Logger.IsEnabledFor(Level.Trace))
                {
                    Log.DebugFormat("SetText: text={0}", value);
                }
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            ApplyTabText();
        }

        /// <summary>PuTTY's session log of this tab (when session logging is on), otherwise null</summary>
        public string LogFile { get; private set; }

        /// <summary>Files or folders dropped from Explorer on the terminal area: (tab, paths)</summary>
        public event Action<ctlPuttyPanel, string[]> FilesDropped;

        /// <summary>
        /// Accept files dropped on the panel behind PuTTY. Whether Windows hands a drop over PuTTY's window on to
        /// this panel depends on how PuTTY is attached; dropping on the tab header always works.
        /// </summary>
        private void EnableFileDrop()
        {
            this.m_AppPanel.AllowDrop = true;
            DragEventHandler over = delegate (object sender, DragEventArgs e)
            {
                e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) && this.FilesDropped != null ? DragDropEffects.Copy : DragDropEffects.None;
            };
            this.m_AppPanel.DragEnter += over;
            this.m_AppPanel.DragOver += over;
            this.m_AppPanel.DragDrop += delegate (object sender, DragEventArgs e)
            {
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0 && this.FilesDropped != null)
                {
                    this.BeginInvoke((MethodInvoker)delegate { this.FilesDropped(this, files); });
                }
            };
        }

        /// <summary>The session's SSH tunnels (PuTTY profile and -L/-R/-D arguments), read when the tab opens</summary>
        public List<SshTunnel> Tunnels { get; private set; }

        /// <summary>When the tab was opened; tunnels get a grace period to come up after login</summary>
        public DateTime StartedUtc { get; private set; }

        /// <summary>Appended to the tab text only (e.g. " ↔2"), so <see cref="Text"/> stays the plain title</summary>
        private string tabTextSuffix = string.Empty;

        /// <summary>Tunnel details appended to the tab tooltip</summary>
        private string tunnelToolTip = string.Empty;

        /// <summary>True while a background tab has a title change the user hasn't looked at yet</summary>
        public bool HasActivity { get; private set; }

        /// <summary>Mark the tab ("● " before its title) or clear the mark</summary>
        public void SetActivity(bool activity)
        {
            if (activity != this.HasActivity)
            {
                this.HasActivity = activity;
                ApplyTabText();
            }
        }

        /// <summary>Show the tunnel marker and details on the tab</summary>
        public void SetTunnelDisplay(string suffix, string toolTip)
        {
            if (suffix != this.tabTextSuffix || toolTip != this.tunnelToolTip)
            {
                this.tabTextSuffix = suffix ?? string.Empty;
                this.tunnelToolTip = toolTip ?? string.Empty;
                ApplyTabText();
            }
        }

        /// <summary>The session's environment (prod/qual/dev), shown as a marker on the tab</summary>
        public EnvironmentKind Environment
        {
            get { return SessionEnvironment.Of(this.m_Session); }
        }

        private void ApplyTabText()
        {
            string text = base.Text ?? string.Empty;
            // room for the prod badge painted at the tab's right end (EnvironmentTabStripFactory)
            string badgeSpace = this.m_Session != null && this.Environment == EnvironmentKind.Prod ? "     " : string.Empty;
            this.TabText = (this.HasActivity ? "● " : string.Empty) + text.Replace("&", "&&") + this.tabTextSuffix + badgeSpace;
            this.ToolTipText = this.tunnelToolTip.Length == 0 ? text : text + "\n\n" + this.tunnelToolTip;
        }

        private void CreatePanel()
        {
            this.m_AppPanel = new ApplicationPanel();
            this.SuspendLayout();            
            this.m_AppPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.m_AppPanel.ApplicationName = this.m_puttyStartInfo.Executable;
            this.m_AppPanel.ApplicationParameters = this.m_puttyStartInfo.Args;
            this.m_AppPanel.ApplicationWorkingDirectory = this.m_puttyStartInfo.WorkingDir;
            this.m_AppPanel.Location = new System.Drawing.Point(0, 0);
            this.m_AppPanel.Name = this.m_Session.SessionId; // "applicationControl1";
            this.m_AppPanel.Size = new System.Drawing.Size(this.Width, this.Height);
            this.m_AppPanel.TabIndex = 0;            
            this.m_AppPanel.m_CloseCallback = this.m_ApplicationExit;
            this.Controls.Add(this.m_AppPanel);

            this.ResumeLayout();
        }

        void AdjustMenu()
        {
            // for mintty, disable the putty menu items
            if (this.Session.Proto == ConnectionProtocol.Mintty)
            {
                this.toolStripPuttySep1.Visible = false;
                this.eventLogToolStripMenuItem.Visible = false;
                this.toolStripPuttySep2.Visible = false;
                this.changeSettingsToolStripMenuItem.Visible = false;
                this.copyAllToClipboardToolStripMenuItem.Visible = false;
                this.restartSessionToolStripMenuItem.Visible = false;
                this.clearScrollbackToolStripMenuItem.Visible = false;
                this.resetTerminalToolStripMenuItem.Visible = false;
            }
        }

        void CreateMenu()
        {
            this.newSessionToolStripMenuItem.Enabled = SuperPuTTY.Settings.PuttyPanelShowNewSessionMenu;
            if (SuperPuTTY.Settings.PuttyPanelShowNewSessionMenu)
            {
                this.contextMenuStrip1.SuspendLayout();

                // BBB: do i need to dispose each one?
                newSessionToolStripMenuItem.DropDownItems.Clear();
                foreach (SessionData session in SuperPuTTY.GetAllSessions())
                {
                    ToolStripMenuItem tsmiParent = newSessionToolStripMenuItem;
                    foreach (string part in SessionData.GetSessionNameParts(session.SessionId))
                    {
                        if (part == session.SessionName)
                        {
                            ToolStripMenuItem newSessionTSMI = new ToolStripMenuItem
                            {
                                Tag = session,
                                Text = session.SessionName
                            };
                            newSessionTSMI.Click += new System.EventHandler(newSessionTSMI_Click);
                            newSessionTSMI.ToolTipText = session.ToString();
                            tsmiParent.DropDownItems.Add(newSessionTSMI);
                        }
                        else
                        {
                            if (tsmiParent.DropDownItems.ContainsKey(part))
                            {
                                tsmiParent = (ToolStripMenuItem)tsmiParent.DropDownItems[part];
                            }
                            else
                            {
                                ToolStripMenuItem newSessionFolder = new ToolStripMenuItem(part) {Name = part};
                                tsmiParent.DropDownItems.Add(newSessionFolder);
                                tsmiParent = newSessionFolder;
                            }
                        }
                    }
                }
                this.contextMenuStrip1.ResumeLayout();
            }

            DockPane pane = GetDockPane();
            if (pane != null)
            {
                this.closeOthersToTheRightToolStripMenuItem.Enabled =
                    pane.Contents.IndexOf(this) != pane.Contents.Count - 1;
            }
            this.closeOthersToolStripMenuItem.Enabled = this.DockPanel.DocumentsCount > 1;
            this.closeAllToolStripMenuItem.Enabled = this.DockPanel.DocumentsCount > 1;
        }

        private void closeSessionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void closeOthersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var docs = from doc in this.DockPanel.DocumentsToArray()
                       where doc is ToolWindowDocument && doc != this
                       select doc as ToolWindowDocument;
            CloseDocs("Close Others", docs);
        }

        private void closeAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var docs = from doc in this.DockPanel.DocumentsToArray()
                       where doc is ToolWindowDocument
                       select doc as ToolWindowDocument;
            CloseDocs("Close All", docs);
        }

        private void closeOthersToTheRightToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // find the dock pane with this window
            DockPane pane = GetDockPane();
            if (pane != null)
            {
                // found the pane
                List<ToolWindowDocument> docsToClose = new List<ToolWindowDocument>();
                bool close = false;
                foreach (IDockContent content in new List<IDockContent>(pane.Contents))
                {
                    if (content == this)
                    {
                        close = true;
                        continue;
                    }
                    if (close)
                    {
                        ToolWindowDocument win = content as ToolWindowDocument;
                        if (win != null)
                        {
                            docsToClose.Add(win);
                        }
                    }
                }
                if (docsToClose.Count > 0)
                {
                    CloseDocs("Close Other To the Right", docsToClose);
                }
            }
        }

        void CloseDocs(string source, IEnumerable<ToolWindowDocument> docsToClose)
        {
            int n = docsToClose.Count();
            Log.InfoFormat("Closing mulitple docs: source={0}, count={1}, conf={2}", source, n, SuperPuTTY.Settings.MultipleTabCloseConfirmation);

            bool okToClose = true;
            if (SuperPuTTY.Settings.MultipleTabCloseConfirmation && n > 1)
            {
                okToClose = DialogResult.Yes == MessageBox.Show(this, string.Format("Close {0} Tabs?", n), source, MessageBoxButtons.YesNo);
            }

            if (okToClose)
            {
                foreach (ToolWindowDocument doc in docsToClose)
                {
                    doc.Close();
                }
            }
        }

        DockPane GetDockPane()
        {
            return this.DockPanel.Panes.FirstOrDefault(pane => pane.Contents.Contains(this));
        }

        /// <summary>
        /// Reset the focus to the child application window
        /// </summary>
        internal void SetFocusToChildApplication(string caller)
        {
            if (!this.m_AppPanel.ExternalProcessCaptured) { return; }

            bool success = false;
            for (int i = 0; i < RefocusAttempts; i++)
            {
                Thread.Sleep(RefocusIntervalMs);
                if (this.m_AppPanel.ReFocusPuTTY(caller))
                {
                    if (i > 0)
                    {
                        Log.DebugFormat("SetFocusToChildApplication success after {0} attempts", i + 1);
                    }
                    success = true;
                    break;
                }
            }

            if (!success)
            {
                Log.WarnFormat("Unable to SetFocusToChildApplication, {0}", this.Text);
            }
        }

        protected override string GetPersistString()
        {
            string str = String.Format("{0}?SessionId={1}&TabName={2}", 
                this.GetType().FullName, 
                HttpUtility.UrlEncode(this.m_Session.SessionId),
                HttpUtility.UrlEncode(this.TextOverride));
            return str;
        }

        /// <summary>Restore sessions from a string containing previous sessions</summary>
        /// <param name="persistString">A string containing the sessions to restore</param>
        /// <returns>The <seealso cref="ctlPuttyPanel"/> object which is the parent of the hosted putty application, null if unable to start session</returns>
        public static ctlPuttyPanel FromPersistString(String persistString)
        {
            ctlPuttyPanel panel = null;
            if (persistString.StartsWith(typeof(ctlPuttyPanel).FullName))
            {
                int idx = persistString.IndexOf("?");
                if (idx != -1)
                {
                    NameValueCollection data = HttpUtility.ParseQueryString(persistString.Substring(idx + 1));
                    string sessionId = data["SessionId"] ?? data["SessionName"];
                    string tabName = data["TabName"];

                    Log.InfoFormat("Restoring putty session, sessionId={0}, tabName={1}", sessionId, tabName);

                    SessionData session = SuperPuTTY.GetSessionById(sessionId);
                    if (session != null)
                    {
                        panel = SuperPuTTY.OpenPuttySession(session);
                        if (panel == null)
                        {
                            Log.WarnFormat("Could not restore putty session, sessionId={0}", sessionId);
                        }
                        else
                        {
                            panel.Icon = SuperPuTTY.GetIconForSession(session);
                            panel.Text = tabName;
                            panel.TextOverride = tabName;
                        }
                    }
                    else
                    {
                        Log.WarnFormat("Session not found, sessionId={0}", sessionId);
                    }
                }
                else
                {
                    idx = persistString.IndexOf(":");
                    if (idx != -1)
                    {
                        string sessionId = persistString.Substring(idx + 1);
                        Log.InfoFormat("Restoring putty session, sessionId={0}", sessionId);
                        SessionData session = SuperPuTTY.GetSessionById(sessionId);
                        if (session != null)
                        {
                            panel = SuperPuTTY.OpenPuttySession(session);
                        }
                        else
                        {
                            Log.WarnFormat("Session not found, sessionId={0}", sessionId);
                        }
                    }
                }
            }
            return panel;
        }

        private void aboutPuttyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo("http://www.chiark.greenend.org.uk/~sgtatham/putty/") { UseShellExecute = true });
        }

 
        private void duplicateSessionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SuperPuTTY.OpenPuttySession(this.m_Session);
        }

        private void renameTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            dlgRenameItem dialog = new dlgRenameItem
            {
                ItemName = this.Text,
                DetailName = this.m_Session.SessionId
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                this.Text = dialog.ItemName;
                this.TextOverride = dialog.ItemName;
            }
        }

        private void refreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.m_AppPanel != null)
            {
                this.m_AppPanel.RefreshAppWindow();
            }
        }

        public SessionData Session { get { return this.m_Session; } }
        public ApplicationPanel AppPanel { get { return this.m_AppPanel; } }
        public ctlPuttyPanel previousPanel { get; set; }
        public ctlPuttyPanel nextPanel { get; set; }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            CreateMenu();
        }

        private void newSessionTSMI_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = (ToolStripMenuItem) sender;
            SessionData session = menuItem.Tag as SessionData;
            if (session != null)
            {
                SuperPuTTY.OpenPuttySession(session);
            }
        }

        private void puTTYMenuTSMI_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = (ToolStripMenuItem) sender;
            string[] tags = ((ToolStripMenuItem)sender).Tag.ToString().Split(';');
            uint[] commands = new uint[tags.Length];
            for (int i = 0; i < tags.Length; ++i)
            {
                commands[i] = Convert.ToUInt32(tags[i], 16);
                Log.DebugFormat("Sending Putty Command: menu={2}, tag={0}, command={1}", tags[i], commands[i], menuItem.Text);
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    this.SetFocusToChildApplication("MenuHandler");
                    for (int i = 0; i < commands.Length; ++i)
                    {
                        NativeMethods.SendMessage(m_AppPanel.AppWindowHandle, (uint)NativeMethods.WM.SYSCOMMAND, commands[i], 0L);
                    }
                }
                catch (Exception ex)
                {
                    Log.ErrorFormat("Error sending command menu command to embedded putty", ex);
                }
            });
        }

        /// <summary>PuTTY's "Copy All to Clipboard" system menu command (IDM_COPYALL)</summary>
        private const uint PuttyCopyAllCommand = 0x0170;

        /// <summary>True if the embedded program is PuTTY/KiTTY, which understands PuTTY's system menu commands</summary>
        public bool SupportsPuttyCommands
        {
            get { return m_Session.Proto != ConnectionProtocol.Mintty && m_Session.Proto != ConnectionProtocol.VNC; }
        }

        /// <summary>
        /// Let PuTTY copy the terminal screen and scrollback to the clipboard as plain text. Blocks until PuTTY has
        /// done it, so call it off the UI thread. Returns false if the session can't do this.
        /// </summary>
        public bool CopyAllToClipboard()
        {
            if (!SupportsPuttyCommands || !m_AppPanel.ExternalProcessCaptured)
            {
                return false;
            }
            NativeMethods.SendMessage(m_AppPanel.AppWindowHandle, (uint)NativeMethods.WM.SYSCOMMAND, PuttyCopyAllCommand, 0L);
            return true;
        }

        public bool AcceptCommands
        {
            get { return this.acceptCommandsToolStripMenuItem.Checked;  }
            set { this.acceptCommandsToolStripMenuItem.Checked = value; }
        }

        public string TextOverride { get; set; }

    }
}
