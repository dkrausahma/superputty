using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;
using log4net;
using SuperPutty.Data;
using WeifenLuo.WinFormsUI.Docking;

namespace SuperPutty.Utils
{
    /// <summary>
    /// Wraps the theme's tab strip factory and paints each terminal tab's environment onto it: a coloured bar along
    /// the top edge (prod red, qual amber, dev green) and, for prod, a red badge - so a production session is
    /// recognisable at a glance even with tab icons switched off. The tab rectangles come from the VS2005 strip's
    /// private GetTabRectangle(int); the index matches the pane's DisplayingContents.
    /// </summary>
    public class EnvironmentTabStripFactory : DockPanelExtender.IDockPaneStripFactory
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(EnvironmentTabStripFactory));

        public static readonly Color ProdColor = Color.FromArgb(205, 40, 40);
        public static readonly Color QualColor = Color.FromArgb(235, 160, 30);
        public static readonly Color DevColor = Color.FromArgb(50, 150, 70);

        private readonly DockPanelExtender.IDockPaneStripFactory inner;

        /// <summary>The environment of a tab's content (None for non-terminal tabs)</summary>
        private readonly Func<IDockContent, EnvironmentKind> environmentOf;

        public EnvironmentTabStripFactory(DockPanelExtender.IDockPaneStripFactory inner, Func<IDockContent, EnvironmentKind> environmentOf)
        {
            this.inner = inner;
            this.environmentOf = environmentOf;
        }

        public DockPaneStripBase CreateDockPaneStrip(DockPane pane)
        {
            DockPaneStripBase strip = this.inner.CreateDockPaneStrip(pane);
            MethodInfo tabRectangle = strip.GetType().GetMethod("GetTabRectangle", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int) }, null);
            if (tabRectangle == null)
            {
                Log.WarnFormat("{0} has no GetTabRectangle(int), environment markers are not shown on tabs", strip.GetType().Name);
                return strip;
            }
            strip.Paint += delegate (object sender, PaintEventArgs e) { PaintMarkers(strip, pane, tabRectangle, e.Graphics); };
            return strip;
        }

        private void PaintMarkers(DockPaneStripBase strip, DockPane pane, MethodInfo tabRectangle, Graphics g)
        {
            if (pane.DockState != DockState.Document)
            {
                return;
            }
            for (int i = 0; i < pane.DisplayingContents.Count; i++)
            {
                EnvironmentKind environment = this.environmentOf(pane.DisplayingContents[i]);
                if (environment == EnvironmentKind.None)
                {
                    continue;
                }
                Rectangle tab;
                try
                {
                    tab = (Rectangle)tabRectangle.Invoke(strip, new object[] { i });
                }
                catch (TargetInvocationException)
                {
                    continue;
                }
                if (tab.Width <= 0 || tab.Height <= 0)
                {
                    continue; // tab not shown (overflow)
                }

                Color color = environment == EnvironmentKind.Prod ? ProdColor : environment == EnvironmentKind.Qual ? QualColor : DevColor;
                int bar = environment == EnvironmentKind.Prod ? 3 : 2;
                using (SolidBrush brush = new SolidBrush(color))
                {
                    g.FillRectangle(brush, tab.Left + 1, tab.Top, tab.Width - 2, bar);
                }
                if (environment == EnvironmentKind.Prod)
                {
                    PaintProdBadge(g, new Rectangle(tab.Right - 12, tab.Top + 4, 9, 9));
                }
            }
        }

        /// <summary>A red round badge with a white exclamation mark</summary>
        private static void PaintProdBadge(Graphics g, Rectangle r)
        {
            SmoothingMode previous = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush red = new SolidBrush(ProdColor))
            using (Pen white = new Pen(Color.White, 1.5f))
            {
                g.FillEllipse(red, r);
                g.DrawEllipse(Pens.White, r);
                float x = r.Left + r.Width / 2f;
                g.DrawLine(white, x, r.Top + 2f, x, r.Top + r.Height - 4.5f);
                g.FillRectangle(Brushes.White, x - 0.75f, r.Top + r.Height - 3f, 1.5f, 1.5f);
            }
            g.SmoothingMode = previous;
        }
    }
}
