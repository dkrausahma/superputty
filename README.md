# SuperPuTTY Application

SuperPuTTY is a Windows application that turns PuTTY into a tabbed, dockable session manager. It starts
PuTTY (or KiTTY, Mintty, Cygterm, a VNC viewer) as a normal process and embeds that process's window as a
tab inside one main window, so dozens of SSH, Telnet or serial sessions can be arranged, saved and restored
side by side instead of living in separate desktop windows.

This repository is a maintained fork of [jimradford/superputty](https://github.com/jimradford/superputty),
ported from .NET Framework 4.x to **.NET 10**, with dependencies moved to NuGet and a WiX 5 installer.
Behaviour and settings files are compatible with the upstream 1.4.0.9 release.

## The problem it solves

PuTTY opens one window per connection and has no notion of tabs, groups or saved workspaces. Administrators
working with many hosts end up juggling windows. SuperPuTTY keeps PuTTY itself (and its saved sessions, keys
and Pageant integration) but adds the window management on top.

## Key features

- **Tabs and docking** — sessions open as tabs that can be split, docked side by side or floated
  (DockPanelSuite).
- **Layouts** — save the current arrangement of open sessions and restore it later, optionally on start-up
  or automatically on exit.
- **Session tree** — sessions organised in folders, searchable, importable from PuTTY's registry, PuTTY
  Connection Manager or a SuperPuTTY XML export.
- **Protocols** — SSH, Telnet, Rlogin, Raw, Serial (via PuTTY/KiTTY), Cygterm, Mintty and VNC.
- **Command bar** — send a command or keystrokes to one, several or all open sessions at once. It remembers
  the last commands per session and login and shows those of the active tab.
- **SPSL scripting** — small script language (`SENDLINE`, `SENDKEY`, `SLEEP`, `OPENSESSION`, …) to run on
  connect or from the script editor.
- **File transfer** — PSCP-based file browser with drag-and-drop transfers; launch FileZilla or WinSCP for
  a session.
- **Icon bar** with toggle buttons (pressed while active, click again to undo): show/close the Sessions pane (with Session Detail below it), show/close the Layouts
  pane, and toggle **kiosk mode** (F11; full screen, menus and panels hidden, only terminal tabs visible).
- **Production at a glance** — terminal tabs carry a coloured bar for their environment (prod red with a
  warning badge, qual amber, dev green); running a command in several terminals warns when production
  systems are among them.
- **Session icons by environment and role** — 44 icons: the shape shows the role (server, web, database,
  docker, GPU, app, storage, security, mail, monitoring, jump host), the colour the environment (prod red,
  qual amber, dev green, or neutral blue). Pick them in the session's settings.
- **Commands pane** — a library of frequently used commands, grouped by category with a Favorites folder at the top, and filterable. One
  click types a command into the active terminal, a double click types and runs it. Starts with common
  Linux commands; add, edit and delete your own — for all sessions, a folder of the session tree or a
  single session (the pane shows what applies to the active tab). Placeholders like `{{service}}` are
  asked for before sending and remembered per session; ready-made Docker Compose commands remember each
  host's project directory.
- **GPUs at a glance** — on hosts with NVIDIA GPUs, Host Info shows utilisation, VRAM and temperature per GPU and
  which processes hold GPU memory.
- **Host Info pane** — live facts about the active tab's host: name, OS, public and private addresses, uptime,
  CPU, load, memory, swap, disks, logged-in users and the busiest processes, updated every 5 seconds over a single
  SSH connection; Copy puts it on the clipboard as text. Opens as a tab in front of Session Detail.
- **Containers pane** — the docker containers on the active tab's host, grouped by compose project, with a
  status dot (green healthy/running, amber starting, red unhealthy/failed), uptime and published ports at a glance.
  Double-click opens a shell inside a container in a new tab; follow logs, inspect, start/stop/restart
  from the context menu. Uses plink with your PuTTY profile and Pageant keys.
- **SSH tunnels on the tab** — tabs show how many tunnels their session opens (`web01 ↔2`), with `!`
  when a local tunnel isn't listening or its port is taken (e.g. by another session); the tab tooltip
  lists every tunnel and its state.
- **Upload files** — drag files or folders from Explorer onto a terminal tab (or the upload icon) to copy
  them to that host, by default into the shell's current directory.
- **Transfer files** — copy files and folders between this PC and any session, or directly from one
  host to another (relayed through this PC, so the hosts need no keys for each other).
- **Explain in Claude** — Tools → Explain in Claude shows the last command and its output for review and
  editing, then streams Claude's explanation (opt-in; API key from `ANTHROPIC_API_KEY` or stored encrypted).
- **Live input** — the keyboard button in the command bar types what you type in one tab into all tabs that
  accept commands (ClusterSSH style); production tabs ask first.
- **Reconnect** — a dropped connection marks the tab (×) and shows a Reconnect bar above the terminal;
  optionally reconnects automatically (Options → GUI, off by default).
- **Pageant indicator** — the status bar shows whether Pageant runs and how many keys it holds
  (green: keys loaded, amber: no keys, grey: not running).
- **Health badges** — the session tree marks hosts with full disks, high load or memory, failed systemd
  units or unhealthy containers (orange/red, details in the tooltip). Open tabs are checked every five
  minutes; any session or folder on demand.
- **Copy last command** — copies just the last command and its output; with **session logs** switched on,
  every tab's output is also written to a file you can open as clean text.
- **Background tab activity** — a background tab whose title changes gets a `●` marker (and a Windows
  notification when SuperPuTTY isn't active), e.g. when a long build finishes. Most shells only change the
  title at the prompt; to see when a command finishes, let bash show the running command in the title:
  ```bash
  # ~/.bashrc on the server (interactive shells): running command in the window title
  trap 'printf "\033]0;%s: %s\007" "${HOSTNAME%%.*}" "$BASH_COMMAND"' DEBUG
  ```
- **Tunnel overview and folder commands** — Tools → SSH Tunnels lists the tunnels of all open tabs; the
  Commands pane can run a command in all open tabs below a folder of the session tree.
- **Copy session to clipboard** — one click copies the active terminal's screen and scrollback (commands
  and output) as plain text, e.g. to paste into a chat or ticket.
- **Quick selector, keyboard shortcuts, global hot keys, tab switcher.**
- **Single-instance mode** — further launches (e.g. from `ssh://` links or the command line) open in the
  running instance.
- **Portable mode** — keep the settings file next to the executable.
- **Backup and restore** — File → Back up Settings puts settings, sessions, commands and layouts into one zip;
  Restore Settings brings them back on the next start (after saving the current state).
- **Protected passwords** — stored session passwords are encrypted for your Windows user and passed to PuTTY
  through a short-lived file, never on the command line (key-based login with Pageant stays the better choice).

## Quick start (users)

1. Install the [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0) and
   [PuTTY](https://www.chiark.greenend.org.uk/~sgtatham/putty/) (or KiTTY). PSCP is needed for the file
   browser.
2. Install `SuperPuttySetup.msi`, or copy a build output folder anywhere.
3. On first start SuperPuTTY asks for the location of `putty.exe` / `pscp.exe` and the folder for sessions
   and layouts (default `Documents\SuperPuTTY`).
4. Import your PuTTY sessions via *File → Import Sessions*, or create new ones in the session tree.

Command line: `SuperPutty.exe --help` lists the options, for example
`SuperPutty.exe -layout MyLayout` or `SuperPutty.exe -ssh -P 22 -l user -load MyPuttySession host`.

## Quick start (developers)

Requirements: Windows, the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), optionally
Visual Studio 2022/2026 for the WinForms designer.

```bash
git clone ssh://git@bitbucket.brandcompete.com/dkait/dkait-superputty.git
cd dkait-superputty
dotnet build SuperPutty.sln -c Debug
bin\Debug\SuperPutty.exe

# tests without network access
dotnet test SuperPuttyUnitTests/SuperPuttyUnitTests.csproj --filter "TestCategory!=NetworkTest"

# installer (x64 MSI)
dotnet build SuperPuttyInstaller -c Release
```

Architecture, configuration, the full command reference and known pitfalls are in
[development.md](development.md).

### License and credits
MIT. This edition (the .NET 10 port and the new features) is developed by **Kraus-Ahma IT Consulting**
and builds on the work of others:

- **SuperPuTTY** — Copyright (c) 2009–2018 Jim Radford and contributors, MIT —
  https://github.com/jimradford/superputty
- **PuTTY** — Simon Tatham and the PuTTY team, MIT — https://www.chiark.greenend.org.uk/~sgtatham/putty/
  (PuTTY itself is not included; SuperPuTTY embeds your installed PuTTY/KiTTY.)
- **DockPanel Suite** (MIT), **Apache log4net** (Apache 2.0), **Anthropic C# SDK** and
  Microsoft.Extensions.AI.Abstractions (MIT), **FamFamFam Silk icons** (CC BY).

See `License.txt` and Help → About in the application.

### Upstream project

The original project, its issue tracker, documentation and historic releases are on GitHub:

- Project: https://github.com/jimradford/superputty
- Documentation: https://github.com/jimradford/superputty/wiki/Documentation
- Releases: https://github.com/jimradford/superputty/releases (latest upstream: 1.4.0.9, .NET Framework)
- Facebook page: https://www.facebook.com/superputty

The in-app update check of this edition points to its own release location (placeholder until published).
