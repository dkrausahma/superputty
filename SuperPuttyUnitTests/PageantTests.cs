using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Utils;

namespace SuperPuttyUnitTests
{
    /// <summary>Pageant status bar indicator: agent protocol messages and texts</summary>
    [TestFixture]
    public class PageantTests
    {
        [Test]
        public void RequestIsRequestIdentities()
        {
            CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 1, 11 }, Pageant.BuildIdentitiesRequest());
        }

        [Test]
        public void AnswerGivesKeyCount()
        {
            // length 5, type 12, 3 keys (key blobs follow, not needed)
            byte[] answer = new byte[64];
            new byte[] { 0, 0, 0, 5, 12, 0, 0, 0, 3 }.CopyTo(answer, 0);

            ClassicAssert.AreEqual(3, Pageant.ParseIdentitiesAnswer(answer));
        }

        [Test]
        public void FailureOrGarbageGivesNoCount()
        {
            ClassicAssert.IsNull(Pageant.ParseIdentitiesAnswer(new byte[] { 0, 0, 0, 1, 5, 0, 0, 0, 0 }), "SSH_AGENT_FAILURE");
            ClassicAssert.IsNull(Pageant.ParseIdentitiesAnswer(new byte[] { 0, 0, 0, 9 }), "too short");
            ClassicAssert.IsNull(Pageant.ParseIdentitiesAnswer(new byte[] { 0, 0, 0x7f, 0, 12, 0, 0, 0, 1 }), "length beyond the buffer");
            ClassicAssert.IsNull(Pageant.ParseIdentitiesAnswer(null));
        }

        [Test]
        public void Texts()
        {
            ClassicAssert.AreEqual("Pageant not running", Pageant.Describe(PageantState.NotRunning));
            ClassicAssert.AreEqual("Pageant running", Pageant.Describe(new PageantState { Running = true }));
            ClassicAssert.AreEqual("Pageant: no keys", Pageant.Describe(new PageantState { Running = true, KeyCount = 0 }));
            ClassicAssert.AreEqual("Pageant: 1 key", Pageant.Describe(new PageantState { Running = true, KeyCount = 1 }));
            ClassicAssert.AreEqual("Pageant: 4 keys", Pageant.Describe(new PageantState { Running = true, KeyCount = 4 }));
        }
    }
}
