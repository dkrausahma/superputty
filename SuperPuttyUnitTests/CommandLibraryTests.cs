using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Data;

namespace SuperPuttyUnitTests
{
    /// <summary>Commands pane library: starter set, filter, XML file</summary>
    [TestFixture]
    public class CommandLibraryTests
    {
        private string tempFile;

        [SetUp]
        public void SetUp()
        {
            this.tempFile = Path.Combine(Path.GetTempPath(), "SuperPuTTY.CommandLibraryTests." + Guid.NewGuid().ToString("N") + ".xml");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(this.tempFile))
            {
                File.Delete(this.tempFile);
            }
        }

        [Test]
        public void DefaultsContainStandardCommandsInCategories()
        {
            CommandLibrary library = CommandLibrary.CreateDefault();

            ClassicAssert.Greater(library.Commands.Count, 30);
            CollectionAssert.Contains(library.Categories(), "System");
            CollectionAssert.Contains(library.Commands.Select(c => c.Command).ToList(), "df -h");
            ClassicAssert.IsTrue(library.Commands.All(c => c.Scope == CommandScope.Global && !string.IsNullOrEmpty(c.Category)));
        }

        [Test]
        public void MissingFileGivesDefaults()
        {
            CommandLibrary library = CommandLibrary.Load(this.tempFile);

            ClassicAssert.AreEqual(CommandLibrary.CreateDefault().Commands.Count, library.Commands.Count);
        }

        [Test]
        public void SaveAndLoadKeepsAllFieldsAndTrailingSpaces()
        {
            CommandLibrary library = new CommandLibrary();
            library.Commands.Add(new CommandSnippet { Command = "psql -U postgres ", Name = "Postgres shell", Description = "as postgres", Category = "DB" });
            library.Save(this.tempFile);

            CommandSnippet loaded = CommandLibrary.Load(this.tempFile).Commands.Single();

            ClassicAssert.AreEqual("psql -U postgres ", loaded.Command);
            ClassicAssert.AreEqual("Postgres shell", loaded.Name);
            ClassicAssert.AreEqual("as postgres", loaded.Description);
            ClassicAssert.AreEqual("DB", loaded.Category);
            ClassicAssert.AreEqual(CommandScope.Global, loaded.Scope);
        }

        [Test]
        public void InvalidFileGivesEmptyLibrary()
        {
            File.WriteAllText(this.tempFile, "<Commands><broken");

            ClassicAssert.AreEqual(0, CommandLibrary.Load(this.tempFile).Commands.Count);
        }

        [Test]
        public void FilterMatchesCommandNameDescriptionAndCategoryIgnoringCase()
        {
            CommandSnippet command = new CommandSnippet { Command = "ss -tulpn", Name = "Ports", Description = "Listening sockets", Category = "Network" };

            ClassicAssert.IsTrue(command.Matches("TULPN"));
            ClassicAssert.IsTrue(command.Matches("ports"));
            ClassicAssert.IsTrue(command.Matches("sockets"));
            ClassicAssert.IsTrue(command.Matches("netw"));
            ClassicAssert.IsTrue(command.Matches("  "));
            ClassicAssert.IsFalse(command.Matches("docker"));
        }

        [Test]
        public void CommandsWithoutCategoryGoToGeneral()
        {
            CommandLibrary library = new CommandLibrary();
            library.Commands.Add(new CommandSnippet { Command = "ls" });

            CollectionAssert.AreEqual(new[] { CommandLibrary.FavoritesCategory, CommandLibrary.DefaultCategory }, library.Categories());
        }

        [Test]
        public void DefaultsStartWithFavoritesContainingOnlyClear()
        {
            CommandLibrary library = CommandLibrary.CreateDefault();

            ClassicAssert.AreEqual(CommandLibrary.FavoritesCategory, library.Categories()[0]);
            CollectionAssert.AreEqual(new[] { "clear" },
                library.Commands.Where(c => CommandLibrary.IsFavorites(c.Category)).Select(c => c.Command).ToArray());
            ClassicAssert.AreEqual(CommandLibrary.CurrentVersion, library.Version);
        }

        [Test]
        public void FavoritesComeFirstEvenWhenEmptyOrAddedLater()
        {
            CommandLibrary library = new CommandLibrary();
            library.Commands.Add(new CommandSnippet { Command = "ls", Category = "Files" });
            library.Commands.Add(new CommandSnippet { Command = "pwd", Category = "favorites" });

            CollectionAssert.AreEqual(new[] { CommandLibrary.FavoritesCategory, "Files" }, library.Categories());
        }

        [Test]
        public void UpgradeAddsClearToFavoritesOnlyOnce()
        {
            CommandLibrary library = new CommandLibrary();
            library.Commands.Add(new CommandSnippet { Command = "ls", Category = "Files" });
            library.Save(this.tempFile);

            CommandLibrary loaded = CommandLibrary.Load(this.tempFile);
            ClassicAssert.IsTrue(loaded.Upgrade());
            ClassicAssert.AreEqual("clear", loaded.Commands[0].Command);

            // the user deletes "clear"; it must not come back
            loaded.Commands.RemoveAt(0);
            loaded.Save(this.tempFile);
            CommandLibrary reloaded = CommandLibrary.Load(this.tempFile);
            ClassicAssert.IsFalse(reloaded.Upgrade());
            ClassicAssert.IsFalse(reloaded.Commands.Any(c => c.Command == "clear"));
        }

        [Test]
        public void AddToFavoritesCopiesOnce()
        {
            CommandLibrary library = new CommandLibrary();
            CommandSnippet uptime = new CommandSnippet { Command = "uptime", Category = "System" };
            library.Commands.Add(uptime);

            CommandSnippet favorite;
            ClassicAssert.IsTrue(library.AddToFavorites(uptime, out favorite));
            ClassicAssert.AreEqual(CommandLibrary.FavoritesCategory, favorite.Category);
            ClassicAssert.AreEqual("System", uptime.Category);
            ClassicAssert.IsFalse(library.AddToFavorites(uptime, out favorite));
            ClassicAssert.AreEqual(2, library.Commands.Count);
        }

        [Test]
        public void DisplayTextPrefersName()
        {
            ClassicAssert.AreEqual("Ports", new CommandSnippet { Command = "ss -tulpn", Name = "Ports" }.DisplayText);
            ClassicAssert.AreEqual("ss -tulpn", new CommandSnippet { Command = "ss -tulpn" }.DisplayText);
        }
    }
}
