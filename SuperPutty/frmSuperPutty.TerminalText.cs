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
    /// <summary>Copy Session, Copy Last Command, Explain in Claude</summary>
    public partial class frmSuperPutty
    {
        private void copySession_Click(object sender, EventArgs e)
        {
            CopyActiveSessionToClipboard(false);
        }

        private void copyLastCommand_Click(object sender, EventArgs e)
        {
            CopyActiveSessionToClipboard(true);
        }

        /// <summary>
        /// Explain in Claude: takes the last command and its output of the active tab (the whole screen if no prompt
        /// is recognised) and opens the preview dialog; nothing is sent before the user presses Send there. PuTTY
        /// can only hand out its text through the clipboard, so the previous clipboard text is put back afterwards.
        /// </summary>
        private void explainInClaude_Click(object sender, EventArgs e)
        {
            ctlPuttyPanel panel = this.DockPanel.ActiveDocument as ctlPuttyPanel;
            if (panel == null || !panel.SupportsPuttyCommands)
            {
                SuperPuTTY.ReportStatus("Explain in Claude: no PuTTY terminal tab is active");
                return;
            }
            string previousClipboard = null;
            try
            {
                previousClipboard = Clipboard.ContainsText() ? Clipboard.GetText() : null;
            }
            catch (System.Runtime.InteropServices.ExternalException ex)
            {
                Log.Warn("Could not read the clipboard", ex);
            }

            string tabName = panel.Text;
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool copied = false;
                try
                {
                    copied = panel.CopyAllToClipboard();
                }
                catch (Exception ex)
                {
                    Log.Error("Error copying session text", ex);
                }

                this.BeginInvoke((MethodInvoker)delegate
                {
                    string text = null;
                    try
                    {
                        if (copied && Clipboard.ContainsText())
                        {
                            text = Clipboard.GetText();
                            text = TerminalText.LastCommand(text) ?? text;
                        }
                        if (previousClipboard != null)
                        {
                            Clipboard.SetText(previousClipboard);
                        }
                    }
                    catch (System.Runtime.InteropServices.ExternalException ex)
                    {
                        Log.Warn("Clipboard access failed", ex);
                    }
                    if (text == null)
                    {
                        SuperPuTTY.ReportStatus("Explain in Claude: could not read the text of {0}", tabName);
                        return;
                    }
                    using (dlgExplainInClaude dialog = new dlgExplainInClaude(tabName, text))
                    {
                        dialog.ShowDialog(this);
                    }
                });
            });
        }

        /// <summary>
        /// Copy the active terminal's screen and scrollback to the clipboard (PuTTY's "Copy All to Clipboard"),
        /// e.g. to paste a session into a chat, and report the number of lines in the status bar. With
        /// <paramref name="lastCommandOnly"/> only the last command and its output are kept (cut at the prompts).
        /// </summary>
        private void CopyActiveSessionToClipboard(bool lastCommandOnly)
        {
            ctlPuttyPanel panel = this.DockPanel.ActiveDocument as ctlPuttyPanel;
            if (panel == null || !panel.SupportsPuttyCommands)
            {
                SuperPuTTY.ReportStatus("Copy to clipboard: no PuTTY terminal tab is active");
                return;
            }

            string tabName = panel.Text;
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool copied = false;
                try
                {
                    copied = panel.CopyAllToClipboard();
                }
                catch (Exception ex)
                {
                    Log.Error("Error copying session to clipboard", ex);
                }

                this.BeginInvoke((MethodInvoker)delegate
                {
                    if (!copied)
                    {
                        SuperPuTTY.ReportStatus("Copy to clipboard failed for {0}", tabName);
                        return;
                    }
                    int lines = 0;
                    bool cut = false;
                    try
                    {
                        if (Clipboard.ContainsText())
                        {
                            string text = Clipboard.GetText();
                            if (lastCommandOnly)
                            {
                                string last = TerminalText.LastCommand(text);
                                if (last != null)
                                {
                                    Clipboard.SetText(last);
                                    text = last;
                                    cut = true;
                                }
                            }
                            lines = text.Split('\n').Length;
                        }
                    }
                    catch (System.Runtime.InteropServices.ExternalException ex)
                    {
                        Log.Warn("Could not read clipboard to count lines", ex);
                    }
                    if (lastCommandOnly && !cut)
                    {
                        SuperPuTTY.ReportStatus("No shell prompt recognised in {0}: copied the whole session ({1} lines)", tabName, lines);
                    }
                    else
                    {
                        SuperPuTTY.ReportStatus("Copied {0} {1} lines from {2} to the clipboard", lines, lastCommandOnly ? "last-command" : "session", tabName);
                    }
                });
            });
        }
    }
}
