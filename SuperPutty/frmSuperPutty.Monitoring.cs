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
    /// <summary>SSH tunnel markers and overview, health checks of the open tabs</summary>
    public partial class frmSuperPutty
    {
        /// <summary>The state of one tunnel of an open tab (tab markers and the tunnel overview)</summary>
        private class TunnelEntry
        {
            public ctlPuttyPanel Panel { get; set; }
            public SshTunnel Tunnel { get; set; }
            public TunnelState State { get; set; }
            /// <summary>Who holds the port for a conflict, otherwise null</summary>
            public string Holder { get; set; }
        }

        /// <summary>
        /// The state of every tunnel of the open tabs: is its local port held by the tab's PuTTY (or by another tab
        /// of the same host)? Null if the listener table can't be read.
        /// </summary>
        private List<TunnelEntry> CollectTunnelStates()
        {
            List<ctlPuttyPanel> panels = this.DockPanel.Contents.OfType<ctlPuttyPanel>().ToList();
            List<TunnelEntry> entries = new List<TunnelEntry>();
            if (!panels.Any(p => p.Tunnels != null && p.Tunnels.Count > 0))
            {
                return entries;
            }

            Dictionary<int, HashSet<int>> listeners;
            try
            {
                listeners = TcpListeners.Snapshot();
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                Log.Warn("Could not read the TCP listener table, tunnel markers disabled", ex);
                this.tunnelTimer.Stop();
                return null;
            }

            // PuTTY process -> tab, to name the tab that holds a port
            Dictionary<int, ctlPuttyPanel> tabsByProcess = new Dictionary<int, ctlPuttyPanel>();
            foreach (ctlPuttyPanel panel in panels)
            {
                int id = panel.AppPanel.ProcessId;
                if (id != 0)
                {
                    tabsByProcess[id] = panel;
                }
            }

            foreach (ctlPuttyPanel panel in panels.Where(p => p.Tunnels != null && p.Tunnels.Count > 0))
            {
                int processId = panel.AppPanel.ProcessId;
                List<int> sameHost = tabsByProcess
                    .Where(t => t.Key != processId && TunnelMonitor.SameTarget(t.Value.Session, panel.Session))
                    .Select(t => t.Key).ToList();
                bool inStartup = DateTime.UtcNow - panel.StartedUtc < TunnelMonitor.StartupGrace;
                foreach (SshTunnel tunnel in panel.Tunnels)
                {
                    TunnelState state = TunnelMonitor.StateOf(tunnel, processId, listeners, sameHost, inStartup);
                    entries.Add(new TunnelEntry
                    {
                        Panel = panel,
                        Tunnel = tunnel,
                        State = state,
                        Holder = state == TunnelState.Conflict ? DescribeProcesses(listeners[tunnel.ListenPort], tabsByProcess) : null
                    });
                }
            }
            return entries;
        }

        /// <summary>
        /// Show on each tab with SSH tunnels how many it has and whether their local ports are held by its PuTTY
        /// (or by another SuperPuTTY tab of the same host); details in the tab tooltip
        /// </summary>
        private void UpdateTunnelDisplays()
        {
            List<TunnelEntry> entries = CollectTunnelStates();
            if (entries == null)
            {
                return;
            }
            foreach (IGrouping<ctlPuttyPanel, TunnelEntry> tab in entries.GroupBy(e => e.Panel))
            {
                List<TunnelState> states = tab.Select(e => e.State).ToList();
                Dictionary<int, string> holders = tab.Where(e => e.Holder != null)
                    .GroupBy(e => e.Tunnel.ListenPort).ToDictionary(g => g.Key, g => g.First().Holder);
                tab.Key.SetTunnelDisplay(TunnelMonitor.TabSuffix(states), TunnelMonitor.ToolTip(tab.Select(e => e.Tunnel).ToList(), states, holders));
            }
        }

        /// <summary>Tools → SSH Tunnels: all tunnels of the open tabs; double-click switches to the tab</summary>
        private void tunnelsOverview_Click(object sender, EventArgs e)
        {
            List<TunnelEntry> entries = CollectTunnelStates() ?? new List<TunnelEntry>();
            List<string[]> rows = entries.Select(entry => new[]
            {
                entry.Panel.Text,
                entry.Tunnel.Describe(),
                entry.State == TunnelState.Up ? "up"
                    : entry.State == TunnelState.Down ? "NOT listening"
                    : entry.State == TunnelState.Conflict ? "port in use by " + entry.Holder
                    : entry.State == TunnelState.NotCheckable ? "on the server (not checked)" : "connecting…",
                entry.Tunnel.Origin
            }).ToList();

            using (dlgTunnels dialog = new dlgTunnels(rows))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedIndex >= 0)
                {
                    entries[dialog.SelectedIndex].Panel.Activate();
                }
            }
        }

        /// <summary>Check the hosts of the open terminal tabs (command tabs count as their original session)</summary>
        private void CheckOpenTabsHealth()
        {
            if (!SuperPuTTY.Settings.AutoHealthCheck)
            {
                return;
            }
            HealthMonitor.Check(this.DockPanel.Contents.OfType<ctlPuttyPanel>()
                .Select(p => SuperPuTTY.GetSessionById(p.Session.ScopeSessionId))
                .Where(s => s != null).Distinct().ToList(), false);
        }

        private void healthChecksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.healthChecksToolStripMenuItem.Checked = !this.healthChecksToolStripMenuItem.Checked;
            SuperPuTTY.Settings.AutoHealthCheck = this.healthChecksToolStripMenuItem.Checked;
            SuperPuTTY.Settings.Save();
            CheckOpenTabsHealth();
        }

        /// <summary>The holders of a port, for tunnel conflicts: "tab 'name'" for SuperPuTTY tabs, else "name (PID)"</summary>
        private static string DescribeProcesses(IEnumerable<int> processIds, Dictionary<int, ctlPuttyPanel> tabsByProcess)
        {
            return string.Join(", ", processIds.Select(id =>
            {
                ctlPuttyPanel tab;
                if (tabsByProcess.TryGetValue(id, out tab))
                {
                    return string.Format("tab '{0}'", tab.Text);
                }
                try
                {
                    using (Process process = Process.GetProcessById(id))
                    {
                        return string.Format("{0} (PID {1})", process.ProcessName, id);
                    }
                }
                catch (ArgumentException)
                {
                    return "PID " + id;
                }
            }));
        }
    }
}
