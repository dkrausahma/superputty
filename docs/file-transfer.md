# File transfer

All transfers use **PSCP** (set its location under Options) and run in the background; progress and result
appear in the status bar.

## Upload by drag and drop

Drop files or folders from Explorer onto a terminal **tab header** (or the upload icon). A dialog confirms the
target folder — preset with the shell's current directory when the window title shows it (`user@host: ~/dir`,
as Debian, Ubuntu and Fedora set it). Click the upload icon to pick files instead.

## Transfer Files

**Tools → Transfer Files…** (or its icon): source and target are each **This PC** or a session, preset with the
active tab and its current directory.

- PC → host: upload. Host → PC: download.
- **Host → host**: copied through this PC (a temporary folder, deleted afterwards), so the hosts need no keys for
  each other.

## File browser and external tools

The session menu offers a PSCP-based file browser and, if installed, **WinSCP** or **FileZilla** for the
session.
