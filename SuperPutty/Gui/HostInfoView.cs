using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using SuperPutty.Data;

namespace SuperPutty.Gui
{
    /// <summary>
    /// Draws a <see cref="HostInfo"/>: host name, OS and addresses, bars for CPU, memory and disks (coloured with
    /// the health badges' thresholds), logged-in users, processes and the busiest ones, problems, and a status
    /// line. Scrolls vertically when the pane is shorter than the content.
    /// </summary>
    public class HostInfoView : ScrollableControl
    {
        private const int Pad = 8;
        private const int RowHeight = 18;
        private const int LabelWidth = 74;
        private const int MaxDisks = 6;

        private HostInfo info;
        private string message;
        private bool messageIsError;
        private string status;
        private Font boldFont;
        private Font titleFont;
        private Font smallFont;

        public HostInfoView()
        {
            this.DoubleBuffered = true;
            this.ResizeRedraw = true;
            this.AutoScroll = true;
            this.BackColor = SystemColors.Window;
            this.ForeColor = SystemColors.WindowText;
        }

        /// <summary>The snapshot shown; null shows only the message</summary>
        public HostInfo Info
        {
            get { return this.info; }
            set { this.info = value; Invalidate(); }
        }

        /// <summary>Text instead of (or, for errors, above) the values: "Connecting…", why the connection failed</summary>
        public void SetMessage(string text, bool isError)
        {
            this.message = text;
            this.messageIsError = isError;
            Invalidate();
        }

        /// <summary>The line at the bottom: "Live · updated 10:32:05"</summary>
        public string Status
        {
            get { return this.status; }
            set { this.status = value; Invalidate(); }
        }

