using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Data;

namespace SuperPuttyUnitTests
{
    /// <summary>Commands pane phase 3: placeholders, remembered values, compose defaults, import, reordering, multi-line</summary>
    [TestFixture]
    public class CommandPlaceholderTests
    {
        [Test]
        public void ParsesPlaceholdersWithDefaults()
        {
            List<CommandPlaceholder> placeholders = CommandPlaceholders.Parse(
                "docker compose --project-directory {{compose dir:~/app}} restart {{service}} && echo {{Service}}");

            ClassicAssert.AreEqual(2, placeholders.Count, "names are case-insensitive");
            ClassicAssert.AreEqual("compose dir", placeholders[0].Name);
            ClassicAssert.AreEqual("~/app", placeholders[0].Default);
            ClassicAssert.AreEqual("service", placeholders[1].Name);
            ClassicAssert.AreEqual(string.Empty, placeholders[1].Default);
        }

        [Test]
        public void LeavesShellAndDockerSyntaxAlone()
        {
            ClassicAssert.IsFalse(CommandPlaceholders.HasPlaceholders("docker ps --format '{{.Names}}\\t{{.Status}}'"));
            ClassicAssert.IsFalse(CommandPlaceholders.HasPlaceholders("find . -name '*.log' -exec rm {} \\;"));
            ClassicAssert.IsFalse(CommandPlaceholders.HasPlaceholders("echo ${HOME} {a,b}"));
            ClassicAssert.IsTrue(CommandPlaceholders.HasPlaceholders("systemctl status {{service}}"));
        }

        [Test]
        public void FillsValuesAndFallsBackToDefaults()
        {
            Dictionary<string, string> values = new Dictionary<string, string> { { "SERVICE", "nginx" } };

            ClassicAssert.AreEqual("cd ~/app && docker compose restart nginx",
                CommandPlaceholders.Fill("cd {{dir:~/app}} && docker compose restart {{service}}", values));
            ClassicAssert.AreEqual("docker ps --format '{{.Names}}'", CommandPlaceholders.Fill("docker ps --format '{{.Names}}'", values));
        }

        [Test]
        public void RemembersValuesPerSession()
        {
            CommandLibrary library = new CommandLibrary();
            library.SetValue("Prod/web01", "service", "nginx");
            library.SetValue("Prod/db01", "service", "postgres");
            library.SetValue("Prod/web01", "Service", "php-fpm");

            ClassicAssert.AreEqual("php-fpm", library.GetValue("Prod/web01", "service"));
            ClassicAssert.AreEqual("postgres", library.GetValue("Prod/db01", "SERVICE"));
            ClassicAssert.IsNull(library.GetValue("Dev/app01", "service"));
            ClassicAssert.AreEqual(2, library.Values.Count);
        }

        [Test]
        public void ValuesSurviveSaveAndLoad()
        {
            string file = Path.Combine(Path.GetTempPath(), "SuperPuTTY.PlaceholderTests." + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                CommandLibrary library = new CommandLibrary { Version = CommandLibrary.CurrentVersion };
                library.SetValue("web01", "compose dir", "~/shop");
                library.Save(file);

                ClassicAssert.AreEqual("~/shop", CommandLibrary.Load(file).GetValue("web01", "compose dir"));
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Test]
        public void DefaultsAndUpgradeContainComposeCommandsOnce()
        {
            ClassicAssert.IsTrue(CommandLibrary.CreateDefault().Commands.Any(c => c.Category == "Docker Compose" && CommandPlaceholders.HasPlaceholders(c.Command)));

            CommandLibrary v1 = new CommandLibrary { Version = 1 };
            v1.Commands.Add(new CommandSnippet { Command = "clear", Category = CommandLibrary.FavoritesCategory });
            ClassicAssert.IsTrue(v1.Upgrade());
            ClassicAssert.AreEqual(1, v1.Commands.Count(c => c.Command == "clear"), "v1 → v2 must not add Favorites again");
            int compose = v1.Commands.Count(c => c.Category == "Docker Compose");
            ClassicAssert.Greater(compose, 5);
            ClassicAssert.IsFalse(v1.Upgrade());
            ClassicAssert.AreEqual(compose, v1.Commands.Count(c => c.Category == "Docker Compose"));
        }

        [Test]
        public void MergeSkipsExistingEntries()
        {
            CommandLibrary library = new CommandLibrary();
            library.Commands.Add(new CommandSnippet { Command = "uptime", Category = "System" });
            CommandLibrary import = new CommandLibrary();
            import.Commands.Add(new CommandSnippet { Command = "uptime", Category = "system" });
            import.Commands.Add(new CommandSnippet { Command = "uptime", Category = "System", Scope = CommandScope.Session, ScopeKey = "web01" });
            import.Commands.Add(new CommandSnippet { Command = "free -h", Category = "System" });

            ClassicAssert.AreEqual(2, library.Merge(import));
            ClassicAssert.AreEqual(3, library.Commands.Count);
        }

        [Test]
        public void MoveStaysWithinCategoryAndScope()
        {
            CommandLibrary library = new CommandLibrary();
            CommandSnippet a = new CommandSnippet { Command = "a", Category = "X" };
            CommandSnippet other = new CommandSnippet { Command = "o", Category = "Y" };
            CommandSnippet b = new CommandSnippet { Command = "b", Category = "X" };
            library.Commands.AddRange(new[] { a, other, b });

            ClassicAssert.IsTrue(library.Move(b, -1));
            CollectionAssert.AreEqual(new[] { "b", "o", "a" }, library.Commands.Select(c => c.Command).ToArray());
            ClassicAssert.IsFalse(library.Move(b, -1), "already first in its category");
            ClassicAssert.IsFalse(library.Move(other, 1), "only command of its category");
        }

        [Test]
        public void MultiLineCommands()
        {
            CommandSnippet command = new CommandSnippet { Command = "cd ~/app\r\ngit pull\ndocker compose up -d" };

            ClassicAssert.AreEqual("cd ~/app …", command.DisplayText);
            ClassicAssert.AreEqual("cd ~/app\rgit pull\rdocker compose up -d", CommandSnippet.ForTerminal(command.Command));
            ClassicAssert.AreEqual("ls", CommandSnippet.ForTerminal("ls"));
        }
    }
}
