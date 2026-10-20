using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using log4net;
using SuperPutty.Data;
using SuperPutty.Utils;

namespace SuperPutty.Scp
{
    /// <summary>One side of a transfer: a session's host (remote path) or this PC (<see cref="Session"/> null)</summary>
    public class TransferEndpoint
    {
        public SessionData Session { get; set; }

        /// <summary>Remote path ("~/app", "/var/log/*.log") or local path</summary>
        public string Path { get; set; }

        public bool IsLocal { get { return this.Session == null; } }

        public override string ToString()
        {
            return (this.IsLocal ? "this PC" : this.Session.SessionName) + ":" + this.Path;
        }
    }

    /// <summary>How a transfer is carried out</summary>
    public enum TransferMode
    {
        /// <summary>This PC → host (pscp upload)</summary>
        Upload,
        /// <summary>Host → this PC (pscp download)</summary>
        Download,
        /// <summary>Host → host, relayed through a temporary folder on this PC (download, then upload)</summary>
        Relay
    }

    /// <summary>
    /// Copies files and folders between this PC and session hosts with pscp (recursive). Host-to-host copies go
    /// through a temporary folder on this PC, so the hosts need no keys for each other; only this PC's Pageant
    /// keys or stored passwords are used.
    /// </summary>
    public static class FileTransferJob
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FileTransferJob));

        /// <summary>Temporary folders of host-to-host copies</summary>
        public static string RelayFolder
        {
            get { return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SuperPuTTY", "relay"); }
        }

        /// <summary>The transfer mode, or null with a reason if the combination isn't possible</summary>
        public static TransferMode? ModeOf(TransferEndpoint source, TransferEndpoint target, out string error)
        {
            error = null;
            if (source == null || target == null || string.IsNullOrWhiteSpace(source.Path) || string.IsNullOrWhiteSpace(target.Path))
            {
                error = "Source and target need a path.";
                return null;
            }
            if (source.IsLocal && target.IsLocal)
            {
                error = "Source and target are both this PC; use Explorer for that.";
                return null;
            }
            if (!source.IsLocal && !RemoteCommandTab.IsSupported(source.Session) || !target.IsLocal && !RemoteCommandTab.IsSupported(target.Session))
            {
                error = "File transfers need SSH sessions.";
                return null;
            }
            if (source.IsLocal)
            {
                return TransferMode.Upload;
            }
            return target.IsLocal ? TransferMode.Download : TransferMode.Relay;
        }

        /// <summary>
        /// Run the transfer synchronously (call it off the UI thread). Local sources may list several paths
        /// separated by '|'. Progress texts go to <paramref name="progress"/>.
        /// </summary>
        public static FileTransferResult Run(TransferEndpoint source, TransferEndpoint target, Action<string> progress)
        {
            string error;
            TransferMode? mode = ModeOf(source, target, out error);
            FileTransferResult result = new FileTransferResult();
            if (mode == null)
            {
                result.SetError(error, null);
                return result;
            }

            switch (mode.Value)
            {
                case TransferMode.Upload:
                    return Upload(LocalItems(source.Path.Split('|')), target, progress);
                case TransferMode.Download:
                    Directory.CreateDirectory(target.Path);
                    return Download(source, target.Path, progress);
                default:
                    return Relay(source, target, progress);
            }
        }

        private static FileTransferResult Relay(TransferEndpoint source, TransferEndpoint target, Action<string> progress)
        {
            string folder = System.IO.Path.Combine(RelayFolder, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                progress("Copying from " + source.Session.SessionName + " to this PC…");
                FileTransferResult result = Download(source, folder, progress);
                if (result.StatusCode != ResultStatusCode.Success)
                {
                    return result;
                }

                List<BrowserFileInfo> items = LocalItems(Directory.GetFileSystemEntries(folder));
                if (items.Count == 0)
                {
                    result.SetError("Nothing was copied from " + source, null);
                    return result;
                }
                progress("Copying from this PC to " + target.Session.SessionName + "…");
                return Upload(items, target, progress);
            }
            finally
            {
                TryDeleteFolder(folder);
            }
        }

        private static FileTransferResult Download(TransferEndpoint source, string localFolder, Action<string> progress)
        {
            PscpClient client = new PscpClient(new PscpOptions { PscpLocation = SuperPuTTY.Settings.PscpExe }, source.Session);
            List<BrowserFileInfo> remote = new List<BrowserFileInfo>
            {
                new BrowserFileInfo { Path = RemotePath.ToPscpPath(source.Path), Source = SourceType.Remote }
            };
            BrowserFileInfo target = new BrowserFileInfo { Path = localFolder, Source = SourceType.Local, Type = FileType.Directory };
            return client.CopyFiles(remote, target, Report(source.Session.SessionName, progress));
        }

        private static FileTransferResult Upload(List<BrowserFileInfo> items, TransferEndpoint target, Action<string> progress)
        {
            FileTransferResult result = new FileTransferResult();
            if (items.Count == 0)
            {
                result.SetError("No existing local files or folders to copy.", null);
                return result;
            }
            PscpClient client = new PscpClient(new PscpOptions { PscpLocation = SuperPuTTY.Settings.PscpExe }, target.Session);
            BrowserFileInfo folder = new BrowserFileInfo { Path = RemotePath.ToPscpPath(target.Path), Source = SourceType.Remote, Type = FileType.Directory };
            return client.CopyFiles(items, folder, Report(target.Session.SessionName, progress));
        }

        private static TransferUpdateCallback Report(string host, Action<string> progress)
        {
            return (complete, cancelAll, status) => progress(string.Format("{0}: {1} {2}%", host, status.Filename, status.PercentComplete));
        }

        /// <summary>Existing local files and folders as pscp sources</summary>
        public static List<BrowserFileInfo> LocalItems(IEnumerable<string> paths)
        {
            List<BrowserFileInfo> items = new List<BrowserFileInfo>();
            foreach (string path in paths.Select(p => p.Trim()).Where(p => p.Length > 0))
            {
                if (Directory.Exists(path)) { items.Add(new BrowserFileInfo(new DirectoryInfo(path))); }
                else if (File.Exists(path)) { items.Add(new BrowserFileInfo(new FileInfo(path))); }
            }
            return items;
        }

        private static void TryDeleteFolder(string folder)
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.WarnFormat("Could not delete relay folder {0}: {1}", folder, ex.Message);
            }
        }

        /// <summary>Remove relay folders left behind, e.g. after a crash; called at start-up</summary>
        public static void CleanUp()
        {
            try
            {
                if (Directory.Exists(RelayFolder))
                {
                    foreach (string folder in Directory.GetDirectories(RelayFolder))
                    {
                        if (Directory.GetLastWriteTimeUtc(folder) < DateTime.UtcNow.AddHours(-1))
                        {
                            TryDeleteFolder(folder);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.Warn("Could not clean up relay folders", ex);
            }
        }
    }
}
