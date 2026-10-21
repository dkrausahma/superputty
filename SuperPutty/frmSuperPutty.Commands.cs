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
    /// <summary>Sending commands to terminals: active session, Run in All/Folder/New Tab, command bar, live input</summary>
    public partial class frmSuperPutty
    {
        /// <summary>The session of the active terminal tab, or null</summary>
        private SessionData ActiveSession()
        {
            ctlPuttyPanel panel = this.DockPanel.ActiveDocument as ctlPuttyPanel;
            return panel == null ? null : panel.Session;
        }

        /// <summary>
        /// Type a command into the active terminal tab, optionally followed by Enter, and put the focus there.
        /// Run commands are recorded in that session's command bar history. Returns false without an active terminal.
        /// </summary>
        private bool SendToActiveSession(string command, bool execute)
        {
            ctlPuttyPanel panel = this.DockPanel.ActiveDocument as ctlPuttyPanel;
            if (panel == null || !panel.AppPanel.ExternalProcessCaptured)
            {
                return false;
            }

            // line breaks of multi-line commands are typed as Enter
            string text = CommandSnippet.ForTerminal(command);
            CommandData data = execute ? new CommandData(text, new KeyEventArgs(Keys.Enter)) : new CommandData(text);
            data.SendToTerminal(panel.AppPanel.AppWindowHandle.ToInt32());
            if (execute)
            {
                this.commandHistory.Add(CommandHistory.KeyFor(panel.Session), command.Trim());
                RefreshCommandHistory();
            }
            panel.SetFocusToChildApplication("CommandsPane");
            return true;
        }

        /// <summary>
        /// Run a command in all terminal tabs that accept commands, after a confirmation, and record it in each
        /// session's history. Returns the number of terminals it was sent to.
        /// </summary>
        private int RunInAllSessions(string command)
        {
            return RunInSessions(command, GetCommandTargets(), "Run in All Terminals");
        }

        /// <summary>Run a command in the open tabs (that accept commands) of sessions below a folder of the session tree</summary>
        private int RunInFolder(string command, string folder)
        {
            List<ctlPuttyPanel> targets = GetCommandTargets()
                .Where(p => CommandLibrary.FolderChain(p.Session.ScopeSessionId).Contains(folder)).ToList();
            return RunInSessions(command, targets, "Run in Terminals of " + folder);
        }

        /// <summary>Run a command in the given tabs after a confirmation, recording it in each session's history</summary>
        private int RunInSessions(string command, List<ctlPuttyPanel> targets, string title)
        {
            if (targets.Count == 0)
            {
                SuperPuTTY.ReportStatus("{0}: no open terminal tab accepts commands", title);
                return 0;
            }
            // production tabs among the targets get an explicit warning
            List<ctlPuttyPanel> prod = targets.Where(t => t.Environment == EnvironmentKind.Prod).ToList();
            string question = string.Format("Run this command in {0} terminal(s)?\n\n{1}\n\n{2}", targets.Count, command.Trim(),
                string.Join(", ", targets.Select(t => t.Text)));
            if (prod.Count > 0)
            {
                question = string.Format("WARNING: {0} of the {1} terminals are PRODUCTION systems:\n{2}\n\n", prod.Count, targets.Count,
                    string.Join(", ", prod.Select(t => t.Text))) + question;
            }
            if (MessageBox.Show(this, question, title, MessageBoxButtons.OKCancel,
                prod.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.OK)
            {
                return 0;
            }

            foreach (ctlPuttyPanel panel in targets)
            {
                new CommandData(CommandSnippet.ForTerminal(command), new KeyEventArgs(Keys.Enter)).SendToTerminal(panel.AppPanel.AppWindowHandle.ToInt32());
                this.commandHistory.Add(CommandHistory.KeyFor(panel.Session), command.Trim());
            }
            RefreshCommandHistory();
            SuperPuTTY.ReportStatus("Ran \"{0}\" in {1} terminal(s)", command.Trim(), targets.Count);
            return targets.Count;
        }

        /// <summary>
        /// Run a command in a new tab on the active terminal's host; the tab keeps a shell open after the command.
        /// Returns false without an active SSH terminal.
        /// </summary>
        private bool RunInNewTab(string command)
        {
            ctlPuttyPanel panel = this.DockPanel.ActiveDocument as ctlPuttyPanel;
            if (panel == null || !RemoteCommandTab.IsSupported(panel.Session))
            {
                SuperPuTTY.ReportStatus("Run in new tab: no SSH terminal tab is active");
                return false;
            }
            string title = command.Trim();
            if (title.Length > 30)
            {
                title = title.Substring(0, 29) + "…";
            }
            return RemoteCommandTab.Open(panel.Session, RemoteCommandTab.KeepShellAfter(command), title) != null;
        }

        #region Live input

        /// <summary>Live input on: keys typed in a target tab are typed in all target tabs (read by the keyboard hook)</summary>
        private bool liveInput;

        private void tbBtnLiveInput_CheckedChanged(object sender, EventArgs e)
        {
            if (!this.tbBtnLiveInput.Checked)
            {
                this.liveInput = false;
                this.tbBtnLiveInput.BackColor = System.Drawing.Color.Empty;
                SuperPuTTY.ReportStatus("Live input off");
                return;
            }
            List<ctlPuttyPanel> targets = GetCommandTargets();
            if (targets.Count < 2)
            {
                this.tbBtnLiveInput.Checked = false;
                MessageBox.Show(this, "Live input needs at least two open terminal tabs that accept commands " +
                    "(tab menu → Accept Commands, or the selector next to this button).", "Live Input", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            List<ctlPuttyPanel> prod = targets.Where(t => t.Environment == EnvironmentKind.Prod).ToList();
            if (prod.Count > 0 && MessageBox.Show(this,
                string.Format("WARNING: {0} of the {1} terminals are PRODUCTION systems:\n{2}\n\nEvery key you type goes to all of them. Turn on live input?",
                    prod.Count, targets.Count, string.Join(", ", prod.Select(t => t.Text))),
                "Live Input", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.OK)
            {
                this.tbBtnLiveInput.Checked = false;
                return;
            }
            this.liveInput = true;
            this.tbBtnLiveInput.BackColor = System.Drawing.Color.FromArgb(255, 200, 120);
            SuperPuTTY.ReportStatus("Live input on: keys typed in {0} go to all of them", string.Join(", ", targets.Select(t => t.Text)));
        }

        /// <summary>Repeats a key typed into a target tab's PuTTY in the other target tabs (called from the keyboard hook)</summary>
        private void MirrorKey(Keys key, IntPtr hookData)
        {
            try
            {
                ctlPuttyPanel active = this.DockPanel.ActiveDocument as ctlPuttyPanel;
                if (active == null || !LiveInput.HasFocus(active.AppPanel.AppWindowHandle))
                {
                    return;
                }
                List<ctlPuttyPanel> targets = GetCommandTargets();
                if (!targets.Contains(active))
                {
                    return;
                }
                uint scanCode = (uint)Marshal.ReadInt32(hookData, 4);
                bool extended = (Marshal.ReadInt32(hookData, 8) & 0x01) != 0;
                IntPtr layoutWindow = active.AppPanel.AppWindowHandle;
                Keystroke? keystroke = LiveInput.Translate(key, scanCode, extended, this.isShiftDown, this.isControlDown, this.isAltDown,
                    (k, scan, shift, altGr) => LiveInput.ToChars(k, scan, shift, altGr, layoutWindow));
                if (keystroke == null)
                {
                    return;
                }
                foreach (ctlPuttyPanel target in targets.Where(t => t != active))
                {
                    LiveInput.Send(target.AppPanel.AppWindowHandle, keystroke.Value);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Live input failed", ex);
            }
        }

        #endregion

        /// <summary>Send command from send command toolbar to open sessions</summary>
        /// <param name="saveHistory">If true, save the history in the command toolbar combobox</param>
        /// <returns>The number of commands sent</returns>
        private int TrySendCommandsFromToolbar(bool saveHistory)
        {
            return TrySendCommandsFromToolbar(new CommandData(this.tsSendCommandCombo.Text), saveHistory);
        }

        /// <summary>
        /// The terminal tabs that receive commands sent to several sessions (command bar, "Run in All Terminals"),
        /// in tab order: tabs with "Accept Commands" checked, or only visible tabs if so chosen in the document selector
        /// </summary>
        private List<ctlPuttyPanel> GetCommandTargets()
        {
            return VisualOrderTabSwitchStrategy.GetDocuments(this.DockPanel)
                .OfType<ctlPuttyPanel>()
                .Where(panel => this.sendCommandsDocumentSelector.IsDocumentSelected(panel) && panel.AppPanel.ExternalProcessCaptured)
                .ToList();
        }

        /// <summary>Send commands to open sessions</summary>
        /// <param name="command">The <seealso cref="CommandData"/> object containing text and or keyboard commands</param>
        /// <param name="saveHistory">If True, save the history in the command toolbar combobox</param>
        /// <returns>The number terminals commands have been sent to</returns>
        private int TrySendCommandsFromToolbar(CommandData command, bool saveHistory)
        {
            int sent = 0;
            List<string> sessionKeys = new List<string>();

            if(this.DockPanel.Contents.Count > 0)
            {
                foreach (ctlPuttyPanel panel in GetCommandTargets())
                {
                    command.SendToTerminal(panel.AppPanel.AppWindowHandle.ToInt32());

                    sent++;
                    sessionKeys.Add(CommandHistory.KeyFor(panel.Session));
                }

                if (sent > 0)
                {
                    // success...clear text and save in the history of every session that got the command
                    bool addToHistory = command != null && !string.IsNullOrEmpty(command.Command) && saveHistory;
                    MethodInvoker update = delegate {
                        if (addToHistory)
                        {
                            foreach (string sessionKey in sessionKeys)
                            {
                                this.commandHistory.Add(sessionKey, command.Command);
                            }
                        }
                        this.tsSendCommandCombo.Text = string.Empty;
                        RefreshCommandHistory();
                    };

                    if (this.InvokeRequired)
                    {
                        this.BeginInvoke(update);
                    }
                    else
                    {
                        update();
                    }
                }
            }
            return sent;
        }
    }
}
