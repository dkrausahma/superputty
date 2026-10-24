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
    /// <summary>File upload (drag & drop, Upload Files) and Transfer Files</summary>
    public partial class frmSuperPutty
    {
        #region Upload

        /// <summary>Uploads need an SSH session and pscp</summary>
        private static bool CanUpload(SessionData session)
        {
            return RemoteCommandTab.IsSupported(session) && SuperPuTTY.IsScpEnabled;
        }

        private void btnUploadFiles_Click(object sender, EventArgs e)
        {
            ctlPuttyPanel panel = this.DockPanel.ActiveDocument as ctlPuttyPanel;
            if (panel != null && this.openFileDialogUpload.ShowDialog(this) == DialogResult.OK)
            {
                UploadFiles(panel, this.openFileDialogUpload.FileNames);
            }
        }

        /// <summary>Files dropped on the icon bar's upload button go to the active terminal's host</summary>
        private void tsIcons_DragOver(object sender, DragEventArgs e)
        {
            ctlPuttyPanel panel = this.DockPanel.ActiveDocument as ctlPuttyPanel;
            bool overButton = this.tsIcons.GetItemAt(this.tsIcons.PointToClient(new Point(e.X, e.Y))) == this.btnUploadFiles;
            e.Effect = overButton && panel != null && CanUpload(panel.Session) && e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void tsIcons_DragDrop(object sender, DragEventArgs e)
        {
            ctlPuttyPanel panel = this.DockPanel.ActiveDocument as ctlPuttyPanel;
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (panel != null && files != null && files.Length > 0)
            {
                this.BeginInvoke((MethodInvoker)delegate { UploadFiles(panel, files); });
            }
        }

        /// <summary>
        /// Upload files and folders to a tab's host with pscp, into the folder confirmed in a dialog (preset with
        /// the shell's current directory if the window title shows it). Runs in the background; progress and
        /// result appear in the status bar.
        /// </summary>
        private void UploadFiles(ctlPuttyPanel panel, string[] paths)
        {
            SessionData session = panel.Session;
            if (!RemoteCommandTab.IsSupported(session))
            {
                SuperPuTTY.ReportStatus("Uploads need an SSH session");
                return;
            }
            if (!SuperPuTTY.IsScpEnabled)
            {
                MessageBox.Show(this, "pscp.exe is not set or not found. Set its location in Tools > Options.", "Upload Files", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<BrowserFileInfo> sources = new List<BrowserFileInfo>();
            foreach (string path in paths)
            {
                if (Directory.Exists(path)) { sources.Add(new BrowserFileInfo(new DirectoryInfo(path))); }
                else if (File.Exists(path)) { sources.Add(new BrowserFileInfo(new FileInfo(path))); }
            }
            if (sources.Count == 0)
            {
                return;
            }

            string detected = RemotePath.FromWindowTitle(panel.AppPanel.GetWindowTitle());
            string targetFolder;
            using (dlgUploadFiles dialog = new dlgUploadFiles(panel.Text, paths, detected ?? "~", detected != null))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                targetFolder = dialog.TargetFolder;
            }

            BrowserFileInfo target = new BrowserFileInfo { Path = RemotePath.ToPscpPath(targetFolder), Source = SourceType.Remote, Type = FileType.Directory };
            string where = string.Format("{0}:{1}", panel.Text, targetFolder);
            SuperPuTTY.ReportStatus("Uploading {0} item(s) to {1}…", sources.Count, where);
            ThreadPool.QueueUserWorkItem(delegate
            {
                FileTransferResult result;
                try
                {
                    PscpClient client = new PscpClient(new PscpOptions { PscpLocation = SuperPuTTY.Settings.PscpExe }, session);
                    result = client.CopyFiles(sources, target, (complete, cancelAll, status) =>
                    {
                        this.BeginInvoke((MethodInvoker)delegate
                        {
                            SuperPuTTY.ReportStatus("Uploading to {0}: {1} {2}%", where, status.Filename, status.PercentComplete);
                        });
                    });
                }
                catch (Exception ex)
                {
                    Log.Error("Upload failed", ex);
                    result = new FileTransferResult();
                    result.SetError(ex.Message, ex);
                }

                this.BeginInvoke((MethodInvoker)delegate
                {
                    if (result.StatusCode == ResultStatusCode.Success)
                    {
                        SuperPuTTY.ReportStatus("Uploaded {0} item(s) to {1}", sources.Count, where);
                    }
                    else
                    {
                        SuperPuTTY.ReportStatus("Upload to {0} failed: {1}", where, result.ErrorMsg);
                        MessageBox.Show(this, string.Format("Upload to {0} failed:\n\n{1}\n\nUploads need key-based login (e.g. Pageant) or a stored password, and write access to the folder.", where, result.ErrorMsg),
                            "Upload Files", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                });
            });
        }

        /// <summary>
        /// Transfer Files dialog: source preset with the active tab and its current directory, target with another
        /// open tab (or the Downloads folder); the copy runs in the background, progress in the status bar
        /// </summary>
        private void transferFiles_Click(object sender, EventArgs e)
        {
            if (!SuperPuTTY.IsScpEnabled)
            {
                MessageBox.Show(this, "pscp.exe is not set or not found. Set its location in Tools > Options.", "Transfer Files", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // sessions of open tabs first (with their current directory), then all other SSH sessions
            List<ctlPuttyPanel> tabs = VisualOrderTabSwitchStrategy.GetDocuments(this.DockPanel).OfType<ctlPuttyPanel>()
                .Where(p => RemoteCommandTab.IsSupported(p.Session)).ToList();
            List<SessionData> sessions = tabs.Select(p => p.Session).Distinct().ToList();
            sessions.AddRange(SuperPuTTY.GetAllSessions()
                .Where(s => RemoteCommandTab.IsSupported(s) && !sessions.Contains(s))
                .OrderBy(s => s.SessionId));

            // preset: the active tab and its neighbour, the left one as source and the right one as target
            ctlPuttyPanel fromTab, toTab;
            FileTransferJob.PreselectPair(tabs, this.DockPanel.ActiveDocument as ctlPuttyPanel, (a, b) => a.Session == b.Session, out fromTab, out toTab);

            TransferEndpoint source = fromTab == null
                ? new TransferEndpoint { Path = string.Empty }
                : new TransferEndpoint { Session = fromTab.Session, Path = (RemotePath.FromWindowTitle(fromTab.AppPanel.GetWindowTitle()) ?? "~") + "/" };
            ctlPuttyPanel other = toTab;
            TransferEndpoint target = other == null
                ? new TransferEndpoint { Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") }
                : new TransferEndpoint { Session = other.Session, Path = RemotePath.FromWindowTitle(other.AppPanel.GetWindowTitle()) ?? "~" };

            // a session's current directory, from its open tab's window title
            Func<SessionData, string> remoteDirectory = session =>
            {
                ctlPuttyPanel tab = tabs.FirstOrDefault(p => p.Session == session);
                return (tab == null ? null : RemotePath.FromWindowTitle(tab.AppPanel.GetWindowTitle())) ?? "~";
            };
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

            using (dlgTransferFiles dialog = new dlgTransferFiles(sessions, source, target, remoteDirectory, downloads))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                source = dialog.Source;
                target = dialog.Target;
            }

            string what = string.Format("{0} → {1}", source, target);
            SuperPuTTY.ReportStatus("Copying {0}…", what);
            ThreadPool.QueueUserWorkItem(delegate
            {
                FileTransferResult result;
                try
                {
                    result = FileTransferJob.Run(source, target, text => this.BeginInvoke((MethodInvoker)delegate { SuperPuTTY.ReportStatus("Copying: {0}", text); }));
                }
                catch (Exception ex)
                {
                    Log.Error("Transfer failed", ex);
                    result = new FileTransferResult();
                    result.SetError(ex.Message, ex);
                }
                this.BeginInvoke((MethodInvoker)delegate
                {
                    if (result.StatusCode == ResultStatusCode.Success)
                    {
                        SuperPuTTY.ReportStatus("Copied {0}", what);
                    }
                    else
                    {
                        SuperPuTTY.ReportStatus("Copy failed: {0}", result.ErrorMsg);
                        MessageBox.Show(this, string.Format("Copying {0} failed:\n\n{1}\n\nTransfers need key-based login (e.g. Pageant) or a stored password, and access to the paths.", what, result.ErrorMsg),
                            "Transfer Files", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                });
            });
        }

        #endregion
    }
}
