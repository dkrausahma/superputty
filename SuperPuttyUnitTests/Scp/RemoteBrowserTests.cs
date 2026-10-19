using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Scp;
using System.Windows.Forms;
using System.Drawing;
using SuperPutty.Data;

namespace SuperPuttyUnitTests.Scp
{
    [TestFixture]
    public class RemoteBrowserTests
    {
        static RemoteBrowserTests()
        {
            Program.InitLoggingForUnitTests();
        }

        [Test]
        public void ParserTimestampYearMode()
        {
            // May 15 12:32, Mar  3 03:37, Nov 18 18:19, May 27  2012
            PscpClient.ScpLineParser parser = new PscpClient.ScpLineParser();
            DateTime dt;

            ClassicAssert.True(parser.TryParseTimestamp("May 15 2012", out dt));
            ClassicAssert.AreEqual(2012, dt.Year);
            ClassicAssert.AreEqual(5, dt.Month);
            ClassicAssert.AreEqual(15, dt.Day);
            ClassicAssert.AreEqual(0, dt.Hour);
            ClassicAssert.AreEqual(0, dt.Minute);
            ClassicAssert.AreEqual(DateTimeKind.Local, dt.Kind);

            ClassicAssert.True(parser.TryParseTimestamp("May  5 2012", out dt));
            ClassicAssert.AreEqual(2012, dt.Year);
            ClassicAssert.AreEqual(5, dt.Month);
            ClassicAssert.AreEqual(5, dt.Day);
            ClassicAssert.AreEqual(0, dt.Hour);
            ClassicAssert.AreEqual(0, dt.Minute);
            ClassicAssert.AreEqual(DateTimeKind.Local, dt.Kind);
        }

        [Test]
        public void ParserTimestampTimeMode()
        {
            PscpClient.ScpLineParser parser = new PscpClient.ScpLineParser();
            DateTime dt;

            ClassicAssert.True(parser.TryParseTimestamp("May 15 12:32", out dt));
            ClassicAssert.AreEqual(DateTime.Now.Year, dt.Year);
            ClassicAssert.AreEqual(5, dt.Month);
            ClassicAssert.AreEqual(15, dt.Day);
            ClassicAssert.AreEqual(12, dt.Hour);
            ClassicAssert.AreEqual(32, dt.Minute);
            ClassicAssert.AreEqual(DateTimeKind.Local, dt.Kind);

            ClassicAssert.True(parser.TryParseTimestamp("Mar  3 03:37", out dt));
            ClassicAssert.AreEqual(DateTime.Now.Year, dt.Year);
            ClassicAssert.AreEqual(3, dt.Month);
            ClassicAssert.AreEqual(3, dt.Day);
            ClassicAssert.AreEqual(3, dt.Hour);
            ClassicAssert.AreEqual(37, dt.Minute);
            ClassicAssert.AreEqual(DateTimeKind.Local, dt.Kind);

            ClassicAssert.True(parser.TryParseTimestamp("Nov 18 18:19", out dt));
            ClassicAssert.AreEqual(DateTime.Now.Year, dt.Year);
            ClassicAssert.AreEqual(11, dt.Month);
            ClassicAssert.AreEqual(18, dt.Day);
            ClassicAssert.AreEqual(18, dt.Hour);
            ClassicAssert.AreEqual(19, dt.Minute);
            ClassicAssert.AreEqual(DateTimeKind.Local, dt.Kind);
        }

    }

    public class RemoteBrowserViewTester 
    {
        [TestView]
        public void TestGUI()
        {
            Form form = new Form();
            form.Size = new Size(600, 800);

            SessionData session = new SessionData
            {
                Username = ScpConfig.UserName,
                Password = ScpConfig.Password, 
                Host = ScpConfig.KnownHost, 
                Port = 22
            };

            BrowserPresenter presenter = new BrowserPresenter(
                "Remote",
                new RemoteBrowserModel(ScpConfig.DefaultOptions), 
                session, 
                new MockFileTransferPresenter());

            BrowserView view = new BrowserView(
                presenter,
                RemoteBrowserModel.NewDirectory("/home/" + ScpConfig.UserName));
            view.Dock = DockStyle.Fill;

            form.Controls.Add(view);
            form.ShowDialog();
        }

    }
}
