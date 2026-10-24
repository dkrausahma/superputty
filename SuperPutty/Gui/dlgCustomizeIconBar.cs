using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SuperPutty.Utils;

namespace SuperPutty.Gui
{
    /// <summary>
    /// Customize Icon Bar: every tool with a checkbox (shown or not), in icon bar order followed by the hidden
    /// ones; Move Up/Down reorder, Add Separator / Add Space insert a divider or an empty gap. Separators and spaces
    /// count like tools: only checked ones.
    /// </summary>
    public class dlgCustomizeIconBar : Form
    {
        private const string SeparatorText = "──────  separator  ──────";
        private const string SpaceText = "(space)";

        private readonly Dictionary<string, frmSuperPutty.IconBarTool> tools;
        private readonly List<string> defaults;
        private readonly ListView list = new ListView();
        private readonly ImageList images = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
        private readonly Button buttonUp = new Button { Text = "Move &Up" };
        private readonly Button buttonDown = new Button { Text = "Move &Down" };
        private readonly Button buttonSeparator = new Button { Text = "Add &Separator" };
        private readonly Button buttonSpace = new Button { Text = "Add S&pace" };
        private readonly Button buttonReset = new Button { Text = "&Reset to Default" };

        /// <summary>The chosen entries: tool ids and "|" separators, in order</summary>
        public List<string> Entries
        {
            get
            {
                return this.list.Items.Cast<ListViewItem>().Where(i => i.Checked).Select(i => (string)i.Tag).ToList();
            }
        }

        public dlgCustomizeIconBar(IEnumerable<frmSuperPutty.IconBarTool> allTools, List<string> current, List<string> defaultLayout)
        {
            SuperPutty.Utils.HelpFiles.Attach(this, "icon-bar.html");
            this.tools = allTools.ToDictionary(t => t.Id);
            this.defaults = defaultLayout;
            foreach (frmSuperPutty.IconBarTool tool in this.tools.Values.Where(t => t.Image != null))
            {
                this.images.Images.Add(tool.Id, tool.Image);
            }

            this.Text = "Customize Icon Bar";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(440, 470);

            Label hint = new Label
            {
                Text = "Checked tools, separators and spaces appear on the icon bar, in this order.",
                Location = new Point(12, 10), Size = new Size(416, 30)
            };

            this.list.Location = new Point(12, 42);
            this.list.Size = new Size(290, 380);
            this.list.View = View.Details;
            this.list.HeaderStyle = ColumnHeaderStyle.None;
            this.list.CheckBoxes = true;
            this.list.FullRowSelect = true;
            this.list.HideSelection = false;
            this.list.MultiSelect = false;
            this.list.SmallImageList = this.images;
            this.list.Columns.Add(string.Empty, 266);
            this.list.SelectedIndexChanged += delegate { UpdateButtons(); };

            int x = 314;
            Button[] buttons = { this.buttonUp, this.buttonDown, this.buttonSeparator, this.buttonSpace, this.buttonReset };
            foreach (Button button in buttons)
            {
                button.Location = new Point(x, 42 + 34 * Array.IndexOf(buttons, button) + (button == this.buttonReset ? 20 : 0));
                button.Size = new Size(114, 26);
            }
            this.buttonUp.Click += delegate { MoveSelected(-1); };
            this.buttonDown.Click += delegate { MoveSelected(1); };
            this.buttonSeparator.Click += delegate { AddGap(IconBarLayout.Separator); };
            this.buttonSpace.Click += delegate { AddGap(IconBarLayout.Space); };
            this.buttonReset.Click += delegate { Fill(this.defaults); };

            Button ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(272, 432), Size = new Size(75, 26) };
            Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(353, 432), Size = new Size(75, 26) };
            this.AcceptButton = ok;
            this.CancelButton = cancel;

            this.Controls.AddRange(new Control[] { hint, this.list, this.buttonUp, this.buttonDown, this.buttonSeparator, this.buttonSpace, this.buttonReset, ok, cancel });
            Fill(current);
        }

        /// <summary>The layout's entries checked, then the other tools unchecked</summary>
        private void Fill(List<string> layout)
        {
            this.list.BeginUpdate();
            try
            {
                this.list.Items.Clear();
                foreach (string entry in layout)
                {
                    this.list.Items.Add(CreateItem(entry, true));
                }
                foreach (string id in this.tools.Keys.Where(id => !layout.Contains(id)))
                {
                    this.list.Items.Add(CreateItem(id, false));
                }
            }
            finally
            {
                this.list.EndUpdate();
            }
            UpdateButtons();
        }

        private ListViewItem CreateItem(string entry, bool shown)
        {
            if (IconBarLayout.IsGap(entry))
            {
                return new ListViewItem(entry == IconBarLayout.Separator ? SeparatorText : SpaceText) { Tag = entry, Checked = true, ForeColor = SystemColors.GrayText };
            }
            frmSuperPutty.IconBarTool tool = this.tools[entry];
            return new ListViewItem(tool.Text) { Tag = entry, Checked = shown, ImageKey = tool.Image == null ? null : entry };
        }

        private void MoveSelected(int direction)
        {
            if (this.list.SelectedItems.Count == 0)
            {
                return;
            }
            ListViewItem item = this.list.SelectedItems[0];
            int index = item.Index + direction;
            if (index < 0 || index >= this.list.Items.Count)
            {
                return;
            }
            this.list.Items.Remove(item);
            this.list.Items.Insert(index, item);
            item.Selected = true;
            item.EnsureVisible();
            this.list.Focus();
            UpdateButtons();
        }

        /// <summary>Insert a separator or a space after the selected entry</summary>
        private void AddGap(string gap)
        {
            int index = this.list.SelectedItems.Count == 0 ? 0 : this.list.SelectedItems[0].Index + 1;
            ListViewItem item = CreateItem(gap, true);
            this.list.Items.Insert(index, item);
            item.Selected = true;
            item.EnsureVisible();
            this.list.Focus();
        }

        private void UpdateButtons()
        {
            int index = this.list.SelectedItems.Count == 0 ? -1 : this.list.SelectedItems[0].Index;
            this.buttonUp.Enabled = index > 0;
            this.buttonDown.Enabled = index >= 0 && index < this.list.Items.Count - 1;
        }
    }
}
