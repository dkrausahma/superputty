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
    /// <summary>Kiosk mode (F11): only the terminal tabs, full screen</summary>
    public partial class frmSuperPutty
    {
        private void btnKioskToggle_Click(object sender, EventArgs e)
        {
            ToggleKiosk();
        }

        void ToggleKiosk()
        {
            if (!isKioskMode)
            {
                Log.InfoFormat("Enter Kiosk Mode");
                kioskViewState.SaveState();
                kioskViewState.Hide();
                isKioskMode = true;
                this.tsKiosk.Visible = true;
            }
            else
            {
                Log.InfoFormat("Exit Kiosk Mode");
                kioskViewState.Restore();
                isKioskMode = false;
                this.tsKiosk.Visible = false;
            }
            UpdateIconBar();
        }

        /// <summary>Saves the window layout before kiosk mode hides everything but the terminal tabs, and restores it</summary>
        class ViewState
        {
            public ViewState(frmSuperPutty mainForm)
            {
                this.MainForm = mainForm;
            }

            public frmSuperPutty MainForm { get; set; }
            public bool SessionsWindow { get; set; }
            public bool LogWindow { get; set; }
            public bool LayoutWindow { get; set; }
            public bool SessionDetail { get; set; }
            public bool CommandsWindow { get; set; }
            public bool ContainersWindow { get; set; }
            /// <summary>Not Visible: as a background tab Host Info is invisible but open</summary>
            public bool HostInfoWindow { get; set; }

            public FormBorderStyle FormBorderStyle { get; set; }
            public FormWindowState FormWindowState { get; set; }

            /// <summary>Toolbar positions; showing the toolbars again can otherwise reorder their rows</summary>
            public Point IconBarLocation { get; set; }
            public Point CommandBarLocation { get; set; }
            public Point ConnectionBarLocation { get; set; }

            public void SaveState()
            {
                this.IconBarLocation = this.MainForm.tsIcons.Location;
                this.CommandBarLocation = this.MainForm.tsCommands.Location;
                this.ConnectionBarLocation = this.MainForm.tsConnect.Location;

                this.SessionsWindow = this.MainForm.sessions.IsVisible;
                this.LogWindow = this.MainForm.logViewer.IsVisible;
                this.LayoutWindow = this.MainForm.layouts.IsVisible;
                this.SessionDetail = this.MainForm.sessionDetail.IsVisible;
                this.CommandsWindow = this.MainForm.commandsPane.IsVisible;
                this.ContainersWindow = this.MainForm.containersPane.IsVisible;
                this.HostInfoWindow = this.MainForm.hostInfoPane.Instance != null && !this.MainForm.hostInfoPane.Instance.IsHidden;

                this.FormBorderStyle = this.MainForm.FormBorderStyle;
                this.FormWindowState = this.MainForm.WindowState;

            }

            public void Hide()
            {
                try
                {
                    this.MainForm.DockPanel.Visible = false;

                    // windows
                    this.MainForm.sessions.Hide();
                    this.MainForm.layouts.Hide();
                    this.MainForm.logViewer.Hide();
                    this.MainForm.sessionDetail.Hide();
                    this.MainForm.commandsPane.Hide();
                    this.MainForm.containersPane.Hide();
                    this.MainForm.hostInfoPane.Hide();

                    // status bar
                    this.MainForm.statusStrip1.Hide();

                    // toolbars
                    this.MainForm.tsIcons.Visible = false;
                    this.MainForm.tsCommands.Visible = false;
                    this.MainForm.tsConnect.Visible = false;

                    // menubar
                    this.MainForm.menuStrip1.Hide();

                    this.MainForm.FormBorderStyle = FormBorderStyle.None;
                    if (this.MainForm.WindowState == FormWindowState.Maximized)
                    {
                        // if maximized, goto normal first
                        this.MainForm.WindowState = FormWindowState.Normal;
                    }
                    this.MainForm.WindowState = FormWindowState.Maximized;
                }
                finally
                {
                    this.MainForm.DockPanel.Visible = true;
                }

            }

            public void Restore()
            {
                try
                {
                    this.MainForm.DockPanel.Visible = false;

                    // windows
                    if (this.SessionsWindow) { this.MainForm.sessions.Restore(); }
                    if (this.LayoutWindow) { this.MainForm.layouts.Restore(); }
                    if (this.LogWindow) { this.MainForm.logViewer.Restore(); }
                    if (this.SessionDetail) { this.MainForm.sessionDetail.Restore(); }
                    if (this.CommandsWindow) { this.MainForm.commandsPane.Restore(); }
                    if (this.ContainersWindow) { this.MainForm.containersPane.Restore(); }
                    if (this.HostInfoWindow) { this.MainForm.hostInfoPane.Restore(); }

                    // status bar, toolbars, menu bar and always-on-top as set in the View menu
                    this.MainForm.ApplySettingsToToolbars();
                    this.MainForm.tsIcons.Location = this.IconBarLocation;
                    this.MainForm.tsCommands.Location = this.CommandBarLocation;
                    this.MainForm.tsConnect.Location = this.ConnectionBarLocation;

                    this.MainForm.WindowState = this.FormWindowState;
                    this.MainForm.FormBorderStyle = this.FormBorderStyle;
                }
                finally
                {
                    this.MainForm.DockPanel.Visible = true;

                }
            }
        }
    }
}
