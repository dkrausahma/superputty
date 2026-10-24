using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Data;
using SuperPutty.Utils;

namespace SuperPuttyUnitTests
{
    /// <summary>Health notifications: which level changes notify, and how several alerts become one message</summary>
    [TestFixture]
    public class HealthAlertsTests
    {
        private static readonly HostHealth Ok = HostHealth.Parse("load=0.1\ncpus=4\nmem=40\ndisk=/=40%");
        private static readonly HostHealth Warning = HostHealth.Parse("load=0.1\ncpus=4\nmem=40\ndisk=/=85%");
        private static readonly HostHealth Critical = HostHealth.Parse("load=0.1\ncpus=4\nmem=40\ndisk=/=95%\nunhealthy=1");
        private static readonly HostHealth Failed = HostHealth.Failed("no answer (timeout)");

        [Test]
        public void NotifiesWhenAHostTurnsCriticalOnce()
        {
            HealthAlerts alerts = new HealthAlerts();

            ClassicAssert.IsNull(alerts.Update("web01", "web01", Ok));
            HealthAlert alert = alerts.Update("web01", "web01", Critical);
            ClassicAssert.IsTrue(alert.Critical);
            CollectionAssert.AreEqual(new[] { "1 unhealthy container(s)", "disk / 95% full" }, alert.Problems);
            ClassicAssert.IsNull(alerts.Update("web01", "web01", Critical), "still critical: no repeat");
        }

        [Test]
        public void ACriticalFirstResultNotifies()
        {
            ClassicAssert.IsTrue(new HealthAlerts().Update("web01", "web01", Critical).Critical);
        }

        [Test]
        public void NotifiesWhenACriticalHostRecovers()
        {
            HealthAlerts alerts = new HealthAlerts();
            alerts.Update("web01", "web01", Critical);

            HealthAlert alert = alerts.Update("web01", "web01", Warning);
            ClassicAssert.IsFalse(alert.Critical);
            CollectionAssert.AreEqual(new[] { "disk / 85% full" }, alert.Problems, "what is left");
        }

        [Test]
        public void WarningsAndFailedChecksDontNotify()
        {
            HealthAlerts alerts = new HealthAlerts();
            ClassicAssert.IsNull(alerts.Update("web01", "web01", Warning));
            ClassicAssert.IsNull(alerts.Update("web01", "web01", Ok));

            alerts.Update("db01", "db01", Critical);
            ClassicAssert.IsNull(alerts.Update("db01", "db01", Failed), "a failed check is no recovery");
            ClassicAssert.IsNull(alerts.Update("db01", "db01", Critical), "and the host is still known as critical");
            ClassicAssert.IsNull(alerts.Update(null, "x", Critical));
            ClassicAssert.IsNull(alerts.Update("x", "x", null));
        }

        [Test]
        public void OneAlertMakesADetailedMessage()
        {
            HealthAlerts alerts = new HealthAlerts();
            KeyValuePair<string, string> message = HealthAlerts.Compose(new[] { alerts.Update("web01", "web01", Critical) });

            ClassicAssert.AreEqual("web01: critical", message.Key);
            ClassicAssert.AreEqual("1 unhealthy container(s)\ndisk / 95% full", message.Value);

            alerts.Update("db01", "db01", Critical);
            message = HealthAlerts.Compose(new[] { alerts.Update("db01", "db01", Ok) });
            ClassicAssert.AreEqual("db01: back to normal", message.Key);
            ClassicAssert.AreEqual("No problems left.", message.Value);
        }

        [Test]
        public void SeveralAlertsMakeOneSummary()
        {
            HealthAlerts alerts = new HealthAlerts();
            alerts.Update("old", "old", Critical);
            List<HealthAlert> batch = new List<HealthAlert>
            {
                alerts.Update("a", "a", Critical),
                alerts.Update("b", "b", Critical),
                alerts.Update("c", "c", Critical),
                alerts.Update("d", "d", Critical),
                alerts.Update("old", "old", Ok),
            };

            KeyValuePair<string, string> message = HealthAlerts.Compose(batch);
            ClassicAssert.AreEqual("4 critical, 1 back to normal", message.Key);
            ClassicAssert.AreEqual("a: 1 unhealthy container(s)\nb: 1 unhealthy container(s)\nc: 1 unhealthy container(s)\n… and 2 more", message.Value);
        }
    }
}
