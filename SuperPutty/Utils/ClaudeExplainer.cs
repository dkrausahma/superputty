using System;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using log4net;

namespace SuperPutty.Utils
{
    /// <summary>
    /// "Explain in Claude": sends terminal text the user has reviewed to the Claude API (official Anthropic SDK) and
    /// streams the explanation back. The API key comes from SuperPuTTY's settings (DPAPI-encrypted) or the
    /// ANTHROPIC_API_KEY environment variable. Nothing is sent without the preview dialog.
    /// </summary>
    public static class ClaudeExplainer
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ClaudeExplainer));

        public const string ModelName = "claude-opus-5-5";
        public const string EnvironmentVariable = "ANTHROPIC_API_KEY";

        /// <summary>Where the key comes from, for the dialog</summary>
        public enum KeySource { None, Settings, Environment }

        public const string DefaultQuestion = "Explain this terminal output. What went wrong, and what should I do next?";

        public const string SystemPrompt =
            "You help an administrator who works in SSH terminals (Linux and Unix servers, Docker, Kubernetes, network devices). " +
            "The user sends text copied from a terminal, usually the last command and its output, together with a question. " +
            "Explain briefly what happened and what the output means, and point out errors with their most likely causes. " +
            "Then suggest concrete next steps, with commands on their own lines. Be concise: the answer is shown in a small " +
            "plain-text window, so use short paragraphs and simple lists, no tables. If the text contains something that looks " +
            "like a secret (password, token, private key), say so and recommend rotating it. " +
            "Answer in the language of the user's question.";

        /// <summary>The API key to use: the one stored in SuperPuTTY wins over the environment variable</summary>
        public static string ApiKey(string storedProtectedKey, out KeySource source)
        {
            string stored = null;
            try
            {
                stored = Dpapi.Unprotect(storedProtectedKey);
            }
            catch (Exception ex)
            {
                Log.Warn("Stored Anthropic API key can't be decrypted (other Windows user?)", ex);
            }
            if (!string.IsNullOrWhiteSpace(stored))
            {
                source = KeySource.Settings;
                return stored.Trim();
            }
            string environment = Environment.GetEnvironmentVariable(EnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(environment))
            {
                source = KeySource.Environment;
                return environment.Trim();
            }
            source = KeySource.None;
            return null;
        }

        /// <summary>The user message: the question and the terminal text, clearly separated</summary>
        public static string BuildMessage(string question, string terminalText, string sessionName)
        {
            string q = string.IsNullOrWhiteSpace(question) ? DefaultQuestion : question.Trim();
            return q + "\n\n<terminal" + (string.IsNullOrWhiteSpace(sessionName) ? string.Empty : " session=\"" + sessionName.Replace("\"", "'") + "\"") + ">\n" +
                (terminalText ?? string.Empty).TrimEnd() + "\n</terminal>";
        }

        /// <summary>Streams the explanation; <paramref name="onText"/> gets each piece of text as it arrives (any thread)</summary>
        /// <returns>null on success, otherwise a message for the user</returns>
        public static async Task<string> Explain(string apiKey, string message, Action<string> onText, CancellationToken cancel)
        {
            AnthropicClient client = new AnthropicClient { ApiKey = apiKey };
            MessageCreateParams parameters = new MessageCreateParams
            {
                Model = ModelName,
                MaxTokens = 16000,
                System = SystemPrompt,
                Messages = [new() { Role = Role.User, Content = message }]
            };
            try
            {
                bool refused = false;
                await foreach (RawMessageStreamEvent streamEvent in client.Messages.CreateStreaming(parameters, cancel))
                {
                    if (streamEvent.TryPickContentBlockDelta(out var delta) && delta.Delta.TryPickText(out var text))
                    {
                        onText(text.Text);
                    }
                    else if (streamEvent.TryPickDelta(out var messageDelta) && messageDelta.Delta.StopReason == StopReason.Refusal)
                    {
                        refused = true;
                    }
                }
                return refused ? "Claude declined to answer this request." : null;
            }
            catch (OperationCanceledException)
            {
                return "Cancelled.";
            }
            catch (AnthropicUnauthorizedException)
            {
                return "The API key was rejected. Check it with \"API Key…\" or the " + EnvironmentVariable + " environment variable.";
            }
            catch (AnthropicApiException ex)
            {
                Log.Warn("Claude API error", ex);
                return "The Claude API returned an error: " + ex.Message;
            }
            catch (Exception ex)
            {
                Log.Warn("Claude request failed", ex);
                return "The request failed: " + ex.Message;
            }
        }
    }
}
