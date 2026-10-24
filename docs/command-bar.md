# Command bar and live input

## Command bar

The text field next to the icon bar sends a command to terminals. By default it goes to the active tab; the
selector next to it sends to all tabs that **accept commands** (tab context menu) or to the visible ones.

- ++enter++ sends the command and Enter, the arrow keys pick earlier commands.
- The **history** is kept per session and login (up to 50 entries, saved across restarts; age limit in
  Options, 0 = no limit).
- When production tabs are among the targets, SuperPuTTY asks first.

## Live input

The keyboard button in the command bar switches **live input** on: what you type into one PuTTY tab is typed
into all other tabs that accept commands, key by key (in the style of ClusterSSH). Turning it on needs at least
two targets and asks first when production tabs are among them.

Ctrl and Alt combinations that SuperPuTTY uses itself (e.g. Ctrl+Tab, Alt+F4) stay local.

## Related

- [Commands pane](commands.md): reusable commands, typed or run with a click.
- [Run on Hosts](run-on-hosts.md): run a command on many hosts **without** open tabs and compare the output.
