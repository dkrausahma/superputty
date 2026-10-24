using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace SuperPutty.Utils
{
    /// <summary>What a key press becomes for the other terminals: characters (WM_CHAR) or a key (WM_KEYDOWN/UP)</summary>
    public struct Keystroke
    {
        /// <summary>Characters to send, or null for a key</summary>
        public string Chars;

        public Keys Key;
        public uint ScanCode;
        public bool Extended;
    }

    /// <summary>
    /// Live input to several terminals: a key typed into one PuTTY is repeated in the others. Characters are
    /// translated with the user's keyboard layout and sent as WM_CHAR (so shift, AltGr and Caps Lock are right even
    /// though the other PuTTYs don't share the keyboard state); keys like Enter, arrows or F-keys go as key messages
    /// so PuTTY applies its own key settings. Ctrl+letter becomes the control character, Alt+key ESC + key.
    /// </summary>
    public static class LiveInput
    {
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint WM_CHAR = 0x0102;
        private const uint ToUnicodeKeepState = 0x4;

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ToUnicodeEx(uint virtualKey, uint scanCode, byte[] keyState, StringBuilder buffer, int bufferSize, uint flags, IntPtr layout);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint threadId);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int virtualKey);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        [DllImport("user32.dll")]
        private static extern bool IsChild(IntPtr parent, IntPtr child);

        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint threadId, ref GUITHREADINFO info);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        /// <summary>Keys sent as key messages (PuTTY turns them into escape sequences by its own settings)</summary>
        public static bool IsTerminalKey(Keys key)
        {
            switch (key)
            {
                case Keys.Enter: case Keys.Back: case Keys.Tab: case Keys.Escape:
                case Keys.Up: case Keys.Down: case Keys.Left: case Keys.Right:
                case Keys.Home: case Keys.End: case Keys.PageUp: case Keys.PageDown:
                case Keys.Insert: case Keys.Delete:
                    return true;
                default:
                    return key >= Keys.F1 && key <= Keys.F12;
            }
        }

        public static bool IsModifier(Keys key)
        {
            switch (key)
            {
                case Keys.ShiftKey: case Keys.LShiftKey: case Keys.RShiftKey:
                case Keys.ControlKey: case Keys.LControlKey: case Keys.RControlKey:
                case Keys.Menu: case Keys.LMenu: case Keys.RMenu:
                case Keys.LWin: case Keys.RWin: case Keys.CapsLock: case Keys.NumLock: case Keys.Scroll:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The control character of Ctrl+letter (Ctrl+C = 0x03), or null</summary>
        public static char? ControlCharacter(Keys key)
        {
            return key >= Keys.A && key <= Keys.Z ? (char)(key - Keys.A + 1) : (char?)null;
        }

        /// <summary>
        /// What to repeat in the other terminals for a key press, or null if nothing (modifiers alone, dead keys,
        /// shortcuts with Ctrl/Alt on terminal keys - Ctrl+Tab switches tabs and must not reach the others).
        /// </summary>
        /// <param name="toChars">turns a key into characters with the given shift/AltGr state (null if none)</param>
        public static Keystroke? Translate(Keys key, uint scanCode, bool extended, bool shift, bool control, bool alt,
            Func<Keys, uint, bool, bool, string> toChars)
        {
            if (IsModifier(key))
            {
                return null;
            }
            bool altGr = control && alt;
            if (IsTerminalKey(key))
            {
                if (control || alt || (shift && key != Keys.Enter))
                {
                    return null;
                }
                return new Keystroke { Key = key, ScanCode = scanCode, Extended = extended };
            }
            if (control && !altGr)
            {
                char? c = shift ? null : ControlCharacter(key);
                return c.HasValue ? new Keystroke { Chars = c.Value.ToString() } : (Keystroke?)null;
            }
            string chars = toChars(key, scanCode, shift, altGr);
            if (string.IsNullOrEmpty(chars))
            {
                return null;
            }
            return new Keystroke { Chars = alt && !altGr ? "\u001b" + chars : chars };
        }

        /// <summary>Characters of a key with the keyboard layout of <paramref name="layoutWindow"/>'s thread</summary>
        public static string ToChars(Keys key, uint scanCode, bool shift, bool altGr, IntPtr layoutWindow)
        {
            byte[] state = new byte[256];
            if (shift)
            {
                state[(int)Keys.ShiftKey] = 0x80;
            }
            if (altGr)
            {
                state[(int)Keys.ControlKey] = 0x80;
                state[(int)Keys.Menu] = 0x80;
            }
            state[(int)Keys.CapsLock] = (byte)(GetKeyState((int)Keys.CapsLock) & 0x01);
            IntPtr layout = GetKeyboardLayout(GetWindowThreadProcessId(layoutWindow, IntPtr.Zero));
            StringBuilder buffer = new StringBuilder(8);
            int count = ToUnicodeEx((uint)key, scanCode, state, buffer, buffer.Capacity, ToUnicodeKeepState, layout);
            return count > 0 ? buffer.ToString(0, count) : null; // < 0: dead key
        }

        /// <summary>Sends the keystroke to a terminal window (posted, so the order of keys is kept)</summary>
        public static void Send(IntPtr window, Keystroke keystroke)
        {
            if (keystroke.Chars != null)
            {
                foreach (char c in keystroke.Chars)
                {
                    PostMessage(window, WM_CHAR, new IntPtr(c), new IntPtr(1));
                }
                return;
            }
            long lParam = 1 | ((long)(keystroke.ScanCode & 0xff) << 16) | (keystroke.Extended ? 1L << 24 : 0);
            PostMessage(window, WM_KEYDOWN, new IntPtr((int)keystroke.Key), new IntPtr(lParam));
            PostMessage(window, WM_KEYUP, new IntPtr((int)keystroke.Key), new IntPtr(lParam | (3L << 30)));
        }

        /// <summary>True if the keyboard focus is in <paramref name="appWindow"/> (the embedded PuTTY)</summary>
        public static bool HasFocus(IntPtr appWindow)
        {
            if (appWindow == IntPtr.Zero)
            {
                return false;
            }
            GUITHREADINFO info = new GUITHREADINFO { cbSize = Marshal.SizeOf(typeof(GUITHREADINFO)) };
            if (!GetGUIThreadInfo(0, ref info))
            {
                return false;
            }
            return info.hwndFocus == appWindow || IsChild(appWindow, info.hwndFocus);
        }
    }
}
