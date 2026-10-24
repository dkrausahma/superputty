using System;
using System.Drawing;
using System.Windows.Forms;
using SuperPutty.Data;

namespace SuperPutty.Gui
{
    /// <summary>
    /// The menu of recently opened sessions, shared by the Sessions pane's clock button and the icon bar:
    /// newest first, each with its session icon and its folder at the right; a click opens the session.
    /// </summary>
    public static class RecentSessionsMenu
    {
        /// <summary>Replace the items with the current recent sessions</summary>
        public static void Fill(ToolStripItemCollection items)
        {
            items.Clear();
            foreach (SessionData session in SuperPuTTY.GetRecentSessions())
            {
                SessionData open = session;
                int slash = session.SessionId.LastIndexOf('/');
                ToolStripMenuItem item = new ToolStripMenuItem(session.SessionName)
                {
                    Image = SessionImage(session),
                    // the folder, right-aligned and grey like a shortcut
                    ShortcutKeyDisplayString = slash > 0 ? session.SessionId.Substring(0, slash) : string.Empty,
                    ToolTipText = session.SessionId + (string.IsNullOrEmpty(session.Host) ? string.Empty : "\n" + session.Host)
                };
                item.Click += delegate { SuperPuTTY.OpenPuttySession(open); };
                items.Add(item);
            }
            if (items.Count == 0)
            {
                items.Add(new ToolStripMenuItem("No recent sessions") { Enabled = false });
                return;
            }
            items.Add(new ToolStripSeparator());
            ToolStripMenuItem clear = new ToolStripMenuItem("Clear List");
            clear.Click += delegate { SuperPuTTY.ClearRecentSessions(); };
            items.Add(clear);
        }

        /// <summary>Show the menu below a control</summary>
        public static void ShowBelow(Control control)
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowItemToolTips = true };
            Fill(menu.Items);
            menu.Closed += delegate { control.BeginInvoke((MethodInvoker)menu.Dispose); };
            menu.Show(control, new Point(0, control.Height));
        }

        private static Image SessionImage(SessionData session)
        {
            ImageList images = SuperPuTTY.Images;
            if (images == null)
            {
                return null;
            }
            string key = string.IsNullOrEmpty(session.ImageKey) ? SessionTreeview.ImageKeySession : session.ImageKey;
            return images.Images.ContainsKey(key) ? images.Images[key] : images.Images[SessionTreeview.ImageKeySession];
        }
    }
}
