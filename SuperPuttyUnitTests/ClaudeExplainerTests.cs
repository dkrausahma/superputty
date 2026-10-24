using System.Threading;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Utils;

namespace SuperPuttyUnitTests
{
    /// <summary>Explain in Claude: key lookup, message format, and (network) the API's answer to a wrong key</summary>
    [TestFixture]
    public class ClaudeExplainerTests
    {
        [Test]
        public void StoredKeyWinsOverEnvironment()
        {
            ClaudeExplainer.KeySource source;
            string key = ClaudeExplainer.ApiKey(Dpapi.Protect("sk-ant-stored"), out source);

            ClassicAssert.AreEqual("sk-ant-stored", key);
            ClassicAssert.AreEqual(ClaudeExplainer.KeySource.Settings, source);
        }

        [Test]
        public void UndecryptableKeyIsIgnored()
        {
            ClaudeExplainer.KeySource source;
            string key = ClaudeExplainer.ApiKey(Dpapi.Prefix + "bm90IGRwYXBp", out source);

            ClassicAssert.AreNotEqual(ClaudeExplainer.KeySource.Settings, source);
            ClassicAssert.AreNotEqual("not dpapi", key);
        }

        [Test]
        public void MessageHoldsQuestionAndTerminalText()
        {
            string message = ClaudeExplainer.BuildMessage("Warum?", "$ df -h\n/dev/sda1 100%\n\n", "web\"01");

            ClassicAssert.AreEqual("Warum?\n\n<terminal session=\"web'01\">\n$ df -h\n/dev/sda1 100%\n</terminal>", message);
            StringAssert.StartsWith(ClaudeExplainer.DefaultQuestion, ClaudeExplainer.BuildMessage(" ", "x", null));
        }

        [Test, Category("NetworkTest")]
        public void WrongKeyIsReportedAsRejected()
        {
            string error = ClaudeExplainer.Explain("sk-ant-invalid-test-key", "Hello", piece => { }, CancellationToken.None).GetAwaiter().GetResult();

            StringAssert.Contains("rejected", error);
        }
    }
}
