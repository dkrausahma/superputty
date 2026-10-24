using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Utils;

namespace SuperPuttyUnitTests
{
    /// <summary>Dropped PuTTY connections: recognising PuTTY's titles, automatic reconnect delays</summary>
    [TestFixture]
    public class ReconnectTests
    {
        [Test]
        public void InactiveTitle()
        {
            ClassicAssert.IsTrue(PuttyConnection.IsInactiveTitle("PuTTY (inactive)"));
            ClassicAssert.IsTrue(PuttyConnection.IsInactiveTitle("KiTTY (inactive) "));
            ClassicAssert.IsFalse(PuttyConnection.IsInactiveTitle("root@db01: ~"));
            ClassicAssert.IsFalse(PuttyConnection.IsInactiveTitle(null));
        }

        [Test]
        public void FatalErrorTitle()
        {
            ClassicAssert.IsTrue(PuttyConnection.IsFatalErrorTitle("PuTTY Fatal Error"));
            ClassicAssert.IsFalse(PuttyConnection.IsFatalErrorTitle("PuTTY Security Alert"), "the host key dialog is never closed");
            ClassicAssert.IsFalse(PuttyConnection.IsFatalErrorTitle(null));
        }

        [Test]
        public void DelaysDoubleUpToOneMinute()
        {
            ClassicAssert.AreEqual(TimeSpan.FromSeconds(5), PuttyConnection.ReconnectDelay(0));
            ClassicAssert.AreEqual(TimeSpan.FromSeconds(10), PuttyConnection.ReconnectDelay(1));
            ClassicAssert.AreEqual(TimeSpan.FromSeconds(40), PuttyConnection.ReconnectDelay(3));
            ClassicAssert.AreEqual(TimeSpan.FromSeconds(60), PuttyConnection.ReconnectDelay(4));
            ClassicAssert.AreEqual(TimeSpan.FromSeconds(60), PuttyConnection.ReconnectDelay(50));
        }
    }
}
