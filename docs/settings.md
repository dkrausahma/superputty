# Settings

## Options

**Tools → Options** (++f2++):

| Tab | Contains |
|---|---|
| General | Locations of PuTTY/KiTTY, PSCP, Mintty, VNC viewer, WinSCP, FileZilla; PuTTY default parameters; the data folder; default layout |
| GUI | Session tree, tabs and docking, session panel, command bar history, file transfer prefixes, **Security** (Pass stored passwords to PuTTY), tray and other options, **Reconnect dropped sessions automatically** |
| Shortcuts | Keyboard shortcuts for actions |
| Advanced | Application behaviour (single instance and more), focus handling |

The **View** menu holds the switches for health checks, notifications, session logs, background activity,
toolbars and kiosk mode.

## Where things are stored

| What | Where |
|---|---|
| Settings | `SuperPuTTY.settings` in your user folder (`%USERPROFILE%`) |
| Sessions | `Sessions.XML` in the data folder (default `Documents\SuperPuTTY`) |
| Layouts | `layouts\` in the data folder |
| Commands | `Commands.XML` in the data folder |
| Session logs | `logs\` in the data folder |

**Portable:** put `SuperPuTTY.settings` next to `SuperPutty.exe` (and no copy in your user folder) — the
installation then keeps its settings with it.

## Back up and restore

**File → Back up Settings…** writes one zip with the settings, sessions, commands and layouts (no logs).
**File → Restore Settings…** saves the current state to `backups\` in the data folder first and applies the
backup on the next start. Stored passwords only decrypt for the same Windows user.

## Pageant indicator

The status bar shows whether **Pageant** is running and how many keys it holds: green dot with the number of
keys, amber when it has no keys (or doesn't answer), grey when it isn't running.
