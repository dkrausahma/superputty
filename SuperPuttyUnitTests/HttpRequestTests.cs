using System.Threading;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty;

namespace SuperPuttyUnitTests
{
    [TestFixture]
    public class HttpRequestTests
    {
        const string LatestReleaseUrl = "https://api.github.com/repos/jimradford/superputty/releases/latest";

        static bool Call(string url, out string content)
        {
            bool success = false;
            string body = null;
            using (ManualResetEvent done = new ManualResetEvent(false))
            {
                new httpRequest().MakeRequest(url, (ok, text) => { success = ok; body = text; done.Set(); });
                ClassicAssert.IsTrue(done.WaitOne(30000), "callback not called");
            }
            content = body;
            return success;
        }

        [Test, Category("NetworkTest")]
        public void LatestReleaseReturnsJson()
        {
            string content;
            ClassicAssert.IsTrue(Call(LatestReleaseUrl, out content), content);
            StringAssert.Contains("\"tag_name\"", content);
        }

        [Test, Category("NetworkTest")]
        public void HttpErrorReportsFailure()
        {
            string content;
            ClassicAssert.IsFalse(Call(LatestReleaseUrl + "-does-not-exist", out content));
            StringAssert.Contains("404", content);
        }

        [Test, Category("NetworkTest")]
        public void UnknownHostReportsFailure()
        {
            string content;
            ClassicAssert.IsFalse(Call("https://superputty-test.invalid/", out content));
            ClassicAssert.IsNotEmpty(content);
        }
    }
}
