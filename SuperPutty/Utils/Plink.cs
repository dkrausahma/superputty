using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using SuperPutty.Data;

namespace SuperPutty.Utils
{
    /// <summary>Output of a command run with plink</summary>
    public class PlinkResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public bool TimedOut { get; set; }

        public bool Success { get { return !this.TimedOut && this.ExitCode == 0; } }
    }

    /// <summary>
    /// Runs commands on a session's host in the background with plink, PuTTY's command-line client (klink for
    /// KiTTY). It uses the same profile, host keys, key files and Pageant as the terminal, so no extra login is
    /// needed; -batch makes it fail instead of prompting (e.g. for a password).
    /// </summary>
    public static class Plink
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Plink));

        /// <summary>plink.exe next to putty.exe (klink.exe next to KiTTY), else on the PATH; null if not found</summary>
        public static string FindExecutable()
        {
            string putty = SuperPuTTY.Settings.PuttyExe;
            if (!string.IsNullOrEmpty(putty) && File.Exists(putty))
            {
                string dir = Path.GetDirectoryName(putty);
                foreach (string name in new[] { SuperPuTTY.IsKiTTY ? "klink.exe" : "plink.exe", "plink.exe" })
                {
                    string candidate = Path.Combine(dir, name);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
            {
                try
                {
                    string candidate = Path.Combine(dir.Trim(), "plink.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (ArgumentException)
                {
                    // invalid PATH entry
                }
            }
            return null;
        }

        /// <summary>
        /// The plink arguments for running a command on the session's host: connection settings from the session
        /// (profile, port, user, a key file given with -i in the extra arguments), no terminal, no prompts
        /// </summary>
        public static List<string> BuildArguments(SessionData session, string remoteCommand)
        {
            List<string> args = new List<string> { "-ssh", "-batch", "-T", "-P", session.Port.ToString() };
            if (!string.IsNullOrEmpty(session.PuttySession))
            {
                args.Add("-load");
                args.Add(session.PuttySession);
            }
            if (!string.IsNullOrEmpty(session.Username))
            {
                args.Add("-l");
                args.Add(session.Username);
            }
            string keyFile = CommandLineOptions.getcommand(session.ExtraArgs, "-i");
            if (!string.IsNullOrEmpty(keyFile))
            {
                args.Add("-i");
                args.Add(keyFile);
            }
            if (!string.IsNullOrEmpty(session.Password) && SuperPuTTY.Settings.AllowPlainTextPuttyPasswordArg)
            {
                args.Add("-pw");
                args.Add(session.Password);
            }
            args.Add(session.Host);
            args.Add(remoteCommand);
            return args;
        }

        /// <summary>Run a command on the session's host and collect its output</summary>
        public static Task<PlinkResult> RunAsync(SessionData session, string remoteCommand, TimeSpan timeout)
        {
            string exe = FindExecutable();
            if (exe == null)
            {
                throw new FileNotFoundException("plink.exe not found next to PuTTY or on the PATH");
            }

            return Task.Run(() =>
            {
                // redirected output needs UseShellExecute = false
                ProcessStartInfo startInfo = new ProcessStartInfo(exe)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true
                };
                foreach (string arg in BuildArguments(session, remoteCommand))
                {
                    startInfo.ArgumentList.Add(arg);
                }

                Log.InfoFormat("plink: session={0}, command=[{1}]", session.SessionId, remoteCommand);
                using (Process process = Process.Start(startInfo))
                {
                    process.StandardInput.Close();
                    Task<string> output = process.StandardOutput.ReadToEndAsync();
                    Task<string> error = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit((int)timeout.TotalMilliseconds))
                    {
                        try { process.Kill(true); } catch (InvalidOperationException) { }
                        return new PlinkResult { TimedOut = true, ExitCode = -1, Output = string.Empty, Error = "Timed out after " + timeout.TotalSeconds + " s" };
                    }
                    process.WaitForExit();
                    return new PlinkResult { ExitCode = process.ExitCode, Output = output.Result, Error = error.Result };
                }
            });
        }
    }
}
