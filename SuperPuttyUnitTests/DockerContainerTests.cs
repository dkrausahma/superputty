using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Data;
using SuperPutty.Utils;

namespace SuperPuttyUnitTests
{
    /// <summary>Containers pane: docker ps parsing, docker command lines, plink arguments</summary>
    [TestFixture]
    public class DockerContainerTests
    {
        [Test]
        public void ParsesDockerPsOutput()
        {
            string output =
                "app-web-1\tnginx:1.27\tUp 3 hours (healthy)\tapp\r\n" +
                "app-db-1\tpostgres:16\tUp 3 hours (unhealthy)\tapp\n" +
                "old_job\tbusybox\tExited (0) 2 days ago\t\n" +
                "\n";

            List<DockerContainer> containers = DockerContainer.ParseList(output);

            ClassicAssert.AreEqual(3, containers.Count);
            ClassicAssert.AreEqual("app-web-1", containers[0].Name);
            ClassicAssert.AreEqual("nginx:1.27", containers[0].Image);
            ClassicAssert.AreEqual("app", containers[0].Project);
            ClassicAssert.IsTrue(containers[0].IsRunning);
            ClassicAssert.IsFalse(containers[0].IsUnhealthy);
            ClassicAssert.IsTrue(containers[1].IsUnhealthy);
            ClassicAssert.IsFalse(containers[2].IsRunning);
            ClassicAssert.AreEqual(string.Empty, containers[2].Project);
        }

        [Test]
        public void ReadsThePortsField()
        {
            List<DockerContainer> containers = DockerContainer.ParseList(
                "verum-api\tapi-image\tUp 15 hours (healthy)\tverum\t0.0.0.0:9088->8082/tcp, :::9088->8082/tcp\n" +
                "old\tbusybox\tExited (0) 2 days ago\t\n");

            ClassicAssert.AreEqual("0.0.0.0:9088->8082/tcp, :::9088->8082/tcp", containers[0].Ports);
            ClassicAssert.AreEqual(string.Empty, containers[1].Ports); // older output without the field
        }

        [Test]
        public void PublishedPortsAreListedOnceAndShort()
        {
            CollectionAssert.AreEqual(new[] { "5013→5005", "9088→8082" },
                DockerContainer.ParsePorts("0.0.0.0:5013->5005/tcp, :::5013->5005/tcp, 0.0.0.0:9088->8082/tcp, :::9088->8082/tcp"));
            CollectionAssert.AreEqual(new[] { "127.0.0.1:8080→80", "53→53/udp", "8000-8001→8000-8001" },
                DockerContainer.ParsePorts("127.0.0.1:8080->80/tcp, 0.0.0.0:53->53/udp, [::]:8000-8001->8000-8001/tcp"));
            // exposed but not published
            CollectionAssert.IsEmpty(DockerContainer.ParsePorts("6379/tcp"));
            CollectionAssert.IsEmpty(DockerContainer.ParsePorts(null));
        }

        [Test]
        public void StateFollowsDockersStatusText()
        {
            Dictionary<string, ContainerState> cases = new Dictionary<string, ContainerState>
            {
                { "Up 14 hours (healthy)", ContainerState.Healthy },
                { "Up 2 minutes", ContainerState.Running },
                { "Up 3 seconds (health: starting)", ContainerState.Starting },
                { "Up 15 hours (unhealthy)", ContainerState.Unhealthy },
                { "Up 2 hours (Paused)", ContainerState.Paused },
                { "Restarting (1) 5 seconds ago", ContainerState.Restarting },
                { "Exited (0) 2 days ago", ContainerState.Exited },
                { "Exited (137) 1 hour ago", ContainerState.Exited },
                { "Exited (143) 1 hour ago", ContainerState.Exited },
                { "Exited (1) 2 days ago", ContainerState.Failed },
                { "Dead", ContainerState.Failed },
                { "Created", ContainerState.Created },
                { "Removal In Progress", ContainerState.Other },
            };
            foreach (KeyValuePair<string, ContainerState> entry in cases)
            {
                ClassicAssert.AreEqual(entry.Value, new DockerContainer { Status = entry.Key }.State, entry.Key);
            }
        }

        [Test]
        public void DisplayStatusMovesTheHealthBehindADot()
        {
            ClassicAssert.AreEqual("Up 14 hours · healthy", new DockerContainer { Status = "Up 14 hours (healthy)" }.DisplayStatus);
            ClassicAssert.AreEqual("Up 3 seconds · starting", new DockerContainer { Status = "Up 3 seconds (health: starting)" }.DisplayStatus);
            ClassicAssert.AreEqual("Exited (1) 2 days ago", new DockerContainer { Status = "Exited (1) 2 days ago" }.DisplayStatus);
        }

        [Test]
        public void ShortNameDropsTheComposeProject()
        {
            ClassicAssert.AreEqual("api", new DockerContainer { Name = "verum-dev-api", Project = "verum-dev" }.ShortName);
            ClassicAssert.AreEqual("web_1", new DockerContainer { Name = "app_web_1", Project = "app" }.ShortName); // compose v1
            ClassicAssert.AreEqual("verum-dev-api", new DockerContainer { Name = "verum-dev-api", Project = "other" }.ShortName);
            ClassicAssert.AreEqual("verum-dev-api", new DockerContainer { Name = "verum-dev-api", Project = "" }.ShortName);
            // nothing left after the prefix: keep the name
            ClassicAssert.AreEqual("app-", new DockerContainer { Name = "app-", Project = "app" }.ShortName);
            ClassicAssert.AreEqual("appx-web", new DockerContainer { Name = "appx-web", Project = "app" }.ShortName);
        }

