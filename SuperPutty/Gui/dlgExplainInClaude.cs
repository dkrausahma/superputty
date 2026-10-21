using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using SuperPutty.Utils;

namespace SuperPutty.Gui
{
    /// <summary>
    /// Preview and answer window of "Explain in Claude": the terminal text is shown editable, and only what the user
    /// leaves in it is sent when they press Send. The answer streams into the lower half. Built in code.
    /// </summary>
    public class dlgExplainInClaude : Form
    {
        private readonly string sessionName;
        private readonly TextBox question;
        private readonly TextBox terminalText;
        private readonly Label textInfo;
        private readonly TextBox answer;
        private readonly Label keyInfo;
        private readonly Button send;
        private readonly Button cancel;
        private readonly Button copy;
        private CancellationTokenSource running;

        public dlgExplainInClaude(string sessionName, string text)
        {
            this.sessionName = sessionName;
            this.Text = "Explain in Claude - " + sessionName;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.MinimizeBox = false;
            this.Size = new Size(820, 680);
            this.MinimumSize = new Size(520, 420);
            Font mono = new Font("Consolas", 9f);

            // header: what happens with the text, where the key comes from
            Label notice = new Label
            {
                Text = "The text below is sent to Anthropic's Claude API (" + ClaudeExplainer.ModelName + ") when you press Send. " +
                       "Check it and remove passwords, tokens or customer data first.",
                Dock = DockStyle.Fill,
                AutoSize = false,
                ForeColor = Color.FromArgb(150, 60, 0)
            };
            this.keyInfo = new Label { Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleRight, ForeColor = SystemColors.GrayText };
            Button apiKey = new Button { Text = "API &Key…", AutoSize = true, Anchor = AnchorStyles.Right };
            apiKey.Click += delegate { EditApiKey(); };
            TableLayoutPanel header = new TableLayoutPanel { Dock = DockStyle.Top, Height = 36, ColumnCount = 3, Padding = new Padding(6, 4, 6, 0) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.Controls.Add(notice, 0, 0);
            header.Controls.Add(this.keyInfo, 1, 0);
            header.Controls.Add(apiKey, 2, 0);

            Label questionLabel = new Label { Text = "&Question:", Dock = DockStyle.Top, Height = 18, Padding = new Padding(6, 4, 0, 0) };
            this.question = new TextBox { Dock = DockStyle.Top, Text = ClaudeExplainer.DefaultQuestion };
            Panel questionPanel = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6, 0, 6, 4) };
            questionPanel.Controls.Add(this.question);
            questionPanel.Controls.Add(questionLabel);

            // terminal text (editable) above, answer below
            this.terminalText = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = mono, Text = Normalize(text), AcceptsTab = true };
            this.terminalText.TextChanged += delegate { UpdateTextInfo(); };
            this.textInfo = new Label { Dock = DockStyle.Bottom, Height = 18, ForeColor = SystemColors.GrayText, Padding = new Padding(0, 3, 0, 0) };
            Label terminalLabel = new Label { Text = "&Terminal text to send (editable):", Dock = DockStyle.Top, Height = 18 };

