using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using SuperPutty;
using SuperPutty.Data;

namespace SuperPuttyUnitTests
{
    /// <summary>
    /// Manual check of the owner-drawn Commands and Containers panes with sample data ([TestView], not run by
    /// dotnet test). Uses an in-memory command library, so the real Commands.XML is neither read nor written.
    /// </summary>
    public class PaneVisualsTests
    {
        [TestView]
        public void CommandsAndContainersWithSampleData()
        {
            CommandLibrary library = CommandLibrary.CreateDefault();
            library.Commands.Add(new CommandSnippet { Name = "Grep the logs", Command = "journalctl -u {{unit}} --since {{since:1 hour ago}} | grep -i {{pattern:error}}", Category = CommandLibrary.FavoritesCategory });
            typeof(CommandStore).GetField("library", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, library);

            List<DockerContainer> containers = DockerContainer.ParseList(string.Join("\n", new[]
            {
                "shop-dev-frontend\tacme/shop-frontend\tUp 14 hours (healthy)\tshop-dev\t0.0.0.0:9089->8080/tcp, :::9089->8080/tcp",
                "shop-dev-api\tacme/shop-api\tUp 15 hours (healthy)\tshop-dev\t0.0.0.0:5013->5005/tcp, :::5013->5005/tcp, 0.0.0.0:9088->8082/tcp, 127.0.0.1:9229->9229/tcp",
                "shop-dev-db\tpostgres:16\tUp 15 hours (unhealthy)\tshop-dev\t0.0.0.0:5441->5432/tcp",
                "shop-dev-cache\tredis:7\tUp 3 seconds (health: starting)\tshop-dev\t6379/tcp",
                "shop-dev-worker\tacme/shop-worker\tRestarting (1) 4 seconds ago\tshop-dev\t",
                "nightly-export-20261023_030001\tacme/export\tUp About an hour\t\t",
                "old-job\tbusybox\tExited (1) 2 days ago\t\t",
                "cleanup\tbusybox\tExited (0) 5 days ago\t\t",
            }));

            using (Form form = new Form { Text = "Pane visuals", Width = 640, Height = 600, Font = new Font("Microsoft Sans Serif", 8.25f) })
            {
                CommandsPane commands = new CommandsPane { TopLevel = false, FormBorderStyle = FormBorderStyle.None, Dock = DockStyle.Left, Width = 290 };
                ContainersPane containersPane = new ContainersPane { TopLevel = false, FormBorderStyle = FormBorderStyle.None, Dock = DockStyle.Fill };
                form.Controls.Add(containersPane);
                form.Controls.Add(commands);
                form.Shown += delegate
                {
                    commands.Show();
                    containersPane.Show();
                    Application.DoEvents(); // the pane's Load clears the list (no active tab); fill it afterwards
                    ((ToolStripButton)typeof(ContainersPane).GetField("toolStripButtonShowStopped", BindingFlags.NonPublic | BindingFlags.Instance)
                        .GetValue(containersPane)).Checked = true;
                    typeof(ContainersPane).GetMethod("ShowContainers", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(containersPane, new object[] { containers, "8 container(s), 5 running (sample data)" });
                };
                form.ShowDialog();
            }
        }
    }
}
