using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Utils;

namespace SuperPuttyUnitTests
{
    /// <summary>Update check: release tags compared with the running version</summary>
    [TestFixture]
    public class UpdateCheckTests
    {
        [Test]
        public void ComparesReleaseTagsWithTheRunningVersion()
        {
            ClassicAssert.IsTrue(UpdateCheck.IsNewer("v1.5.1", "1.5.0.0"));
            ClassicAssert.IsTrue(UpdateCheck.IsNewer("1.6", "1.5.0.0"));
            ClassicAssert.IsFalse(UpdateCheck.IsNewer("v1.5.0", "1.5.0.0"), "1.5.0 equals 1.5.0.0");
            ClassicAssert.IsFalse(UpdateCheck.IsNewer("1.4.0.9", "1.5.0.0"));
            ClassicAssert.IsTrue(UpdateCheck.IsNewer("v2.0.0-beta1", "1.5.0.0"), "suffixes are ignored");
        }

        [Test]
        public void UnreadableTagsAreNeverNewer()
        {
            ClassicAssert.IsFalse(UpdateCheck.IsNewer("latest", "1.5.0.0"));
            ClassicAssert.IsFalse(UpdateCheck.IsNewer(null, "1.5.0.0"));
            ClassicAssert.IsFalse(UpdateCheck.IsNewer("", "1.5.0.0"));
        }
    }
}
