using System.Reflection;
using System.Windows.Forms;
using log4net;
using WeifenLuo.WinFormsUI.Docking;

namespace SuperPutty.Utils
{
    /// <summary>
    /// Wraps the theme's tab strip factory so tab tooltips (e.g. the SSH tunnel details) also appear while the
    /// keyboard focus is inside an embedded PuTTY window. The VS2005 strip creates its ToolTip without ShowAlways,
    /// and WinForms then only shows it while the main window is active, which it isn't while PuTTY has the focus.
    /// The ToolTip is the strip's private m_toolTip field.
    /// </summary>
    public class ShowAlwaysTabToolTipFactory : DockPanelExtender.IDockPaneStripFactory
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ShowAlwaysTabToolTipFactory));

        private const string ToolTipField = "m_toolTip";

        private readonly DockPanelExtender.IDockPaneStripFactory inner;

        public ShowAlwaysTabToolTipFactory(DockPanelExtender.IDockPaneStripFactory inner)
        {
            this.inner = inner;
        }

        public DockPaneStripBase CreateDockPaneStrip(DockPane pane)
        {
            DockPaneStripBase strip = this.inner.CreateDockPaneStrip(pane);
            FieldInfo field = strip.GetType().GetField(ToolTipField, BindingFlags.NonPublic | BindingFlags.Instance);
            ToolTip toolTip = field == null ? null : field.GetValue(strip) as ToolTip;
            if (toolTip == null)
            {
                Log.WarnFormat("{0} has no ToolTip field {1}, tab tooltips only show while the window is active", strip.GetType().Name, ToolTipField);
            }
            else
            {
                toolTip.ShowAlways = true;
            }
            return strip;
        }
    }
}
