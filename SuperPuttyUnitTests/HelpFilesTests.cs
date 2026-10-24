using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace SuperPuttyUnitTests
{
    /// <summary>
    /// The help: every page F1 or a menu opens exists, and the generated site (SuperPutty/help) contains every page
    /// of docs/ — i.e. tools/help-docs/build.py was run after the last docs change
    /// </summary>
    [TestFixture]
    public class HelpFilesTests
    {
        /// <summary>The repository, found from this source file (works whatever folder the tests run from)</summary>
        private static string RepoRoot
        {
            get
            {
                string root = Path.GetDirectoryName(Path.GetDirectoryName(SourceFile()));
                if (!File.Exists(Path.Combine(root, "SuperPutty.sln")))
                {
                    Assert.Ignore("Repository not found at " + root);
                }
                return root;
            }
        }

        private static string SourceFile([CallerFilePath] string path = "")
        {
            return path;
        }

        /// <summary>Pages named in the code: HelpFiles.Attach(…, "x.html") and HelpPage overrides returning "x.html"</summary>
        private static List<string> ReferencedPages()
        {
            Regex page = new Regex(@"(?:HelpFiles\.Attach\([^,]+,\s*""|get \{ return "")(?<page>[a-z0-9-]+\.html)""");
            return Directory.GetFiles(Path.Combine(RepoRoot, "SuperPutty"), "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                .SelectMany(f => page.Matches(File.ReadAllText(f)).Cast<Match>().Select(m => m.Groups["page"].Value))
                .Distinct().ToList();
        }

        [Test]
        public void EveryReferencedPageExists()
        {
            List<string> pages = ReferencedPages();
            ClassicAssert.Greater(pages.Count, 8, "F1 pages of panes and dialogs");
            foreach (string page in pages)
            {
                ClassicAssert.IsTrue(File.Exists(Path.Combine(RepoRoot, "SuperPutty", "help", page)), "missing help page " + page);
            }
        }

        [Test]
        public void GeneratedHelpHasEveryDocsPage()
        {
            foreach (string markdown in Directory.GetFiles(Path.Combine(RepoRoot, "docs"), "*.md"))
            {
                string html = Path.ChangeExtension(Path.GetFileName(markdown), ".html");
                string built = Path.Combine(RepoRoot, "SuperPutty", "help", html);
                ClassicAssert.IsTrue(File.Exists(built), html + " missing: run tools/help-docs/build.py");
                ClassicAssert.GreaterOrEqual(File.GetLastWriteTimeUtc(built), File.GetLastWriteTimeUtc(markdown).AddSeconds(-2),
                    html + " is older than " + Path.GetFileName(markdown) + ": run tools/help-docs/build.py");
            }
        }

        [Test]
        public void HelpLoadsNothingFromTheInternet()
        {
            Regex external = new Regex(@"(?:src|href)=""https?://[^""]*(?:fonts\.googleapis|fonts\.gstatic|unpkg\.com|cdn\.jsdelivr)", RegexOptions.IgnoreCase);
            foreach (string file in Directory.GetFiles(Path.Combine(RepoRoot, "SuperPutty", "help"), "*.html", SearchOption.AllDirectories))
            {
                ClassicAssert.IsFalse(external.IsMatch(File.ReadAllText(file)), Path.GetFileName(file) + " loads from a CDN");
            }
        }
    }
}
