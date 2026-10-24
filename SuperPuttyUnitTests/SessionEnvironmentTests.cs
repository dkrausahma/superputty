using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Data;

namespace SuperPuttyUnitTests
{
    /// <summary>Environment of a session (tab marker, prod warning): from the icon, else from a folder name</summary>
    [TestFixture]
    public class SessionEnvironmentTests
    {
        [Test]
        public void IconDecides()
        {
            ClassicAssert.AreEqual(EnvironmentKind.Prod, SessionEnvironment.Of("prod-server", "Customers/web01"));
            ClassicAssert.AreEqual(EnvironmentKind.Qual, SessionEnvironment.Of("qual-database", "db01"));
            ClassicAssert.AreEqual(EnvironmentKind.Dev, SessionEnvironment.Of("dev-gpu", "Prod/gpu01"), "the icon wins over the folder");
        }

        [Test]
        public void FolderNameIsTheFallback()
        {
            ClassicAssert.AreEqual(EnvironmentKind.Prod, SessionEnvironment.Of("firewall", "Customer A/Production/fw01"));
            ClassicAssert.AreEqual(EnvironmentKind.Qual, SessionEnvironment.Of(null, "Staging/app01"));
            ClassicAssert.AreEqual(EnvironmentKind.Dev, SessionEnvironment.Of("flag_yellow", "Dev/Java/app01"));
            ClassicAssert.AreEqual(EnvironmentKind.Prod, SessionEnvironment.Of("computer", "Prod/QA-Tools/x01"), "QA-Tools is no environment name, Prod is");
        }

        [Test]
        public void OtherwiseNone()
        {
            ClassicAssert.AreEqual(EnvironmentKind.None, SessionEnvironment.Of("flag_red", "Customers/web01"));
            ClassicAssert.AreEqual(EnvironmentKind.None, SessionEnvironment.Of("role-server", "web01"));
            ClassicAssert.AreEqual(EnvironmentKind.None, SessionEnvironment.Of("production-like", "Products/x"), "prefixes must match exactly");
            ClassicAssert.AreEqual(EnvironmentKind.None, SessionEnvironment.Of((SessionData)null));
        }
    }
}