            this.answer = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true, Font = mono, BackColor = SystemColors.Window };
            Label answerLabel = new Label { Text = "Answer:", Dock = DockStyle.Top, Height = 18 };

            SplitContainer split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, Padding = new Padding(6, 0, 6, 0) };
            split.Panel1.Controls.Add(this.terminalText);
            split.Panel1.Controls.Add(this.textInfo);
            split.Panel1.Controls.Add(terminalLabel);
            split.Panel2.Controls.Add(this.answer);
            split.Panel2.Controls.Add(answerLabel);

            FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(6) };
            Button close = new Button { Text = "Close", DialogResult = DialogResult.Cancel, AutoSize = true };
            this.copy = new Button { Text = "&Copy Answer", AutoSize = true, Enabled = false };
            this.copy.Click += delegate { if (this.answer.TextLength > 0) { Clipboard.SetText(this.answer.Text); } };
            this.cancel = new Button { Text = "Stop", AutoSize = true, Enabled = false };
            this.cancel.Click += delegate { this.running?.Cancel(); };
            this.send = new Button { Text = "&Send to Claude", AutoSize = true };
            this.send.Click += delegate { Send(); };
            buttons.Controls.Add(close);
            buttons.Controls.Add(this.copy);
            buttons.Controls.Add(this.cancel);
            buttons.Controls.Add(this.send);

            this.Controls.Add(split);
            this.Controls.Add(questionPanel);
            this.Controls.Add(header);
            this.Controls.Add(buttons);
            this.CancelButton = close;

            this.Load += delegate { split.SplitterDistance = split.Height * 55 / 100; this.terminalText.Select(0, 0); };
            this.FormClosing += delegate { this.running?.Cancel(); };
            UpdateTextInfo();
            UpdateKeyInfo();
        }

        /// <summary>Text boxes need CRLF</summary>
        private static string Normalize(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Replace("\n", "\r\n");
        }

        private void UpdateTextInfo()
        {
            int lines = this.terminalText.Lines.Length;
            this.textInfo.Text = string.Format("{0} lines, {1:N0} characters", lines, this.terminalText.TextLength);
        }

        private string CurrentKey(out ClaudeExplainer.KeySource source)
        {
            return ClaudeExplainer.ApiKey(SuperPuTTY.Settings.AnthropicApiKey, out source);
        }

        private void UpdateKeyInfo()
        {
            ClaudeExplainer.KeySource source;
            CurrentKey(out source);
            this.keyInfo.Text = source == ClaudeExplainer.KeySource.Settings ? "API key: stored in SuperPuTTY"
                : source == ClaudeExplainer.KeySource.Environment ? "API key: " + ClaudeExplainer.EnvironmentVariable
                : "No API key";
            this.keyInfo.ForeColor = source == ClaudeExplainer.KeySource.None ? Color.Firebrick : SystemColors.GrayText;
        }

        private void EditApiKey()
        {
            string key = ApiKeyPrompt(this);
            if (key == null)
            {
                return;
            }
            SuperPuTTY.Settings.AnthropicApiKey = key.Length == 0 ? string.Empty : Dpapi.Protect(key);
            SuperPuTTY.Settings.Save();
            UpdateKeyInfo();
        }

        private async void Send()
        {
            ClaudeExplainer.KeySource source;
            string key = CurrentKey(out source);
            if (key == null)
            {
                EditApiKey();
                key = CurrentKey(out source);
                if (key == null)
                {
                    return;
                }
            }
            if (this.terminalText.TextLength == 0)
            {
                MessageBox.Show(this, "There is no terminal text to send.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            this.answer.Clear();
            this.send.Enabled = false;
            this.cancel.Enabled = true;
            this.copy.Enabled = false;
            this.terminalText.ReadOnly = true;
            this.question.ReadOnly = true;
            this.running = new CancellationTokenSource();
            string message = ClaudeExplainer.BuildMessage(this.question.Text, this.terminalText.Text.Replace("\r\n", "\n"), this.sessionName);

            string error = await ClaudeExplainer.Explain(key, message, piece =>
            {
                if (!this.IsDisposed)
                {
                    this.BeginInvoke((MethodInvoker)delegate { this.answer.AppendText(piece.Replace("\r\n", "\n").Replace("\n", "\r\n")); });
                }
            }, this.running.Token);

            if (this.IsDisposed)
            {
                return;
            }
            if (error != null)
            {
                this.answer.AppendText((this.answer.TextLength > 0 ? "\r\n\r\n" : string.Empty) + "[" + error + "]");
            }
            this.running.Dispose();
            this.running = null;
            this.send.Enabled = true;
            this.send.Text = "&Send Again";
            this.cancel.Enabled = false;
            this.copy.Enabled = this.answer.TextLength > 0;
            this.terminalText.ReadOnly = false;
            this.question.ReadOnly = false;
        }

        /// <summary>Asks for an API key; returns the key, "" to remove the stored one, or null if cancelled</summary>
        public static string ApiKeyPrompt(IWin32Window owner)
        {
            using (Form form = new Form())
            {
                form.Text = "Anthropic API Key";
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = false;
                form.ClientSize = new Size(460, 130);

                Label info = new Label
                {
                    Text = "Stored encrypted for your Windows user (DPAPI) in SuperPuTTY.settings. Leave empty and press OK to " +
                           "remove a stored key; then " + ClaudeExplainer.EnvironmentVariable + " is used if set.",
                    Location = new Point(10, 10),
                    Size = new Size(440, 44)
                };
                TextBox key = new TextBox { Location = new Point(10, 58), Width = 440, UseSystemPasswordChar = true };
                Button ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(294, 94) };
                Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(375, 94) };
                form.Controls.AddRange(new Control[] { info, key, ok, cancel });
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                return form.ShowDialog(owner) == DialogResult.OK ? key.Text.Trim() : null;
            }
        }
    }
}
