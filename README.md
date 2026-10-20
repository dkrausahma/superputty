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
- **Copy session to clipboard** — one click copies the active terminal's screen and scrollback (commands
  and output) as plain text, e.g. to paste into a chat or ticket.
- **Quick selector, keyboard shortcuts, global hot keys, tab switcher.**
- **Single-instance mode** — further launches (e.g. from `ssh://` links or the command line) open in the
  running instance.
- **Portable mode** — keep the settings file next to the executable.

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

### License
MIT

For additional License information please read the License.txt included with the download

### Upstream project

The original project, its issue tracker, documentation and historic releases are on GitHub:

- Project: https://github.com/jimradford/superputty
- Documentation: https://github.com/jimradford/superputty/wiki/Documentation
- Releases: https://github.com/jimradford/superputty/releases (latest upstream: 1.4.0.9, .NET Framework)
- Facebook page: https://www.facebook.com/superputty

Note that the in-app update check still queries the upstream GitHub releases.
