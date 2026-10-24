using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SuperPutty.Data;

namespace SuperPutty.Gui
{
    /// <summary>
    /// Colours and small shapes shared by the owner-drawn panes (Commands, Containers). Built on system colours;
    /// with a high-contrast theme, selection uses the plain system highlight and no tints are mixed.
    /// </summary>
    public static class PaneStyle
    {
        public static readonly Color Green = Color.FromArgb(46, 160, 67);
        public static readonly Color Amber = Color.FromArgb(214, 137, 0);
        public static readonly Color Red = Color.FromArgb(207, 34, 46);
        public static readonly Color Gold = Color.FromArgb(232, 177, 0);
        /// <summary>Placeholders in commands</summary>
        public static readonly Color Accent = Color.FromArgb(136, 57, 190);

        private const TextFormatFlags BaseFlags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding
            | TextFormatFlags.VerticalCenter;

        private static bool HighContrast
        {
            get { return SystemInformation.HighContrast; }
        }

        /// <summary>Mix two colours; amount = share of the first one (0..1)</summary>
        public static Color Blend(Color a, Color b, double amount)
        {
            return Color.FromArgb(
                (int)Math.Round(a.R * amount + b.R * (1 - amount)),
                (int)Math.Round(a.G * amount + b.G * (1 - amount)),
                (int)Math.Round(a.B * amount + b.B * (1 - amount)));
        }

        public static Color RowBackground(Color window, bool selected, bool focused, bool hot)
        {
            if (selected)
            {
                return HighContrast ? SystemColors.Highlight : Blend(SystemColors.Highlight, window, focused ? 0.28 : 0.14);
            }
            return hot && !HighContrast ? Blend(SystemColors.Highlight, window, 0.07) : window;
        }

        public static Color RowForeground(Color text, bool selected, bool focused)
        {
            return selected && HighContrast ? SystemColors.HighlightText : text;
        }

        public static Color RowSubtle(bool selected, bool focused)
        {
            return selected && HighContrast ? SystemColors.HighlightText : SystemColors.GrayText;
        }

        /// <summary>Colour of a container's status dot</summary>
        public static Color StateColor(ContainerState state)
        {
            switch (state)
            {
                case ContainerState.Healthy:
                case ContainerState.Running:
                    return Green;
                case ContainerState.Starting:
                case ContainerState.Restarting:
                case ContainerState.Paused:
                    return Amber;
                case ContainerState.Unhealthy:
                case ContainerState.Failed:
                    return Red;
                default:
                    return SystemColors.GrayText;
            }
        }

        /// <summary>Single-line text, cut with an ellipsis; returns the width used</summary>
        public static int DrawText(Graphics g, string text, Font font, Rectangle bounds, Color color, TextFormatFlags align = TextFormatFlags.Left)
        {
            if (bounds.Width <= 0 || string.IsNullOrEmpty(text))
            {
                return 0;
            }
            TextRenderer.DrawText(g, text, font, bounds, color, BaseFlags | TextFormatFlags.EndEllipsis | align);
            return Math.Min(bounds.Width, MeasureText(g, text, font));
        }

        public static int MeasureText(Graphics g, string text, Font font)
        {
            return string.IsNullOrEmpty(text) ? 0 : TextRenderer.MeasureText(g, text, font, Size.Empty, BaseFlags).Width;
        }

        /// <summary>▸ / ▾ in the middle of bounds</summary>
        public static void DrawChevron(Graphics g, Rectangle bounds, bool expanded, Color color)
        {
            int cx = bounds.Left + bounds.Width / 2;
            int cy = bounds.Top + bounds.Height / 2;
            Point[] points = expanded
                ? new[] { new Point(cx - 4, cy - 2), new Point(cx + 4, cy - 2), new Point(cx, cy + 3) }
                : new[] { new Point(cx - 2, cy - 4), new Point(cx + 3, cy), new Point(cx - 2, cy + 4) };
            WithSmoothing(g, () =>
            {
                using (SolidBrush brush = new SolidBrush(color))
                {
                    g.FillPolygon(brush, points);
                }
            });
        }

