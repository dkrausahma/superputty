using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SuperPutty.Utils
{
    /// <summary>
    /// Windows DPAPI (CryptProtectData) for secrets in Sessions.XML: the result can only be decrypted by the same
    /// Windows user (on the same machine, or with a roaming profile). Called directly in crypt32, so no extra
    /// package is needed.
    /// </summary>
    public static class Dpapi
    {
        /// <summary>Marks protected values, so plain values from older files can be told apart</summary>
        public const string Prefix = "dpapi:";

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DataBlob
        {
            public int cbData;
            public IntPtr pbData;
        }

        private const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(ref DataBlob pDataIn, string szDataDescr, IntPtr pOptionalEntropy,
            IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DataBlob pDataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(ref DataBlob pDataIn, IntPtr ppszDataDescr, IntPtr pOptionalEntropy,
            IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DataBlob pDataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr hMem);

        /// <summary>"dpapi:&lt;base64&gt;" for a secret, null for an empty one</summary>
        public static string Protect(string secret)
        {
            if (string.IsNullOrEmpty(secret))
            {
                return null;
            }
            return Prefix + Convert.ToBase64String(Transform(Encoding.UTF8.GetBytes(secret), true));
        }

        /// <summary>
        /// The secret from a value written by <see cref="Protect"/>; a value without the prefix is returned
        /// unchanged (plain text from older files). Throws <see cref="System.Security.Cryptography.CryptographicException"/>
        /// if it can't be decrypted, e.g. for another Windows user.
        /// </summary>
        public static string Unprotect(string value)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return value;
            }
            return Encoding.UTF8.GetString(Transform(Convert.FromBase64String(value.Substring(Prefix.Length)), false));
        }

        private static byte[] Transform(byte[] data, bool protect)
        {
            DataBlob input = new DataBlob { cbData = data.Length, pbData = Marshal.AllocHGlobal(data.Length) };
            DataBlob output = new DataBlob();
            try
            {
                Marshal.Copy(data, 0, input.pbData, data.Length);
                bool ok = protect
                    ? CryptProtectData(ref input, "SuperPuTTY", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, ref output)
                    : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, ref output);
                if (!ok)
                {
                    throw new System.Security.Cryptography.CryptographicException(Marshal.GetLastWin32Error());
                }
                byte[] result = new byte[output.cbData];
                Marshal.Copy(output.pbData, result, 0, output.cbData);
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(input.pbData);
                if (output.pbData != IntPtr.Zero)
                {
                    LocalFree(output.pbData);
                }
            }
        }
    }
}