        [Test]
        public void SkipsLinesThatAreNotContainers()
        {
            string output = "WARNING: something\nbad name;rm\timg\tUp 1 second\t\nok\timg\tUp 1 second\n";

            List<DockerContainer> containers = DockerContainer.ParseList(output);

            ClassicAssert.AreEqual(1, containers.Count);
            ClassicAssert.AreEqual("ok", containers[0].Name);
        }

        [Test]
        public void ValidatesContainerNames()
        {
            ClassicAssert.IsTrue(DockerContainer.IsValidName("app-web_1.blue"));
            ClassicAssert.IsFalse(DockerContainer.IsValidName("web; rm -rf /"));
            ClassicAssert.IsFalse(DockerContainer.IsValidName("$(id)"));
            ClassicAssert.IsFalse(DockerContainer.IsValidName("-u0"));
            ClassicAssert.IsFalse(DockerContainer.IsValidName(""));
            ClassicAssert.IsFalse(DockerContainer.IsValidName(null));
        }

        [Test]
        public void BuildsDockerCommands()
        {
            ClassicAssert.AreEqual("docker ps -a --format '{{.Names}}\\t{{.Image}}\\t{{.Status}}\\t{{.Label \"com.docker.compose.project\"}}\\t{{.Ports}}'", DockerCommands.List(""));
            ClassicAssert.AreEqual("docker exec -it web sh -c 'command -v bash >/dev/null 2>&1 && exec bash || exec sh'", DockerCommands.Shell("", "web", false));
            ClassicAssert.AreEqual("sudo docker exec -it -u 0 web sh -c 'command -v bash >/dev/null 2>&1 && exec bash || exec sh'", DockerCommands.Shell("sudo ", "web", true));
            ClassicAssert.AreEqual("docker logs -f --tail 200 web", DockerCommands.FollowLogs("", "web"));
            ClassicAssert.AreEqual("sudo -n docker restart web", DockerCommands.Action("sudo -n ", "restart", "web"));
            ClassicAssert.AreEqual("docker inspect web | less", DockerCommands.Inspect("", "web"));
        }

        [Test]
        public void RefusesInvalidNamesAndActions()
        {
            Assert.Throws<ArgumentException>(() => DockerCommands.Shell("", "web; reboot", false));
            Assert.Throws<ArgumentException>(() => DockerCommands.Action("", "rm", "web"));
        }

        [Test]
        public void DetectsMissingDockerPermission()
        {
            ClassicAssert.IsTrue(DockerCommands.IsPermissionDenied(
                "permission denied while trying to connect to the Docker daemon socket at unix:///var/run/docker.sock"));
            ClassicAssert.IsFalse(DockerCommands.IsPermissionDenied("bash: docker: command not found"));
        }

        [Test]
        public void PlinkArgumentsUseTheSessionsConnection()
        {
            SessionData session = new SessionData
            {
                SessionId = "Prod/web01", SessionName = "web01", Host = "web01.example.com", Port = 2222,
                Proto = ConnectionProtocol.SSH, Username = "deploy", PuttySession = "Prod Profile",
                ExtraArgs = "-L 8080:localhost:80 -i \"C:\\keys\\deploy.ppk\""
            };

            List<string> args = Plink.BuildArguments(session, "docker ps", null);

            CollectionAssert.AreEqual(new[] {
                "-ssh", "-batch", "-T", "-P", "2222", "-load", "Prod Profile", "-l", "deploy",
                "-i", @"C:\keys\deploy.ppk", "web01.example.com", "docker ps" }, args);
        }

        [Test]
        public void PlinkTakesTheHostFromAProfileThatHasOne()
        {
            // plink reads the first plain argument after such a profile as the command ("bash: <host>: command not found")
            SessionData session = new SessionData
            {
                SessionId = "Test/verum", Host = "10.0.0.5", Port = 22, Proto = ConnectionProtocol.SSH,
                Username = "root", PuttySession = "verum-test"
            };

            List<string> args = Plink.BuildArguments(session, "docker ps", "10.0.0.5 ");

            CollectionAssert.AreEqual(new[] {
                "-ssh", "-batch", "-T", "-P", "22", "-load", "verum-test", "-l", "root", "docker ps" }, args);
        }

        [Test]
        public void PlinkSkipsAProfileThatPointsToAnotherHost()
        {
            SessionData session = new SessionData
            {
                SessionId = "Test/verum", Host = "10.0.0.6", Port = 22, Proto = ConnectionProtocol.SSH,
                Username = "root", PuttySession = "verum-test"
            };

            List<string> args = Plink.BuildArguments(session, "docker ps", "10.0.0.5");

            CollectionAssert.AreEqual(new[] {
                "-ssh", "-batch", "-T", "-P", "22", "-l", "root", "10.0.0.6", "docker ps" }, args);
        }
    }
}