        public static void DrawDot(Graphics g, Point center, int diameter, Color color)
        {
            WithSmoothing(g, () =>
            {
                using (SolidBrush brush = new SolidBrush(color))
                {
                    g.FillEllipse(brush, center.X - diameter / 2f, center.Y - diameter / 2f, diameter, diameter);
                }
            });
        }

        /// <summary>A five-pointed star (Favorites)</summary>
        public static void DrawStar(Graphics g, Point center, int radius, Color color)
        {
            PointF[] points = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                double r = i % 2 == 0 ? radius : radius * 0.45;
                double angle = -Math.PI / 2 + i * Math.PI / 5;
                points[i] = new PointF((float)(center.X + r * Math.Cos(angle)), (float)(center.Y + r * Math.Sin(angle)));
            }
            WithSmoothing(g, () =>
            {
                using (SolidBrush brush = new SolidBrush(color))
                {
                    g.FillPolygon(brush, points);
                }
            });
        }

        /// <summary>▶ (run) in the middle of bounds</summary>
        public static void DrawPlay(Graphics g, Rectangle bounds, Color color)
        {
            int cx = bounds.Left + bounds.Width / 2;
            int cy = bounds.Top + bounds.Height / 2;
            Point[] points = { new Point(cx - 3, cy - 5), new Point(cx + 5, cy), new Point(cx - 3, cy + 5) };
            WithSmoothing(g, () =>
            {
                using (SolidBrush brush = new SolidBrush(color))
                {
                    g.FillPolygon(brush, points);
                }
            });
        }

        /// <summary>Three small dots, vertically centred: marks text cut out of the middle</summary>
        public static void DrawGap(Graphics g, Rectangle cell, Color color)
        {
            int cy = cell.Top + cell.Height / 2;
            float step = Math.Max(2f, cell.Width / 3f);
            float left = cell.Left + (cell.Width - step * 2) / 2f;
            WithSmoothing(g, () =>
            {
                using (SolidBrush brush = new SolidBrush(color))
                {
                    for (int i = 0; i < 3; i++)
                    {
                        g.FillEllipse(brush, left + i * step - 1f, cy - 1f, 2f, 2f);
                    }
                }
            });
        }

        /// <summary>Width a chip with this text needs</summary>
        public static int ChipWidth(Graphics g, string text, Font font)
        {
            return MeasureText(g, text, font) + 8;
        }

        /// <summary>A small rounded label, e.g. a published port</summary>
        public static void DrawChip(Graphics g, Rectangle bounds, string text, Font font, Color window, Color textColor)
        {
            Color border = HighContrast ? textColor : Blend(SystemColors.GrayText, window, 0.45);
            Color fill = HighContrast ? window : Blend(SystemColors.GrayText, window, 0.10);
            WithSmoothing(g, () =>
            {
                using (GraphicsPath path = RoundedRectangle(new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1), 4))
                using (SolidBrush brush = new SolidBrush(fill))
                using (Pen pen = new Pen(border))
                {
                    g.FillPath(brush, path);
                    g.DrawPath(pen, path);
                }
            });
            TextRenderer.DrawText(g, text, font, bounds, textColor, BaseFlags | TextFormatFlags.HorizontalCenter);
        }

        /// <summary>A thin line, e.g. under a section header</summary>
        public static void DrawRule(Graphics g, int left, int right, int y, Color window)
        {
            using (Pen pen = new Pen(HighContrast ? SystemColors.WindowText : Blend(SystemColors.GrayText, window, 0.35)))
            {
                g.DrawLine(pen, left, y, right, y);
            }
        }

        private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void WithSmoothing(Graphics g, Action draw)
        {
            SmoothingMode previous = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                draw();
            }
            finally
            {
                g.SmoothingMode = previous;
            }
        }
    }
}
