using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using log4net;

namespace SuperPutty.Utils
{
    /// <summary>
    /// The help: static HTML pages in help\ next to the executable (generated from docs\ by
    /// tools\help-docs\build.py), opened in the default browser. Works offline, always matches the installed version.
    /// </summary>
    public static class HelpFiles
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(HelpFiles));

        public const string Index = "index.html";

        public static string Folder
        {
            get { return Path.Combine(Application.StartupPath, "help"); }
        }

        /// <summary>The local file of a page ("host-info.html"), or null if the help isn't installed</summary>
        public static string PathOf(string page)
        {
            string path = Path.Combine(Folder, string.IsNullOrEmpty(page) ? Index : page);
            return File.Exists(path) ? path : null;
        }

        /// <summary>Open a help page; an unknown page opens the start page</summary>
        public static void Open(string page)
        {
            string path = PathOf(page) ?? PathOf(Index);
            if (path == null)
            {
                MessageBox.Show("The help files were not found in\n" + Folder + "\n\nReinstall SuperPuTTY to restore them.",
                    "Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException)
            {
                Log.Warn("Could not open the help page " + path, ex);
                MessageBox.Show("Could not open the help:\n" + ex.Message, "Help", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>F1 in a form opens the page</summary>
        public static void Attach(Control control, string page)
        {
            control.HelpRequested += (sender, e) =>
            {
                Open(page);
                e.Handled = true;
            };
        }
    }
}
