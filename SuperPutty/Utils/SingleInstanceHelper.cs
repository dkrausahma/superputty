using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using SuperPutty.Data;
using log4net;

namespace SuperPutty.Utils
{
    /// <summary>
    /// Forwards the command line of a second SuperPuTTY instance to the running one over a named pipe
    /// that only the current user (in the current Windows session) can connect to.
    /// </summary>
    public static class SingleInstanceHelper
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SingleInstanceHelper));

        private const int ConnectTimeoutMs = 5000;

        /// <summary>Default pipe name, unique per Windows session and user</summary>
        public static readonly string PipeName = string.Format(
            "SuperPuTTY.SingleInstance.{0}.{1}", Process.GetCurrentProcess().SessionId, Environment.UserName);

        /// <summary>Start listening for command lines sent by other instances</summary>
        public static void RegisterRemotingService()
        {
            StartListener(PipeName, Run);
        }

        /// <summary>Send the command line to the running instance</summary>
        public static void LaunchInExistingInstance(string[] args)
        {
            try
            {
                Send(PipeName, args);
            }
            catch (Exception ex)
            {
                Log.Error("Unable to send command to the running instance", ex);
            }
        }

        /// <summary>
        /// Starts a background thread that accepts connections on <paramref name="pipeName"/> and passes
        /// each received argument list to <paramref name="onArgs"/> (on that background thread).
        /// </summary>
        public static Thread StartListener(string pipeName, Action<string[]> onArgs)
        {
            Thread listener = new Thread(() => Listen(pipeName, onArgs))
            {
                IsBackground = true,
                Name = "SingleInstanceListener"
            };
            listener.Start();
            Log.InfoFormat("Listening for single instance commands: [\\\\.\\pipe\\{0}]", pipeName);
            return listener;
        }

        /// <summary>Sends <paramref name="args"/> to the listener on <paramref name="pipeName"/></summary>
        public static void Send(string pipeName, string[] args)
        {
            using (NamedPipeClientStream client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly))
            {
                client.Connect(ConnectTimeoutMs);
                using (BinaryWriter writer = new BinaryWriter(client, Encoding.UTF8))
                {
                    writer.Write(args.Length);
                    foreach (string arg in args)
                    {
                        writer.Write(arg);
                    }
                }
            }
        }

        private static void Listen(string pipeName, Action<string[]> onArgs)
        {
            while (true)
            {
                NamedPipeServerStream server;
                try
                {
                    server = new NamedPipeServerStream(
                        pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.CurrentUserOnly);
                }
                catch (Exception ex)
                {
                    // e.g. pipe name already in use by another instance
                    Log.Warn("Unable to listen for single instance commands...feature unavailable", ex);
                    return;
                }

                try
                {
                    using (server)
                    {
                        server.WaitForConnection();

                        string[] args;
                        using (BinaryReader reader = new BinaryReader(server, Encoding.UTF8))
                        {
                            int count = reader.ReadInt32();
                            args = new string[count];
                            for (int i = 0; i < count; i++)
                            {
                                args[i] = reader.ReadString();
                            }
                        }
                        onArgs(args);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Error handling single instance command", ex);
                }
            }
        }

        private static void Run(String[] args)
        {
            Log.InfoFormat("Received remote Run command: [{0}]", String.Join(" ", args));
            CommandLineOptions cmd = new CommandLineOptions(args);
            SessionDataStartInfo ssi = cmd.ToSessionStartInfo();
            SuperPuTTY.OpenSession(ssi);
        }
    }
}
