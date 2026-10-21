using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace SuperPutty.Utils
{
    /// <summary>
    /// Dropped connections of an embedded PuTTY/KiTTY: when the connection ends without PuTTY closing, PuTTY shows
    /// "&lt;app&gt; Fatal Error" (a message box) and, once that is dismissed, sets its title to "&lt;app&gt; (inactive)"
    /// and offers Restart Session (IDM_RESTART) in its system menu. Reconnecting closes the error box, waits for
    /// the inactive state and sends that command.
    /// </summary>
    public static class PuttyConnection
    {
        /// <summary>PuTTY's "Restart Session" system menu command</summary>
        public const uint RestartCommand = 0x0040;

        private const uint WM_SYSCOMMAND = 0x0112;
        private const uint WM_CLOSE = 0x0010;
        private const uint GW_OWNER = 4;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumThreadWindows(uint threadId, EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint command);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        /// <summary>True for PuTTY's title of a closed session ("PuTTY (inactive)", "KiTTY (inactive)")</summary>
        public static bool IsInactiveTitle(string title)
        {
            return title != null && title.TrimEnd().EndsWith("(inactive)", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True for the title of PuTTY's connection error box ("PuTTY Fatal Error")</summary>
        public static bool IsFatalErrorTitle(string title)
        {
            return title != null && title.TrimEnd().EndsWith("Fatal Error", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Wait before the n-th automatic reconnect (0-based): 5 s, 10 s, 20 s, 40 s, then 60 s</summary>
        public static TimeSpan ReconnectDelay(int attempt)
        {
            double seconds = 5 * Math.Pow(2, Math.Max(0, Math.Min(attempt, 10)));
            return TimeSpan.FromSeconds(Math.Min(seconds, 60));
        }

        /// <summary>PuTTY's fatal error boxes belonging to <paramref name="appWindow"/></summary>
        public static List<IntPtr> FindFatalErrorDialogs(IntPtr appWindow)
        {
            List<IntPtr> dialogs = new List<IntPtr>();
            if (appWindow == IntPtr.Zero)
            {
                return dialogs;
            }
            uint thread = GetWindowThreadProcessId(appWindow, IntPtr.Zero);
            if (thread == 0)
            {
                return dialogs;
            }
            EnumThreadWindows(thread, delegate (IntPtr hWnd, IntPtr lParam)
            {
                if (IsWindowVisible(hWnd) && GetWindow(hWnd, GW_OWNER) == appWindow && ClassName(hWnd) == "#32770" && IsFatalErrorTitle(Text(hWnd)))
                {
                    dialogs.Add(hWnd);
                }
                return true;
            }, IntPtr.Zero);
            return dialogs;
        }

        /// <summary>
        /// Closes PuTTY's error box (if any), waits until the session is inactive and restarts it. Blocks for up to
        /// a few seconds, so call it off the UI thread. Returns false if PuTTY never became inactive.
        /// </summary>
        public static bool Restart(IntPtr appWindow, Func<string> title)
        {
            foreach (IntPtr dialog in FindFatalErrorDialogs(appWindow))
            {
                PostMessage(dialog, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
            Stopwatch watch = Stopwatch.StartNew();
            while (!IsInactiveTitle(title()))
            {
                if (watch.ElapsedMilliseconds > 5000)
                {
                    return false;
                }
                Thread.Sleep(100);
            }
            SendMessage(appWindow, WM_SYSCOMMAND, new IntPtr(RestartCommand), IntPtr.Zero);
            return true;
        }

        private static string ClassName(IntPtr hWnd)
        {
            StringBuilder sb = new StringBuilder(64);
            GetClassName(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private static string Text(IntPtr hWnd)
        {
            StringBuilder sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
    }
}
