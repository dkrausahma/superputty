using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using log4net;
using WeifenLuo.WinFormsUI.Docking;

namespace SuperPutty.Utils
{
    /// <summary>
    /// Wraps the theme's tab strip factory so files dragged from Explorer can be dropped on a terminal tab's header.
    /// The terminal area itself belongs to PuTTY (another process), so the tab header is the reliable drop target.
    /// The tab under the cursor comes from the strip's protected HitTest(Point); its index matches the pane's
    /// DisplayingContents.
    /// </summary>
    public class FileDropTabStripFactory : DockPanelExtender.IDockPaneStripFactory
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FileDropTabStripFactory));

        private static readonly MethodInfo HitTestMethod = typeof(DockPaneStripBase).GetMethod("HitTest",
            BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(Point) }, null);

        private readonly DockPanelExtender.IDockPaneStripFactory inner;

        /// <summary>Decides whether a tab accepts dropped files</summary>
        private readonly Func<IDockContent, bool> acceptsFiles;

        /// <summary>Handles files dropped on a tab: (tab content, file and folder paths)</summary>
        private readonly Action<IDockContent, string[]> filesDropped;

        public FileDropTabStripFactory(DockPanelExtender.IDockPaneStripFactory inner, Func<IDockContent, bool> acceptsFiles, Action<IDockContent, string[]> filesDropped)
        {
            this.inner = inner;
            this.acceptsFiles = acceptsFiles;
            this.filesDropped = filesDropped;
        }

        public DockPaneStripBase CreateDockPaneStrip(DockPane pane)
        {
            DockPaneStripBase strip = this.inner.CreateDockPaneStrip(pane);
            if (HitTestMethod == null)
            {
                Log.Warn("DockPaneStripBase.HitTest(Point) not found, files can't be dropped on tabs");
                return strip;
            }

            strip.AllowDrop = true;
            DragEventHandler over = delegate (object sender, DragEventArgs e)
            {
                IDockContent content = TabAt(strip, pane, new Point(e.X, e.Y));
                e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) && content != null && this.acceptsFiles(content)
                    ? DragDropEffects.Copy : DragDropEffects.None;
            };
            strip.DragEnter += over;
            strip.DragOver += over;
            strip.DragDrop += delegate (object sender, DragEventArgs e)
            {
                IDockContent content = TabAt(strip, pane, new Point(e.X, e.Y));
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (content != null && files != null && files.Length > 0 && this.acceptsFiles(content))
                {
                    // let Explorer finish the drag before a dialog opens
                    strip.BeginInvoke((System.Windows.Forms.MethodInvoker)delegate { this.filesDropped(content, files); });
                }
            };
            return strip;
        }

        /// <summary>The tab content under a screen point, or null</summary>
        private static IDockContent TabAt(DockPaneStripBase strip, DockPane pane, Point screenPoint)
        {
            try
            {
                int index = (int)HitTestMethod.Invoke(strip, new object[] { strip.PointToClient(screenPoint) });
                return index >= 0 && index < pane.DisplayingContents.Count ? pane.DisplayingContents[index] : null;
            }
            catch (TargetInvocationException ex)
            {
                Log.Debug("Tab hit test failed", ex);
                return null;
            }
        }
    }
}
