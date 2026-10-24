# Run on Hosts

Run **one command on many hosts** in the background and compare the results in a table — without opening a tab
per host.

## Start

- Right-click a folder in the Sessions pane → **Run Command on Hosts…** — all sessions of that folder.
- **Tools → Run on Hosts…** — the folder of the active tab (all sessions without a tab).

## Run

1. Uncheck hosts you want to leave out. Sessions that aren't SSH (Telnet, serial …) are skipped.
2. Enter the command, e.g. `df -h /` or `docker compose ps`; set the timeout per host.
3. **Run**. Six hosts run at a time; **Stop** ends the rest.

If production hosts are among the checked ones, SuperPuTTY lists them and asks first.

## Results

| Result | Meaning |
|---|---|
| ● ok | Exit code 0 |
| ● exit *N* | The command failed with exit code *N* (its error output is shown after `--- stderr ---`) |
| ● error | No result: login failed, host key unknown, host unreachable, or the timeout passed |
| stopped / not run / skipped | Stopped by you, unchecked, or not an SSH session |

Select a host to see its full output below the table. **Only failed** hides the successful ones. **Copy All**
and **Save…** produce a text report with every host's output.

## Requirements

Runs through plink like the other background tools: **key-based login** (Pageant) and **known host keys**. Hosts
that fail with "Cannot confirm a host key" need one normal connection first.
