using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SuperPutty.Gui
{
    /// <summary>
    /// Overview of the SSH tunnels of all open tabs (tab, tunnel, state, where it is defined). Double-click or OK
    /// returns the selected row so the caller can switch to that tab. A plain list, built in code.
    /// </summary>
    public class dlgTunnels : Form
    {
        private readonly ListView list;

        /// <summary>Index of the chosen row, or -1</summary>
        public int SelectedIndex { get; private set; }

        /// <param name="rows">One row per tunnel: tab, tunnel, state, origin</param>
        public dlgTunnels(IList<string[]> rows)
        {
            this.Text = "SSH Tunnels of Open Tabs";
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.MinimizeBox = false;
            this.Size = new Size(720, 360);
            this.SelectedIndex = -1;

            this.list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false };
            this.list.Columns.Add("Tab", 160);
            this.list.Columns.Add("Tunnel", 210);
            this.list.Columns.Add("State", 200);
            this.list.Columns.Add("Defined in", 110);
            foreach (string[] row in rows)
            {
                ListViewItem item = new ListViewItem(row);
                if (row[2].StartsWith("NOT") || row[2].StartsWith("port in use"))
                {
                    item.ForeColor = Color.Firebrick;
                }
                this.list.Items.Add(item);
            }
            this.list.DoubleClick += delegate { Choose(); };

            Label empty = new Label
            {
                Text = "None of the open tabs has SSH tunnels.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = SystemColors.GrayText,
                Visible = rows.Count == 0
            };

            FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(6) };
            Button close = new Button { Text = "Close", DialogResult = DialogResult.Cancel };
            Button show = new Button { Text = "Go to Tab", Enabled = rows.Count > 0 };
            show.Click += delegate { Choose(); };
            buttons.Controls.Add(close);
            buttons.Controls.Add(show);

            this.Controls.Add(empty);
            this.Controls.Add(this.list);
            this.Controls.Add(buttons);
            this.CancelButton = close;
            this.AcceptButton = show;
            empty.BringToFront();
        }

        private void Choose()
        {
            if (this.list.SelectedIndices.Count == 0)
            {
                return;
            }
            this.SelectedIndex = this.list.SelectedIndices[0];
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
