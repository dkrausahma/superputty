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
            ClassicAssert.AreEqual("docker ps -a --format '{{.Names}}\\t{{.Image}}\\t{{.Status}}\\t{{.Label \"com.docker.compose.project\"}}'", DockerCommands.List(""));
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

            List<string> args = Plink.BuildArguments(session, "docker ps");

            CollectionAssert.AreEqual(new[] {
                "-ssh", "-batch", "-T", "-P", "2222", "-load", "Prod Profile", "-l", "deploy",
                "-i", @"C:\keys\deploy.ppk", "web01.example.com", "docker ps" }, args);
        }
    }
}
