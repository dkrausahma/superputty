using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SuperPutty.Data;

namespace SuperPutty.Gui
{
    /// <summary>
    /// Asks for the values of a command's placeholders ({{name}}) before it is sent, with a live preview.
    /// The rows depend on the command, so the form is built in code rather than in the designer.
    /// </summary>
    public class dlgPlaceholderValues : Form
    {
        private readonly string command;
        private readonly List<KeyValuePair<string, TextBox>> fields = new List<KeyValuePair<string, TextBox>>();
        private readonly TextBox preview;

        /// <summary>The entered values by placeholder name</summary>
        public Dictionary<string, string> Values { get; private set; }

        /// <summary>The command with the values filled in</summary>
        public string Result { get; private set; }

        /// <param name="initialValues">Last used value per placeholder (or its default)</param>
        public dlgPlaceholderValues(string command, IList<CommandPlaceholder> placeholders, IDictionary<string, string> initialValues, string target)
        {
            this.command = command;
            this.Text = "Command Values";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = this.MaximizeBox = false;
            this.ShowInTaskbar = false;
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Padding = new Padding(10);

            TableLayoutPanel table = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380));

            Label header = new Label { Text = string.IsNullOrEmpty(target) ? "Values for the command:" : "Values for the command on " + target + ":", AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
            table.Controls.Add(header, 0, 0);
            table.SetColumnSpan(header, 2);

            Font mono = new Font("Lucida Console", 8.25f);
            int row = 1;
            foreach (CommandPlaceholder placeholder in placeholders)
            {
                string initial;
                initialValues.TryGetValue(placeholder.Name, out initial);
                TextBox box = new TextBox { Text = initial ?? placeholder.Default, Font = mono, Dock = DockStyle.Fill };
                box.TextChanged += delegate { UpdatePreview(); };
                table.Controls.Add(new Label { Text = placeholder.Name + ":", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 6, 0) }, 0, row);
                table.Controls.Add(box, 1, row);
                this.fields.Add(new KeyValuePair<string, TextBox>(placeholder.Name, box));
                row++;
            }

            this.preview = new TextBox { ReadOnly = true, Font = mono, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0), BackColor = SystemColors.Control };
            Label previewLabel = new Label { Text = "Command:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 14, 6, 0) };
            table.Controls.Add(previewLabel, 0, row);
            table.Controls.Add(this.preview, 1, row);
            row++;

            FlowLayoutPanel buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
            Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            Button ok = new Button { Text = "OK" };
            ok.Click += delegate { Accept(); };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            table.Controls.Add(buttons, 0, row);
            table.SetColumnSpan(buttons, 2);

            this.Controls.Add(table);
            this.AcceptButton = ok;
            this.CancelButton = cancel;
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            UpdatePreview();

            this.Shown += delegate
            {
                if (this.fields.Count > 0)
                {
                    this.fields[0].Value.Focus();
                    this.fields[0].Value.SelectAll();
                }
            };
        }

        private Dictionary<string, string> Collect()
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, TextBox> field in this.fields)
            {
                values[field.Key] = field.Value.Text;
            }
            return values;
        }

        private void UpdatePreview()
        {
            this.preview.Text = CommandPlaceholders.Fill(this.command, Collect()).Replace("\r", " ").Replace("\n", " ⏎ ");
        }

        private void Accept()
        {
            this.Values = Collect();
            this.Result = CommandPlaceholders.Fill(this.command, this.Values);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
