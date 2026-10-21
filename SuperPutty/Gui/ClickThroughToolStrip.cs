using System;
using System.Windows.Forms;
using SuperPutty.Utils;

namespace SuperPutty.Gui
{
    /// <summary>
    /// A ToolStrip whose buttons react to the first click even when the main window is not active, e.g. while
    /// the keyboard focus is inside an embedded PuTTY window. A normal ToolStrip answers WM_MOUSEACTIVATE with
    /// MA_ACTIVATEANDEAT, so that first click only activates the window and every button needs two clicks.
    /// </summary>
    public class ClickThroughToolStrip : ToolStrip
    {
        private const int MA_ACTIVATE = 1;
        private const int MA_ACTIVATEANDEAT = 2;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            AcceptFirstClick(ref m);
        }

        /// <summary>Turn the "activate and discard the click" answer to WM_MOUSEACTIVATE into "activate"</summary>
        internal static void AcceptFirstClick(ref Message m)
        {
            if (m.Msg == (int)NativeMethods.WM.MOUSEACTIVATE && m.Result == (IntPtr)MA_ACTIVATEANDEAT)
            {
                m.Result = (IntPtr)MA_ACTIVATE;
            }
        }
    }
}
