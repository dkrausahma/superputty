using System;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Data;
using SuperPutty.Scp;

namespace SuperPuttyUnitTests
{
    /// <summary>Transfer Files: mode selection and local sources</summary>
    [TestFixture]
    public class FileTransferJobTests
    {
        private static TransferEndpoint Host(string name, string path, ConnectionProtocol proto = ConnectionProtocol.SSH)
        {
            return new TransferEndpoint { Session = new SessionData { SessionId = name, SessionName = name, Host = name, Proto = proto }, Path = path };
        }

        private static TransferEndpoint Pc(string path)
        {
            return new TransferEndpoint { Path = path };
        }

        [Test]
        public void ChoosesModeFromEndpoints()
        {
            string error;
            ClassicAssert.AreEqual(TransferMode.Upload, FileTransferJob.ModeOf(Pc(@"C:\a.txt"), Host("web", "~"), out error));
            ClassicAssert.AreEqual(TransferMode.Download, FileTransferJob.ModeOf(Host("web", "~/a.txt"), Pc(@"C:\Temp"), out error));
            ClassicAssert.AreEqual(TransferMode.Relay, FileTransferJob.ModeOf(Host("web", "~/a.txt"), Host("db", "/tmp"), out error));
        }

        [Test]
        public void RejectsImpossibleTransfers()
        {
            string error;
            ClassicAssert.IsNull(FileTransferJob.ModeOf(Pc(@"C:\a"), Pc(@"C:\b"), out error));
            ClassicAssert.IsNotNull(error);
            ClassicAssert.IsNull(FileTransferJob.ModeOf(Host("web", ""), Pc(@"C:\b"), out error));
            ClassicAssert.IsNull(FileTransferJob.ModeOf(Host("web", "~/a"), Host("old", "~", ConnectionProtocol.Telnet), out error));
            ClassicAssert.IsNull(FileTransferJob.ModeOf(null, Pc(@"C:\b"), out error));
        }

        [Test]
        public void LocalItemsKeepsExistingFilesAndFolders()
        {
            string folder = Path.Combine(Path.GetTempPath(), "SuperPuTTY.FileTransferJobTests." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "a.txt");
            File.WriteAllText(file, "x");
            try
            {
                var items = FileTransferJob.LocalItems(new[] { file, folder, Path.Combine(folder, "missing.txt"), "  " });

                ClassicAssert.AreEqual(2, items.Count);
                ClassicAssert.AreEqual(FileType.File, items[0].Type);
                ClassicAssert.AreEqual(FileType.Directory, items[1].Type);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
