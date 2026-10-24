# Copy, logs and Explain in Claude

## Copy a session

**Tools → Copy Session to Clipboard** (or its icon) copies the active terminal's screen and scrollback as plain
text — commands and output, e.g. for a ticket. Only what is still in PuTTY's scrollback is copied (2000 lines by
default, set in the PuTTY profile).

## Copy the last command

**Tools → Copy Last Command** copies only the last command and its output. SuperPuTTY recognises usual prompts
(`user@host:dir$`, `[user@host dir]#`, `user@host dir %`, with an optional `(venv)` in front); without a
recognised prompt the whole session is copied.

## Session logs

**View → Log Sessions to Files** writes every new tab's output to a file in the `logs` folder of the data folder.
The raw `.log` contains terminal control codes; when the tab closes, a readable `.txt` is written next to it.
**Tools → Open Session Log** opens the active tab's log as text, **Open Log Folder** the folder.

!!! note
    Typed passwords aren't echoed by the server and therefore not logged — but everything the terminal
    displayed is.

## Explain in Claude

**Tools → Explain in Claude…** takes the last command and its output and shows it in an editable window —
**nothing is sent until you press Send**. Remove anything confidential first. Claude's explanation streams into
the window, in the language of your question.

Needs an Anthropic API key: **API Key…** in that window stores it encrypted for your Windows user; otherwise the
`ANTHROPIC_API_KEY` environment variable is used.
