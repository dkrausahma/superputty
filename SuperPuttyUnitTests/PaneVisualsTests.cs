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

        /// <summary>Two rounds of sample output, as the Host Info pane would get them from a host</summary>
        public static HostInfo SampleHostInfo()
        {
            string[] lines =
            {
                "host=gpu-dev01.spoke52.example.com", "os=Ubuntu 24.04.1 LTS", "kernel=6.8.0-45-generic", "cpus=16",
                "addr=eth0=78.47.238.52/32", "addr=enp7s0=10.52.6.2/32", "addr=docker0=172.17.0.1/16",
                "disk=/dev/sda1=/=419430400=200000000=219430400", "disk=/dev/sdb=/data=1048576000=870000000=178576000",
                "disk=/dev/sda15=/boot/efi=106858=6186=100672",
                "user=root     pts/0        2026-10-24 09:12 (10.8.0.6)", "user=dominique pts/1        2026-10-24 10:01 (10.8.0.6)",
                "failedunit=nvidia-fabricmanager.service", "unhealthy=0", "slow=1",
                "uptime=3083367", "loadavg=0.05 0.08 0.08 2/671 12345", "cpustat=cpu 10000 0 5000 80000 500 0 0 0 0 0",
                "meminfo=MemTotal=65378304", "meminfo=MemAvailable=50034688", "meminfo=SwapTotal=8388608", "meminfo=SwapFree=6021120",
                "proc=812=5000=python3", "proc=901=300=dockerd", "proc=950=100=containerd", "--end",
                "uptime=3083372", "loadavg=1.45 0.60 0.30 3/672 12346", "cpustat=cpu 13000 0 5400 84200 600 0 0 0 0 0",
                "meminfo=MemTotal=65378304", "meminfo=MemAvailable=50034688", "meminfo=SwapTotal=8388608", "meminfo=SwapFree=6021120",
                "proc=812=5000=python3", "proc=812=7600=python3", "proc=901=340=dockerd", "proc=950=105=containerd", "--end",
            };
            HostInfoReader reader = new HostInfoReader();
            HostInfo info = null;
            foreach (string line in lines)
            {
                info = reader.Read(line) ?? info;
            }
            return info;
        }

        [TestView]
        public void HostInfoWithSampleData()
        {
            using (Form form = new Form { Text = "Host Info", Width = 420, Height = 460, Font = new Font("Microsoft Sans Serif", 8.25f) })
            {
                SuperPutty.Gui.HostInfoView view = new SuperPutty.Gui.HostInfoView { Dock = DockStyle.Fill, Info = SampleHostInfo(), Status = "Live · updated 10:32:05 · every 5 s (sample data)" };
                form.Controls.Add(view);
                form.ShowDialog();
            }
        }
    }
}
