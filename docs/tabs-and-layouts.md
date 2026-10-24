# Tabs and layouts

## Arranging terminals

Every session opens as a tab. Drag a tab to dock it beside or below others, or out of the window to float it.
Tool panes (Sessions, Commands, Host Info …) dock the same way.

## Layouts

A **layout** saves the arrangement of open sessions and panes. **File → Save Layout / Save Layout As**, the
**Layouts** pane lists them. A layout can be restored on start-up (Options) or from the command line
(`-layout Name`).

## Tab markers

| Marker | Meaning |
|---|---|
| Coloured bar | Environment: prod red (with a red **!**), qual amber, dev green |
| `↔2` | The session opens 2 SSH tunnels; `↔2!` = one of them isn't working — see [SSH tunnels](ssh-tunnels.md) |
| `●` | Something changed in a background tab (title changed, e.g. a command finished) |
| `×` | The connection dropped |

### Environment markers

Production tabs get a red bar and badge, and commands sent to several terminals ask for confirmation when
production hosts are among them.

### Background activity

**View → Mark Background Tab Activity** (on by default). When SuperPuTTY isn't the active window, a Windows
notification appears as well. Most shells only change the title at the prompt; to see when a long command
finishes, let bash show the running command in the title (`~/.bashrc` on the server):

```bash
trap 'printf "\033]0;%s: %s\007" "${HOSTNAME%%.*}" "$BASH_COMMAND"' DEBUG
```

## Reconnect

When a connection drops, the tab is marked `×` and a yellow bar offers **Reconnect**. With **Options → GUI → Reconnect
dropped sessions automatically** SuperPuTTY retries after 5, 10, 20, 40 and 60 seconds (up to six times). Key and
Pageant logins reconnect silently; password sessions ask again.

## Kiosk mode

++f11++ (or the kiosk icon) shows only the terminal tabs, full screen. ++f11++ again restores everything.
