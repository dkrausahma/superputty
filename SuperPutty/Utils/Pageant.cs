using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SuperPutty.Utils
{
    /// <summary>What the status bar shows about Pageant</summary>
    public struct PageantState
    {
        public bool Running;

        /// <summary>Number of loaded keys; null if Pageant runs but did not answer</summary>
        public int? KeyCount;

        public static readonly PageantState NotRunning = new PageantState();
    }

    /// <summary>
    /// Asks a running Pageant how many keys it holds, the way PuTTY talks to it: the request goes into a named
    /// shared memory block whose name is sent with WM_COPYDATA, and Pageant writes its answer into the same block.
    /// Only SSH2_AGENTC_REQUEST_IDENTITIES is sent - no key material is read.
    /// </summary>
    public static class Pageant
    {
        private const uint AgentCopyDataId = 0x804e50ba;
        private const int AgentMaxMessageLength = 8192;
        private const byte SSH2_AGENTC_REQUEST_IDENTITIES = 11;
        private const byte SSH2_AGENT_IDENTITIES_ANSWER = 12;

        private const int WM_COPYDATA = 0x004A;
        private const uint PAGE_READWRITE = 0x04;
        private const uint FILE_MAP_WRITE = 0x0002;
        private const uint SMTO_ABORTIFHUNG = 0x0002;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        private struct COPYDATASTRUCT
        {
            public UIntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam, ref COPYDATASTRUCT lParam,
            uint flags, uint timeout, out IntPtr result);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFileMapping(IntPtr hFile, IntPtr attributes, uint protect, uint maximumSizeHigh,
            uint maximumSizeLow, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFile(IntPtr mapping, uint desiredAccess, uint offsetHigh, uint offsetLow, UIntPtr bytes);

        [DllImport("kernel32.dll")]
        private static extern bool UnmapViewOfFile(IntPtr address);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        public static bool IsRunning()
        {
            return FindWindow("Pageant", "Pageant") != IntPtr.Zero;
        }

        /// <summary>Whether Pageant runs and how many keys it holds; blocks up to <paramref name="timeoutMs"/></summary>
        public static PageantState Query(uint timeoutMs = 2000)
        {
            IntPtr window = FindWindow("Pageant", "Pageant");
            if (window == IntPtr.Zero)
            {
                return PageantState.NotRunning;
            }
            PageantState state = new PageantState { Running = true };

            string mapName = "PageantRequest" + GetCurrentThreadId().ToString("x8");
            IntPtr mapping = CreateFileMapping(INVALID_HANDLE_VALUE, IntPtr.Zero, PAGE_READWRITE, 0, AgentMaxMessageLength, mapName);
            if (mapping == IntPtr.Zero)
            {
                return state;
            }
            IntPtr view = IntPtr.Zero;
            GCHandle name = default(GCHandle);
            try
            {
                view = MapViewOfFile(mapping, FILE_MAP_WRITE, 0, 0, UIntPtr.Zero);
                if (view == IntPtr.Zero)
                {
                    return state;
                }
                byte[] request = BuildIdentitiesRequest();
                Marshal.Copy(request, 0, view, request.Length);

                byte[] nameBytes = Encoding.ASCII.GetBytes(mapName + "\0");
                name = GCHandle.Alloc(nameBytes, GCHandleType.Pinned);
                COPYDATASTRUCT data = new COPYDATASTRUCT
                {
                    dwData = new UIntPtr(AgentCopyDataId),
                    cbData = nameBytes.Length,
                    lpData = name.AddrOfPinnedObject()
                };
                IntPtr result;
                if (SendMessageTimeout(window, WM_COPYDATA, IntPtr.Zero, ref data, SMTO_ABORTIFHUNG, timeoutMs, out result) == IntPtr.Zero
                    || result == IntPtr.Zero)
                {
                    return state;
                }

                byte[] response = new byte[AgentMaxMessageLength];
                Marshal.Copy(view, response, 0, response.Length);
                state.KeyCount = ParseIdentitiesAnswer(response);
                return state;
            }
            finally
            {
                if (name.IsAllocated)
                {
                    name.Free();
                }
                if (view != IntPtr.Zero)
                {
                    UnmapViewOfFile(view);
                }
                CloseHandle(mapping);
            }
        }

        /// <summary>uint32 length (big endian) followed by the message type</summary>
        public static byte[] BuildIdentitiesRequest()
        {
            return new byte[] { 0, 0, 0, 1, SSH2_AGENTC_REQUEST_IDENTITIES };
        }

        /// <summary>The key count of an SSH2_AGENT_IDENTITIES_ANSWER (length, type 12, uint32 count …), else null</summary>
        public static int? ParseIdentitiesAnswer(byte[] response)
        {
            if (response == null || response.Length < 9)
            {
                return null;
            }
            long length = ReadUInt32(response, 0);
            if (length < 5 || length > response.Length - 4 || response[4] != SSH2_AGENT_IDENTITIES_ANSWER)
            {
                return null;
            }
            long count = ReadUInt32(response, 5);
            return count > int.MaxValue ? (int?)null : (int)count;
        }

        private static long ReadUInt32(byte[] data, int offset)
        {
            return ((long)data[offset] << 24) | ((long)data[offset + 1] << 16) | ((long)data[offset + 2] << 8) | data[offset + 3];
        }

        /// <summary>Status bar text</summary>
        public static string Describe(PageantState state)
        {
            if (!state.Running)
            {
                return "Pageant not running";
            }
            if (state.KeyCount == null)
            {
                return "Pageant running";
            }
            return state.KeyCount == 0 ? "Pageant: no keys" : state.KeyCount == 1 ? "Pageant: 1 key" : "Pageant: " + state.KeyCount + " keys";
        }
    }
}
