using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using log4net;
using SuperPutty.Utils;

namespace SuperPutty.Gui
{
    /// <summary>
    /// Status bar entry for Pageant: a coloured dot (green = keys loaded, amber = running without keys,
    /// grey = not running) and the key count. Pageant is asked in the background every few seconds.
    /// </summary>
    public class PageantIndicator : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PageantIndicator));

        public static readonly Color KeysColor = Color.FromArgb(50, 150, 70);
        public static readonly Color NoKeysColor = Color.FromArgb(235, 160, 30);
        public static readonly Color NotRunningColor = Color.FromArgb(160, 160, 160);

        private readonly ToolStripStatusLabel label;
        private readonly Timer timer;
        private readonly Image keysImage = Dot(KeysColor);
        private readonly Image noKeysImage = Dot(NoKeysColor);
        private readonly Image notRunningImage = Dot(NotRunningColor);
        private bool querying;
        private PageantState? shown;

        /// <param name="strip">status bar to add the entry to</param>
        /// <param name="index">position in the status bar</param>
        public PageantIndicator(StatusStrip strip, int index)
        {
            this.label = new ToolStripStatusLabel
            {
                Name = "toolStripStatusLabelPageant",
                ImageAlign = ContentAlignment.MiddleLeft,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Padding = new Padding(4, 0, 4, 0),
                Image = this.notRunningImage,
                Text = "Pageant"
            };
            strip.Items.Insert(Math.Min(index, strip.Items.Count), this.label);
            strip.ShowItemToolTips = true;

            this.timer = new Timer { Interval = 10000 };
            this.timer.Tick += delegate { Refresh(); };
            this.timer.Start();
            Refresh();
        }

        /// <summary>Asks Pageant again (in the background) and updates the entry</summary>
        public void Refresh()
        {
            if (this.querying)
            {
                return;
            }
            this.querying = true;
            Task.Run(() => Pageant.Query()).ContinueWith(task =>
            {
                this.querying = false;
                if (task.IsFaulted)
                {
                    Log.Debug("Pageant query failed", task.Exception);
                    return;
                }
                Show(task.Result);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void Show(PageantState state)
        {
            if (this.label.IsDisposed || (this.shown.HasValue && this.shown.Value.Running == state.Running && this.shown.Value.KeyCount == state.KeyCount))
            {
                return;
            }
            this.shown = state;
            this.label.Text = Pageant.Describe(state);
            this.label.Image = !state.Running ? this.notRunningImage : state.KeyCount > 0 ? this.keysImage : this.noKeysImage;
            this.label.ToolTipText = !state.Running
                ? "Pageant is not running: sessions that log in with a key will ask for a password or fail"
                : state.KeyCount == null
                    ? "Pageant is running, but did not say how many keys it holds"
                    : state.KeyCount == 0
                        ? "Pageant is running, but no key is loaded"
                        : "Pageant is running with " + state.KeyCount + (state.KeyCount == 1 ? " key" : " keys") + " loaded";
        }

        private static Image Dot(Color color)
        {
            Bitmap bitmap = new Bitmap(12, 12);
            using (Graphics g = Graphics.FromImage(bitmap))
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(brush, 1, 1, 9, 9);
                using (Pen border = new Pen(ControlPaint.Dark(color), 1f))
                {
                    g.DrawEllipse(border, 1, 1, 9, 9);
                }
            }
            return bitmap;
        }

        public void Dispose()
        {
            this.timer.Dispose();
            this.keysImage.Dispose();
            this.noKeysImage.Dispose();
            this.notRunningImage.Dispose();
        }
    }
}
