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
    /// <summary>Low-level keyboard and mouse hooks (tab switching, shortcuts, live input), window enumeration</summary>
    public partial class frmSuperPutty
    {
        #region Mouse and Keyboard Hooks

        private static IntPtr SetKBHook(NativeMethods.LowLevelKMProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, proc, NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        // Intercept keyboard messages for Ctrl-F4 and Ctrl-Tab handling
        private IntPtr KBHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys keys = (Keys)vkCode;

                // track key state globally for control/alt/shift is up/down
                bool isKeyDown = wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN;
                if (keys == Keys.LControlKey || keys == Keys.RControlKey) { isControlDown = isKeyDown; }
                if (keys == Keys.LShiftKey || keys == Keys.RShiftKey) { isShiftDown = isKeyDown; }
                if (keys == Keys.LMenu || keys == Keys.RMenu) { isAltDown = isKeyDown; }

                if (Log.Logger.IsEnabledFor(Level.Trace))
                {
                    Log.DebugFormat("### KBHook: nCode={0}, wParam={1}, lParam={2} ({4,-4} - {3}) [{5}{6}{7}]",
                        nCode, wParam, vkCode, keys, isKeyDown ? "Down" : "Up",
                        isControlDown ? "Ctrl" : "", isAltDown ? "Alt" : "", isAltDown ? "Shift" : "");
                }

                if (IsForegroundWindow(this))
                {
                    // SuperPutty or Putty is the window in front...

                    if (keys == Keys.LControlKey || keys == Keys.RControlKey)
                    {
                        // If Ctrl-Tab has been pressed to move to an older panel then
                        // make it current panel when Ctrl key is finally released.
                        if (SuperPuTTY.Settings.EnableControlTabSwitching && !isControlDown && !isShiftDown)
                        {
                            this.tabSwitcher.CurrentDocument = (ToolWindow)this.DockPanel.ActiveDocument;
                        }
                    }

                    if (keys == Keys.LShiftKey || keys == Keys.RShiftKey)
                    {
                        // If Ctrl-Shift-Tab has been pressed to move to an older panel then
                        // make it current panel when both keys are finally released.
                        if (SuperPuTTY.Settings.EnableControlTabSwitching && !isControlDown && !isShiftDown)
                        {
                            this.tabSwitcher.CurrentDocument = (ToolWindow)this.DockPanel.ActiveDocument;
                        }
                    }

                    if (SuperPuTTY.Settings.EnableControlTabSwitching && isControlDown && !isShiftDown && keys == Keys.Tab)
                    {
                        // Operator has pressed Ctrl-Tab, make next PuTTY panel active
                        if (isKeyDown && this.DockPanel.ActiveDocument is ToolWindowDocument)
                        {
                            if (this.tabSwitcher.MoveToNextDocument())
                            {
                                // Eat the keystroke
                                return (IntPtr)1;
                            }
                        }
                    }

                    if (SuperPuTTY.Settings.EnableControlTabSwitching && isControlDown && isShiftDown && keys == Keys.Tab)
                    {
                        // Operator has pressed Ctrl-Shift-Tab, make previous PuTTY panel active
                        if (isKeyDown && this.DockPanel.ActiveDocument is ToolWindowDocument)
                        {
                            if (this.tabSwitcher.MoveToPrevDocument())
                            {
                                // Eat the keystroke
                                return (IntPtr)1;
                            }
                        }
                    }

                    // misc action handling (eat keyup and down)
                    if (SuperPuTTY.Settings.EnableKeyboadShortcuts &&
                        isKeyDown &&
                        keys != Keys.LControlKey && keys != Keys.RControlKey &&
                        keys != Keys.LMenu && keys != Keys.RMenu &&
                        keys != Keys.LShiftKey && keys != Keys.RShiftKey)
                    {
                        if (isControlDown) keys |= Keys.Control;
                        if (isShiftDown) keys |= Keys.Shift;
                        if (isAltDown) keys |= Keys.Alt;

                        if (Log.Logger.IsEnabledFor(Level.Trace))
                        {
                            Log.DebugFormat("#### TryExecute shortcut: keys={0}", keys);
                        }
                        SuperPuttyAction action;
                        if (this.shortcuts.TryGetValue(keys, out action))
                        {
                            // post action to avoid getting errant keystrokes (e.g. allow current to be eaten)
                            this.BeginInvoke(new Action(() =>
                            {
                                ExecuteSuperPuttyAction(action);
                            }));
                            return (IntPtr)1;
                        }
                    }
                }

                if (isKeyDown && this.liveInput)
                {
                    MirrorKey((Keys)vkCode, lParam);
                }
            }

            return NativeMethods.CallNextHookEx(kbHookID, nCode, wParam, lParam);
        }

        private static IntPtr SetMHook(NativeMethods.LowLevelKMProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, proc, NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr MHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)NativeMethods.WM.LBUTTONUP || wParam == (IntPtr)NativeMethods.WM.RBUTTONUP) && IsForegroundWindow(this))
            {
                this.BringToFront();
                //if (!Menu_IsMouseOver()) dockPanel.Focus();
            }
            return NativeMethods.CallNextHookEx(mHookID, nCode, wParam, lParam);
        }

        private static bool IsForegroundWindow(Form parent)
        {
            IntPtr fgWindow = NativeMethods.GetForegroundWindow();
            if (parent.Handle == fgWindow) return true; // main form is FG
            //foreach (Form f in Application.OpenForms) { if (f.Handle == fgWindow) return true; }
            List<IntPtr> result = new List<IntPtr>();
            GCHandle listHandle = GCHandle.Alloc(result);
            try
            {
                NativeMethods.EnumWindowProc childProc = new NativeMethods.EnumWindowProc(EnumWindow);
                NativeMethods.EnumChildWindows(parent.Handle, childProc, GCHandle.ToIntPtr(listHandle));
            }
            finally
            {
                if (listHandle.IsAllocated)
                    listHandle.Free();
            }
            return result.Count > 0;
        }

        private static bool EnumWindow(IntPtr handle, IntPtr pointer)
        {
            GCHandle gch = GCHandle.FromIntPtr(pointer);
            List<IntPtr> list = gch.Target as List<IntPtr>;
            if (handle == NativeMethods.GetForegroundWindow()) list.Add(handle);
            if (list.Count == 0) return true; else return false;
        }

        void UpdateShortcutsFromSettings()
        {
            // remove old
            this.shortcuts.Clear();
            this.optionsToolStripMenuItem.ShortcutKeys = Keys.None;
            this.kioskModeToolStripMenuItem.ShortcutKeys = Keys.None;
            this.copySessionToolStripMenuItem.ShortcutKeys = Keys.None;
            this.copyLastCommandToolStripMenuItem.ShortcutKeys = Keys.None;

            // reload new definitions
            foreach (KeyboardShortcut ks in SuperPuTTY.Settings.LoadShortcuts())
            {
                try
                {
                    // shortcuts
                    SuperPuttyAction action = (SuperPuttyAction)Enum.Parse(typeof(SuperPuttyAction), ks.Name);
                    Keys keys = ks.Key | ks.Modifiers;
                    if (keys != Keys.None)
                    {
                        this.shortcuts.Add(keys, action);
                    }

                    // sync menu items
                    switch (action)
                    {
                        case SuperPuttyAction.Options:
                            this.optionsToolStripMenuItem.ShortcutKeys = keys;
                            break;
                        case SuperPuttyAction.KioskMode:
                            this.kioskModeToolStripMenuItem.ShortcutKeys = keys;
                            break;
                        case SuperPuttyAction.CopySessionToClipboard:
                            this.copySessionToolStripMenuItem.ShortcutKeys = keys;
                            break;
                        case SuperPuttyAction.CopyLastCommand:
                            this.copyLastCommandToolStripMenuItem.ShortcutKeys = keys;
                            break;
                        case SuperPuttyAction.OpenSession:
                            this.openSessionToolStripMenuItem.ShortcutKeys = keys;
                            break;
                        case SuperPuttyAction.SwitchSession:
                            this.switchSessionToolStripMenuItem.ShortcutKeys = keys;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.ErrorFormat("Error creating shortcut: " + ks + ", disabled.", ex);
                }

            }
        }

        bool ExecuteSuperPuttyAction(SuperPuttyAction action)
        {
            ctlPuttyPanel activePanel = this.DockPanel.ActiveDocument as ctlPuttyPanel;
            bool success = true;

            Log.InfoFormat("Executing action, name={0}", action);
            switch (action)
            {
                case SuperPuttyAction.CloseTab:
                    ToolWindow win = this.DockPanel.ActiveDocument as ToolWindow;
                    if (win != null) { win.Close(); }
                    break;
                case SuperPuttyAction.NextTab:
                    this.tabSwitcher.MoveToNextDocument();
                    break;
                case SuperPuttyAction.PrevTab:
                    this.tabSwitcher.MoveToPrevDocument();
                    break;
                case SuperPuttyAction.KioskMode:
                    this.ToggleKiosk();
                    break;
                case SuperPuttyAction.CopySessionToClipboard:
                    this.CopyActiveSessionToClipboard(false);
                    break;
                case SuperPuttyAction.CopyLastCommand:
                    this.CopyActiveSessionToClipboard(true);
                    break;
                case SuperPuttyAction.OpenSession:
                    KeyEventWindowActivator.ActivateForm(this);
                    this.openSessionToolStripMenuItem.PerformClick();
                    break;
                case SuperPuttyAction.SwitchSession:
                    KeyEventWindowActivator.ActivateForm(this);
                    this.switchSessionToolStripMenuItem.PerformClick();
                    break;
                case SuperPuttyAction.Options:
                    KeyEventWindowActivator.ActivateForm(this);
                    this.optionsToolStripMenuItem.PerformClick();
                    break;
                case SuperPuttyAction.DuplicateSession:
                    if (activePanel != null && activePanel.Session != null)
                        SuperPuTTY.OpenPuttySession(activePanel.Session);
                    break;
                case SuperPuttyAction.GotoCommandBar:
                    if (!this.isKioskMode)
                    {
                        KeyEventWindowActivator.ActivateForm(this);
                        if (!this.tsCommands.Visible)
                        {
                            this.toggleCheckedState(this.sendCommandsToolStripMenuItem, EventArgs.Empty);
                        }
                        this.tsSendCommandCombo.Focus();
                    }
                    break;
                case SuperPuttyAction.GotoConnectionBar:
                    // perhaps consider allowing this later...need to really have a better approach to the state saving/invoking the toggle.
                    if (QuickConnectionBarEnabled && !this.isKioskMode)
                    {
                        KeyEventWindowActivator.ActivateForm(this);
                        if (!this.tsConnect.Visible)
                        {
                            this.toggleCheckedState(this.quickConnectionToolStripMenuItem, EventArgs.Empty);
                        }
                        this.tbTxtBoxHost.Focus();
                    }
                    break;
                case SuperPuttyAction.FocusActiveSession:
                    // focus on current super putty session...or at least try to
                    KeyEventWindowActivator.ActivateForm(this);
                    if (activePanel != null)
                        activePanel.SetFocusToChildApplication("ExecuteAction");
                    break;
                case SuperPuttyAction.OpenScriptEditor:
                    KeyEventWindowActivator.ActivateForm(this);
                    toolStripButtonRunScript_Click(this, EventArgs.Empty);
                    break;
                case SuperPuttyAction.RenameTab:                    
                    if (activePanel != null && activePanel.Session != null)
                    {
                        dlgRenameItem dialog = new dlgRenameItem
                        {
                            ItemName = activePanel.Text,
                            DetailName = activePanel.Session.SessionId
                        };

                        if (dialog.ShowDialog(this) == DialogResult.OK)
                        {
                            activePanel.Text = activePanel.TextOverride = dialog.ItemName;                            
                        }                        
                    }
                    break;
                default:
                    success = false;
                    break;
            }

            return success;
        }


        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if ((keyData & Keys.Alt) == Keys.Alt)
            {
                menuStrip1.Visible = true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        #endregion
    }
}
