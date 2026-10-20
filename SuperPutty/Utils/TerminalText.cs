using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SuperPutty.Utils
{
    /// <summary>Plain text from terminal output: control codes removed, last command found by its prompt</summary>
    public static class TerminalText
    {
        /// <summary>CSI (colours, cursor moves), OSC (window titles) and other escape sequences</summary>
        private static readonly Regex EscapeSequences = new Regex(
            @"\x1B\[[0-?]*[ -/]*[@-~]|\x1B\][^\x07\x1B]*(?:\x07|\x1B\\)|\x1B[PX^_][^\x1B]*\x1B\\|\x1B[()][A-Za-z0-9]|\x1B[@-Z\\-_=>]",
            RegexOptions.Compiled);

        /// <summary>
        /// A shell prompt at the start of a line, followed by the command: "user@host:~/dir$ ", "[root@db01 ~]# ",
        /// "user@host ~ % ", optionally with a "(venv) " prefix
        /// </summary>
        private static readonly Regex Prompt = new Regex(
            @"^(?:\([^)]*\)\s*)?(?:\[[^\]]+\]|[\w.-]+@[\w.-]+(?::\S*|\s+\S+)?)\s?[$#%>](?:\s|$)(?<command>.*)$",
            RegexOptions.Compiled);

        /// <summary>
        /// Remove terminal control codes from raw output (e.g. a PuTTY session log): escape sequences are dropped,
        /// and each line is replayed like a terminal does it — backspace moves the cursor left, carriage return to
        /// the line start, and following characters overwrite (so "\b \b" erases, progress bars keep their last state)
        /// </summary>
        public static string Clean(string raw)
        {
            string text = EscapeSequences.Replace(raw ?? string.Empty, string.Empty);
            StringBuilder result = new StringBuilder(text.Length);
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
            {
                result.Append(ReplayLine(line).TrimEnd()).Append("\r\n");
            }
            return result.ToString().TrimEnd() + "\r\n";
        }

        /// <summary>The visible content of one terminal line after cursor movements and overwrites</summary>
        private static string ReplayLine(string line)
        {
            StringBuilder buffer = new StringBuilder(line.Length);
            int cursor = 0;
            foreach (char c in line)
            {
                if (c == '\r')
                {
                    cursor = 0;
                }
                else if (c == '\b')
                {
                    cursor = Math.Max(0, cursor - 1);
                }
                else if (c == '\t' || c >= ' ' && c != '\x7F')
                {
                    if (cursor < buffer.Length)
                    {
                        buffer[cursor] = c;
                    }
                    else
                    {
                        buffer.Append(c);
                    }
                    cursor++;
                }
            }
            return buffer.ToString();
        }

        /// <summary>True if the line starts with a recognised shell prompt</summary>
        public static bool IsPrompt(string line)
        {
            return Prompt.IsMatch(line ?? string.Empty);
        }

        /// <summary>
        /// The last command and its output from a terminal's text: from the last prompt line that has a command up
        /// to (not including) the following prompt. Null if no prompt is recognised.
        /// </summary>
        public static string LastCommand(string text)
        {
            List<string> lines = new List<string>((text ?? string.Empty).Replace("\r\n", "\n").Split('\n'));
            while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0)
            {
                lines.RemoveAt(lines.Count - 1);
            }

            int end = lines.Count;
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                Match match = Prompt.Match(lines[i]);
                if (!match.Success)
                {
                    continue;
                }
                if (match.Groups["command"].Value.Trim().Length == 0)
                {
                    // an empty prompt (the current one): the command block ends before it
                    end = i;
                    continue;
                }
                return string.Join("\r\n", lines.GetRange(i, end - i)).TrimEnd() + "\r\n";
            }
            return null;
        }
    }
}
