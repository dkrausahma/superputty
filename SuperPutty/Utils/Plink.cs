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
            return BuildArguments(session, remoteCommand, ReadProfileHost(session.PuttySession));
        }

        /// <summary>
        /// BuildArguments with the host name stored in the session's PuTTY profile (null/empty if none). Unlike
        /// PuTTY, plink treats the first plain argument after -load of a profile with a host name as the start of
        /// the remote command, so the host must not be passed again: the host itself would run as a command
        /// ("bash: 10.0.0.5: command not found"). If the profile points to another host than the session, the
        /// profile can't be loaded without connecting there, so it is left out.
        /// </summary>
        public static List<string> BuildArguments(SessionData session, string remoteCommand, string profileHost)
        {
            List<string> args = new List<string> { "-ssh", "-batch", "-T", "-P", session.Port.ToString() };
            bool hostFromProfile = false;
            if (!string.IsNullOrEmpty(session.PuttySession))
            {
                if (string.IsNullOrEmpty(profileHost))
                {
                    args.Add("-load");
                    args.Add(session.PuttySession);
                }
                else if (string.Equals(profileHost.Trim(), session.Host, StringComparison.OrdinalIgnoreCase))
                {
                    args.Add("-load");
                    args.Add(session.PuttySession);
                    hostFromProfile = true;
                }
                else
                {
                    Log.WarnFormat("plink: profile '{0}' connects to {1}, session {2} to {3}; not loading the profile",
                        session.PuttySession, profileHost, session.SessionId, session.Host);
                }
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
                // a short-lived file instead of -pw, so the password doesn't show in the process list
                if (PasswordFile.IsSupported(SuperPuTTY.Settings.PuttyExe))
                {
                    args.Add("-pwfile");
                    args.Add(PasswordFile.Create(session.Password));
                }
                else
                {
                    args.Add("-pw");
                    args.Add(session.Password);
                }
            }
            if (!hostFromProfile)
            {
                args.Add(session.Host);
            }
            args.Add(remoteCommand);
            return args;
        }

        /// <summary>The HostName stored in a saved PuTTY/KiTTY profile; null if there is none or it can't be read</summary>
        private static string ReadProfileHost(string profile)
        {
            if (string.IsNullOrEmpty(profile))
            {
                return null;
            }
            try
            {
                return PuttyDataHelper.ReadProfileValue(profile, "HostName");
            }
            catch (Exception ex) when (ex is System.Security.SecurityException || ex is UnauthorizedAccessException || ex is IOException)
            {
                Log.Warn("Could not read the host name of profile " + profile, ex);
                return null;
            }
        }

        private static ProcessStartInfo CreateStartInfo(string exe, SessionData session, string remoteCommand)
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
            Log.InfoFormat("plink: session={0}, command=[{1}]", session.SessionId,
                remoteCommand.Length > 120 ? remoteCommand.Substring(0, 120) + "…" : remoteCommand);
            return startInfo;
        }

        /// <summary>
        /// Start a long-running command on the session's host (e.g. a loop printing values) and return the
        /// process; the caller reads its output and kills it when done. Standard input is closed already.
        /// </summary>
        public static Process Start(SessionData session, string remoteCommand)
        {
            string exe = FindExecutable();
            if (exe == null)
            {
                throw new FileNotFoundException("plink.exe not found next to PuTTY or on the PATH");
            }
            Process process = Process.Start(CreateStartInfo(exe, session, remoteCommand));
            process.StandardInput.Close();
            return process;
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
                using (Process process = Process.Start(CreateStartInfo(exe, session, remoteCommand)))
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
