using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Data;
using SuperPutty.Utils;

namespace SuperPuttyUnitTests
{
    /// <summary>Commands per folder and per session: applicability, orphans, following renames</summary>
    [TestFixture]
    public class CommandScopeTests
    {
        private static CommandSnippet Global(string command) { return new CommandSnippet { Command = command }; }
        private static CommandSnippet Folder(string command, string folder) { return new CommandSnippet { Command = command, Scope = CommandScope.Folder, ScopeKey = folder }; }
        private static CommandSnippet Session(string command, string id) { return new CommandSnippet { Command = command, Scope = CommandScope.Session, ScopeKey = id }; }

        [Test]
        public void FolderChainListsParentsNearestFirst()
        {
            CollectionAssert.AreEqual(new[] { "Prod/DB", "Prod" }, CommandLibrary.FolderChain("Prod/DB/db01"));
            CollectionAssert.IsEmpty(CommandLibrary.FolderChain("web01"));
            CollectionAssert.IsEmpty(CommandLibrary.FolderChain(null));
        }

        [Test]
        public void ScopeOptionsListAllFoldersAndSessions()
        {
            List<CommandScopeOption> options = CommandLibrary.ScopeOptions(new[] { "Prod/Web/web01", "Prod/DB/db01", "dev01", "Prod/DB/db01" });

            CollectionAssert.AreEqual(new[] {
                "All sessions",
                "Folder Prod", "Folder Prod/DB", "Folder Prod/Web",
                "Session dev01", "Session Prod/DB/db01", "Session Prod/Web/web01" },
                options.Select(o => o.Text).ToArray());
        }

        [Test]
        public void CommandsApplyByScope()
        {
            ClassicAssert.IsTrue(CommandLibrary.AppliesTo(Global("ls"), "Prod/DB/db01"));
            ClassicAssert.IsTrue(CommandLibrary.AppliesTo(Global("ls"), null));
            ClassicAssert.IsTrue(CommandLibrary.AppliesTo(Folder("psql", "Prod"), "Prod/DB/db01"));
            ClassicAssert.IsFalse(CommandLibrary.AppliesTo(Folder("psql", "Prod/DB"), "Prod/Web/web01"));
            ClassicAssert.IsFalse(CommandLibrary.AppliesTo(Folder("psql", "Pro"), "Prod/DB/db01"), "no prefix matching");
            ClassicAssert.IsTrue(CommandLibrary.AppliesTo(Session("mvn", "Dev/app01"), "Dev/app01"));
            ClassicAssert.IsFalse(CommandLibrary.AppliesTo(Session("mvn", "Dev/app01"), "Dev/app02"));
            ClassicAssert.IsFalse(CommandLibrary.AppliesTo(Session("mvn", "Dev/app01"), null));
        }

        [Test]
        public void OrphansAreCommandsOfMissingSessionsOrFolders()
        {
            List<string> ids = new List<string> { "Prod/DB/db01", "web01" };

            ClassicAssert.IsFalse(CommandLibrary.IsOrphan(Global("ls"), ids));
            ClassicAssert.IsFalse(CommandLibrary.IsOrphan(Session("x", "web01"), ids));
            ClassicAssert.IsTrue(CommandLibrary.IsOrphan(Session("x", "gone"), ids));
            ClassicAssert.IsFalse(CommandLibrary.IsOrphan(Folder("x", "Prod"), ids));
            ClassicAssert.IsTrue(CommandLibrary.IsOrphan(Folder("x", "Test"), ids));
        }

        [Test]
        public void RenamedSessionKeepsItsCommands()
        {
            CommandLibrary library = new CommandLibrary();
            library.Commands.Add(Session("mvn package", "Dev/app01"));

            ClassicAssert.IsTrue(library.SessionIdChanged("Dev/app01", "Dev/java-app", new[] { "Dev/java-app" }));
            ClassicAssert.AreEqual("Dev/java-app", library.Commands[0].ScopeKey);
        }

        [Test]
        public void RenamedFolderMovesFolderCommandsOnceEmpty()
        {
            CommandLibrary library = new CommandLibrary();
            library.Commands.Add(Folder("psql", "Prod/DB"));
            library.Commands.Add(Folder("df -h", "Prod"));

            // folder "Prod" renamed to "Production": its two sessions change one after the other
            ClassicAssert.IsFalse(library.SessionIdChanged("Prod/DB/db01", "Production/DB/db01", new[] { "Production/DB/db01", "Prod/DB/db02" }));
            ClassicAssert.AreEqual("Prod/DB", library.Commands[0].ScopeKey, "db02 is still in the old folder");

            ClassicAssert.IsTrue(library.SessionIdChanged("Prod/DB/db02", "Production/DB/db02", new[] { "Production/DB/db01", "Production/DB/db02" }));
            ClassicAssert.AreEqual("Production/DB", library.Commands[0].ScopeKey);
            ClassicAssert.AreEqual("Production", library.Commands[1].ScopeKey);
        }

        [Test]
        public void SessionMovedToAnotherFolderLeavesFolderCommands()
        {
            CommandLibrary library = new CommandLibrary();
            library.Commands.Add(Folder("psql", "Prod/DB"));
            library.Commands.Add(Session("vacuum", "Prod/DB/db01"));

            // moved to a folder of another depth: only the session's own commands follow
            ClassicAssert.IsTrue(library.SessionIdChanged("Prod/DB/db01", "Archive/db01", new[] { "Archive/db01", "Prod/DB/db02" }));
            ClassicAssert.AreEqual("Prod/DB", library.Commands[0].ScopeKey);
            ClassicAssert.AreEqual("Archive/db01", library.Commands[1].ScopeKey);
        }

        [Test]
        public void FavoritesArePerScope()
        {
            CommandLibrary library = new CommandLibrary();
            CommandSnippet global = Global("uptime");
            CommandSnippet session = Session("uptime", "web01");
            library.Commands.Add(global);
            library.Commands.Add(session);

            CommandSnippet favorite;
            ClassicAssert.IsTrue(library.AddToFavorites(global, out favorite));
            ClassicAssert.IsTrue(library.AddToFavorites(session, out favorite), "same command, other scope");
            ClassicAssert.AreEqual(CommandScope.Session, favorite.Scope);
            ClassicAssert.IsFalse(library.AddToFavorites(session, out favorite));
        }

        [Test]
        public void ScopeOptionsMatchCommands()
        {
            ClassicAssert.IsTrue(CommandScopeOption.Global().Matches(Global("ls")));
            ClassicAssert.IsTrue(CommandScopeOption.Folder("Prod").Matches(Folder("x", "Prod")));
            ClassicAssert.IsFalse(CommandScopeOption.Folder("Prod").Matches(Folder("x", "Prod/DB")));
            ClassicAssert.AreEqual("Session web01", CommandScopeOption.Of(Session("x", "web01")).Text);
        }

        [Test]
        public void CommandTabsBelongToTheirOriginalSession()
        {
            SessionData original = new SessionData { SessionId = "Prod/web01", SessionName = "web01", Host = "web01", Proto = ConnectionProtocol.SSH };

            SessionData tab = RemoteCommandTab.CreateCommandSession(original, @"C:\Temp\c.sh", "nginx shell");
            SessionData tabOfTab = RemoteCommandTab.CreateCommandSession(tab, @"C:\Temp\d.sh", "logs");

            ClassicAssert.AreEqual("Prod/web01", original.ScopeSessionId);
            ClassicAssert.AreEqual("Prod/web01", tab.ScopeSessionId);
            ClassicAssert.AreEqual("Prod/web01", tabOfTab.ScopeSessionId);
        }
    }
}
