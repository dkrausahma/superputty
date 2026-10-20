using System.Windows.Forms;

namespace SuperPutty.Gui
{
    /// <summary>
    /// A MenuStrip that opens its menus on the first click even when the main window is not active, e.g. while
    /// the keyboard focus is inside an embedded PuTTY window. See <see cref="ClickThroughToolStrip"/>.
    /// </summary>
    public class ClickThroughMenuStrip : MenuStrip
    {
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            ClickThroughToolStrip.AcceptFirstClick(ref m);
        }
    }
}
