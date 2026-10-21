using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;

namespace SuperPutty.Utils
{
    /// <summary>The listening TCP ports of this machine and the processes that own them (IPv4 and IPv6)</summary>
    public static class TcpListeners
    {
        private const int AF_INET = 2;
        private const int AF_INET6 = 23;
        private const int TCP_TABLE_OWNER_PID_LISTENER = 3;

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int pdwSize, bool bOrder, int ulAf, int tableClass, uint reserved);

        /// <summary>Listening port → IDs of the processes listening on it</summary>
        public static Dictionary<int, HashSet<int>> Snapshot()
        {
            Dictionary<int, HashSet<int>> listeners = new Dictionary<int, HashSet<int>>();
            // MIB_TCPROW_OWNER_PID: state, local addr, local port, remote addr, remote port, pid (6 DWORDs)
            Read(AF_INET, 24, 8, 20, listeners);
            // MIB_TCP6ROW_OWNER_PID: local addr(16), scope(4), local port(4), remote addr(16), scope(4), remote port(4), state(4), pid(4)
            Read(AF_INET6, 56, 20, 52, listeners);
            return listeners;
        }

        private static void Read(int family, int rowSize, int portOffset, int pidOffset, Dictionary<int, HashSet<int>> listeners)
        {
            int size = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref size, false, family, TCP_TABLE_OWNER_PID_LISTENER, 0);
            if (size <= 0)
            {
                return;
            }
            IntPtr table = Marshal.AllocHGlobal(size);
            try
            {
                if (GetExtendedTcpTable(table, ref size, false, family, TCP_TABLE_OWNER_PID_LISTENER, 0) != 0)
                {
                    return;
                }
                int rows = Marshal.ReadInt32(table);
                for (int i = 0; i < rows; i++)
                {
                    IntPtr row = IntPtr.Add(table, 4 + i * rowSize);
                    // the port is stored in network byte order in the low 16 bits
                    int port = (ushort)IPAddress.NetworkToHostOrder((short)(Marshal.ReadInt32(row, portOffset) & 0xFFFF));
                    int pid = Marshal.ReadInt32(row, pidOffset);
                    HashSet<int> pids;
                    if (!listeners.TryGetValue(port, out pids))
                    {
                        listeners[port] = pids = new HashSet<int>();
                    }
                    pids.Add(pid);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(table);
            }
        }
    }
}
