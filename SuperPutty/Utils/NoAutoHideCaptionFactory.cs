using System;
using System.Reflection;
using System.Windows.Forms;
using log4net;
using WeifenLuo.WinFormsUI.Docking;

namespace SuperPutty.Utils
{
    /// <summary>
    /// Wraps the theme's pane caption factory and hides the auto-hide (pin) button on every tool window caption,
    /// so tool windows can't slide in and out. DockPanelSuite has no option for this and the VS2005 caption class
    /// is internal, so the button is found through the caption's private m_buttonAutoHide field and kept hidden
    /// whenever the caption shows it again.
    /// </summary>
    public class NoAutoHideCaptionFactory : DockPanelExtender.IDockPaneCaptionFactory
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(NoAutoHideCaptionFactory));

        private const string AutoHideButtonField = "m_buttonAutoHide";

        private readonly DockPanelExtender.IDockPaneCaptionFactory inner;

        public NoAutoHideCaptionFactory(DockPanelExtender.IDockPaneCaptionFactory inner)
        {
            this.inner = inner;
        }

        public DockPaneCaptionBase CreateDockPaneCaption(DockPane pane)
        {
            DockPaneCaptionBase caption = this.inner.CreateDockPaneCaption(pane);
            FieldInfo field = caption.GetType().GetField(AutoHideButtonField, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Log.WarnFormat("{0} has no field {1}, auto-hide button stays visible", caption.GetType().Name, AutoHideButtonField);
                return caption;
            }

            // the button is created lazily and added to the caption's controls
            caption.ControlAdded += delegate (object sender, ControlEventArgs e)
            {
                if (ReferenceEquals(e.Control, field.GetValue(caption)))
                {
                    e.Control.Visible = false;
                    e.Control.VisibleChanged += HideButton;
                }
            };
            return caption;
        }

        private static void HideButton(object sender, EventArgs e)
        {
            Control button = (Control)sender;
            if (button.Visible)
            {
                button.Visible = false;
            }
        }
    }
}