        private Font BoldFont { get { return this.boldFont ?? (this.boldFont = new Font(this.Font, FontStyle.Bold)); } }
        private Font TitleFont { get { return this.titleFont ?? (this.titleFont = new Font(this.Font.FontFamily, this.Font.Size + 1.5f, FontStyle.Bold)); } }
        private Font SmallFont { get { return this.smallFont ?? (this.smallFont = new Font(this.Font.FontFamily, this.Font.Size - 1f)); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(this.BackColor);
            int width = this.ClientSize.Width;
            int y = Pad + this.AutoScrollPosition.Y;
            int left = Pad;
            int right = width - Pad;
            Color subtle = SystemColors.GrayText;

            if (!string.IsNullOrEmpty(this.message))
            {
                Rectangle box = new Rectangle(left, y, right - left, 1000);
                TextFormatFlags flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
                Size size = TextRenderer.MeasureText(g, this.message, this.Font, new Size(box.Width, int.MaxValue), flags);
                TextRenderer.DrawText(g, this.message, this.Font, new Rectangle(left, y, box.Width, size.Height), this.messageIsError ? PaneStyle.Red : subtle, flags);
                y += size.Height + Pad;
            }

            HostInfo host = this.info;
            if (host != null)
            {
                y = DrawHeader(g, host, left, right, y, subtle);
                y = Rule(g, left, right, y);
                y = DrawBars(g, host, left, right, y, subtle);
                y = Rule(g, left, right, y);
                y = DrawLists(g, host, left, right, y, subtle);
            }

            if (!string.IsNullOrEmpty(this.status))
            {
                y += 4;
                PaneStyle.DrawText(g, this.status, this.SmallFont, new Rectangle(left, y, right - left, RowHeight), subtle);
                y += RowHeight;
            }

            // content height for the scroll bar
            int contentHeight = y - this.AutoScrollPosition.Y + Pad;
            if (this.AutoScrollMinSize.Height != contentHeight)
            {
                this.AutoScrollMinSize = new Size(0, contentHeight);
            }
        }

        private int DrawHeader(Graphics g, HostInfo host, int left, int right, int y, Color subtle)
        {
            // host name first; OS and kernel at its right if they fit, otherwise on a line of their own
            string name = host.HostName ?? "…";
            string os = string.Join(" · ", new[] { host.Os, host.Kernel }.Where(s => !string.IsNullOrEmpty(s)));
            int nameWidth = PaneStyle.MeasureText(g, name, this.TitleFont);
            int osWidth = PaneStyle.MeasureText(g, os, this.SmallFont);
            bool osBeside = left + nameWidth + 12 + osWidth <= right;
            PaneStyle.DrawText(g, name, this.TitleFont, Rectangle.FromLTRB(left, y, right, y + RowHeight + 2), this.ForeColor);
            if (osBeside)
            {
                PaneStyle.DrawText(g, os, this.SmallFont, Rectangle.FromLTRB(right - osWidth, y, right, y + RowHeight + 2), subtle, TextFormatFlags.Right);
            }
            y += RowHeight + 2;
            if (!osBeside && os.Length > 0)
            {
                PaneStyle.DrawText(g, os, this.SmallFont, Rectangle.FromLTRB(left, y, right, y + RowHeight - 2), subtle);
                y += RowHeight - 2;
            }

            // addresses, uptime at the right; public and private on separate lines when one line is too short
            string uptime = host.Uptime == null ? string.Empty : "up " + HostInfo.FormatUptimeShort(host.Uptime.Value);
            int uptimeWidth = PaneStyle.MeasureText(g, uptime, this.SmallFont);
            PaneStyle.DrawText(g, uptime, this.SmallFont, Rectangle.FromLTRB(right - uptimeWidth, y, right, y + RowHeight), subtle, TextFormatFlags.Right);
            List<string> addresses = new List<string>();
            if (host.PublicAddresses.Any())
            {
                addresses.Add("Public " + string.Join(", ", host.PublicAddresses));
            }
            if (host.PrivateAddresses.Any())
            {
                addresses.Add("Private " + string.Join(", ", host.PrivateAddresses));
            }
            int room = right - uptimeWidth - 8 - left;
            string oneLine = string.Join(" · ", addresses);
            if (addresses.Count < 2 || PaneStyle.MeasureText(g, oneLine, this.SmallFont) <= room)
            {
                PaneStyle.DrawText(g, oneLine, this.SmallFont, Rectangle.FromLTRB(left, y, left + room, y + RowHeight), this.ForeColor);
                return y + RowHeight;
            }
            foreach (string line in addresses)
            {
                PaneStyle.DrawText(g, line, this.SmallFont, Rectangle.FromLTRB(left, y, left + room, y + RowHeight), this.ForeColor);
                y += RowHeight - 2;
                room = right - left; // only the first line shares its row with the uptime
            }
            return y + 2;
        }

        private int DrawBars(Graphics g, HostInfo host, int left, int right, int y, Color subtle)
        {
            // CPU: coloured by load per CPU, like the health badge
            double loadPerCpu = host.Load == null ? 0 : host.Load[0] / Math.Max(1, host.Cpus);
            string load = host.Load == null ? string.Empty : string.Format(CultureInfo.InvariantCulture,
                "load {0:0.00} {1:0.00} {2:0.00} · {3} CPU{4}", host.Load[0], host.Load[1], host.Load[2], host.Cpus, host.Cpus == 1 ? string.Empty : "s");
            y = BarRow(g, "CPU", host.CpuPercent, Level(loadPerCpu, 1, 2), load, left, right, y, subtle);

            if (host.MemTotalKb > 0)
            {
                string memory = HostInfo.FormatSize(host.MemUsedKb) + " / " + HostInfo.FormatSize(host.MemTotalKb)
                    + (host.SwapTotalKb > 0 ? " · swap " + HostInfo.FormatSize(host.SwapUsedKb) : string.Empty);
                int percent = host.MemoryPercent ?? 0;
                y = BarRow(g, "Memory", percent, Level(percent, 85, 95), memory, left, right, y, subtle);
            }
            // "/" first, then the fullest; the rest only as a count (all of them are in the copied text)
            List<DiskUsage> disks = host.Disks.OrderBy(d => d.Mount == "/" ? 0 : 1).ThenByDescending(d => d.Percent).ThenBy(d => d.Mount, StringComparer.Ordinal).ToList();
            foreach (DiskUsage disk in disks.Take(MaxDisks))
            {
                string detail = HostInfo.FormatSize(disk.AvailableKb) + " free of " + HostInfo.FormatSize(disk.SizeKb);
                y = BarRow(g, "Disk " + disk.Mount, disk.Percent, Level(disk.Percent, 80, 90), detail, left, right, y, subtle);
            }
            if (disks.Count > MaxDisks)
            {
                PaneStyle.DrawText(g, string.Format(CultureInfo.InvariantCulture, "+{0} less full filesystem(s); Copy lists all", disks.Count - MaxDisks),
                    this.SmallFont, Rectangle.FromLTRB(left + LabelWidth, y, right, y + RowHeight), subtle);
                y += RowHeight;
            }

            // GPUs: busy share coloured by temperature (a busy GPU is fine, a hot one isn't). VRAM is only amber when
            // practically full: inference servers such as vLLM reserve ~90 % on purpose, so a high fill level is normal
            foreach (GpuInfo gpu in host.Gpus)
            {
                string detail = string.Join(" · ", new[] { gpu.Name, gpu.TemperatureC == null ? null : gpu.TemperatureC + " °C" }
                    .Where(s => !string.IsNullOrEmpty(s)));
                y = BarRow(g, "GPU " + gpu.Index, gpu.UtilizationPercent, Level(gpu.TemperatureC ?? 0, 83, 90), detail, left, right, y, subtle);
                string memory = HostInfo.FormatSize(gpu.MemoryUsedMb * 1024) + " / " + HostInfo.FormatSize(gpu.MemoryTotalMb * 1024);
                y = BarRow(g, "   VRAM", gpu.MemoryPercent, Level(gpu.MemoryPercent, 98, 101), memory, left, right, y, subtle);
            }
            return y;
        }

        /// <summary>0 = ok, 1 = warning, 2 = critical</summary>
        private static int Level(double value, double warning, double critical)
        {
            return value >= critical ? 2 : value >= warning ? 1 : 0;
        }

        /// <summary>"Label  [bar]  42 %  detail"</summary>
        private int BarRow(Graphics g, string label, double? percent, int level, string detail, int left, int right, int y, Color subtle)
        {
            Rectangle row = new Rectangle(left, y, right - left, RowHeight);
            PaneStyle.DrawText(g, label, this.Font, new Rectangle(row.Left, row.Top, LabelWidth - 4, row.Height), this.ForeColor);
            int barWidth = Math.Max(40, Math.Min(160, (right - left - LabelWidth) * 3 / 10));
            Rectangle bar = new Rectangle(row.Left + LabelWidth, row.Top + (row.Height - 8) / 2, barWidth, 8);
            Color color = level == 2 ? PaneStyle.Red : level == 1 ? PaneStyle.Amber : PaneStyle.Green;
            PaneStyle.DrawBar(g, bar, percent == null ? 0 : percent.Value / 100.0, color, this.BackColor);

            string text = percent == null ? "…" : Math.Round(percent.Value).ToString(CultureInfo.InvariantCulture) + " %";
            int textLeft = bar.Right + 6;
            PaneStyle.DrawText(g, text, this.Font, new Rectangle(textLeft, row.Top, 38, row.Height), level > 0 ? color : this.ForeColor, TextFormatFlags.Right);
            PaneStyle.DrawText(g, detail, this.SmallFont, Rectangle.FromLTRB(textLeft + 44, row.Top, right, row.Bottom), subtle);
            return y + RowHeight;
        }

        private int DrawLists(Graphics g, HostInfo host, int left, int right, int y, Color subtle)
        {
            if (host.HasSlowData)
            {
                Label(g, "Logged in", left, y);
                if (host.Users.Count == 0)
                {
                    PaneStyle.DrawText(g, "nobody", this.Font, Rectangle.FromLTRB(left + LabelWidth, y, right, y + RowHeight), subtle);
                    y += RowHeight;
                }
                foreach (LoggedInUser user in host.Users)
                {
                    int x = left + LabelWidth;
                    x += PaneStyle.DrawText(g, user.User, this.BoldFont, Rectangle.FromLTRB(x, y, right, y + RowHeight), this.ForeColor) + 6;
                    string rest = string.Join("  ", new[] { user.Terminal, string.IsNullOrEmpty(user.From) ? null : "from " + user.From,
                        string.IsNullOrEmpty(user.Since) ? null : "since " + user.Since }.Where(s => !string.IsNullOrEmpty(s)));
                    PaneStyle.DrawText(g, rest, this.SmallFont, Rectangle.FromLTRB(x, y, right, y + RowHeight), subtle);
                    y += RowHeight;
                }
            }

            if (host.Processes != null)
            {
                Label(g, "Processes", left, y);
                PaneStyle.DrawText(g, host.Processes.Value.ToString(CultureInfo.InvariantCulture), this.Font,
                    Rectangle.FromLTRB(left + LabelWidth, y, right, y + RowHeight), this.ForeColor);
                y += RowHeight;
            }
            if (host.TopProcesses.Count > 0)
            {
                Label(g, "Busiest", left, y);
                foreach (ProcessUsage process in host.TopProcesses)
                {
                    string percent = process.CpuPercent.ToString(process.CpuPercent >= 10 ? "0" : "0.0", CultureInfo.InvariantCulture) + " %";
                    PaneStyle.DrawText(g, percent, this.Font, new Rectangle(left + LabelWidth, y, 46, RowHeight), this.ForeColor, TextFormatFlags.Right);
                    PaneStyle.DrawText(g, process.Name, this.Font, Rectangle.FromLTRB(left + LabelWidth + 54, y, right, y + RowHeight), this.ForeColor);
                    y += RowHeight;
                }
            }

            if (host.GpuProcesses.Count > 0)
            {
                Label(g, "GPU memory", left, y);
                foreach (GpuProcess process in host.GpuProcesses.Take(5))
                {
                    PaneStyle.DrawText(g, HostInfo.FormatSize(process.MemoryMb * 1024), this.Font, new Rectangle(left + LabelWidth, y, 46, RowHeight), this.ForeColor, TextFormatFlags.Right);
                    string name = process.Name + (host.Gpus.Count > 1 && process.GpuIndex >= 0 ? "  (GPU " + process.GpuIndex + ")" : string.Empty);
                    PaneStyle.DrawText(g, name, this.Font, Rectangle.FromLTRB(left + LabelWidth + 54, y, right, y + RowHeight), this.ForeColor);
                    y += RowHeight;
                }
            }

            if (host.FailedUnits.Count > 0)
            {
                Label(g, "Failed units", left, y, PaneStyle.Amber);
                PaneStyle.DrawText(g, string.Join(", ", host.FailedUnits), this.Font, Rectangle.FromLTRB(left + LabelWidth, y, right, y + RowHeight), PaneStyle.Amber);
                y += RowHeight;
            }
            if (host.UnhealthyContainers > 0)
            {
                Label(g, "Containers", left, y, PaneStyle.Red);
                PaneStyle.DrawText(g, host.UnhealthyContainers + " unhealthy", this.Font, Rectangle.FromLTRB(left + LabelWidth, y, right, y + RowHeight), PaneStyle.Red);
                y += RowHeight;
            }
            return y;
        }

        private void Label(Graphics g, string text, int left, int y, Color? color = null)
        {
            PaneStyle.DrawText(g, text, this.Font, new Rectangle(left, y, LabelWidth - 4, RowHeight), color ?? SystemColors.GrayText);
        }

        private int Rule(Graphics g, int left, int right, int y)
        {
            PaneStyle.DrawRule(g, left, right, y + 4, this.BackColor);
            return y + 9;
        }

        protected override void OnFontChanged(EventArgs e)
        {
            DisposeFonts();
            base.OnFontChanged(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeFonts();
            }
            base.Dispose(disposing);
        }

        private void DisposeFonts()
        {
            foreach (Font font in new[] { this.boldFont, this.titleFont, this.smallFont })
            {
                if (font != null)
                {
                    font.Dispose();
                }
            }
            this.boldFont = this.titleFont = this.smallFont = null;
        }
    }
}
