# SuperPuTTY

SuperPuTTY turns PuTTY into a tabbed, dockable session manager. It starts PuTTY (or KiTTY, Mintty, Cygterm, a
VNC viewer) as a normal program and embeds its window as a tab, so dozens of SSH, Telnet or serial sessions can
be arranged, saved and restored in one main window.

This is the **Kraus-Ahma IT Consulting edition**: the original SuperPuTTY, ported to .NET 10, with tools for
day-to-day administration of many Linux hosts.

!!! tip "Help for the window you are in"
    Press ++f1++ in a pane or dialog (Sessions, Host Info, Containers, Run on Hosts …) to open its page.

## What you can do

| Topic | What it covers |
|---|---|
| [Sessions](sessions.md) | Organise sessions in folders, search them, reopen the last ten with one click |
| [Tabs and layouts](tabs-and-layouts.md) | Dock terminals side by side, save arrangements, see prod/qual/dev at a glance |
| [Icon bar](icon-bar.md) | Every tool as an icon — choose and order them yourself |
| [Command bar](command-bar.md) | Send a command to one, several or all terminals; type into all of them at once |
| [Commands pane](commands.md) | A library of reusable commands with placeholders, per host, folder or for all |
| [Run on Hosts](run-on-hosts.md) | Run one command on many hosts in the background and compare the results |
| [Host Info](host-info.md) | Live CPU, memory, disks, GPUs, users and processes of the active tab's host |
| [Containers](containers.md) | Docker containers of the host: shells, logs, start/stop, open ports in the browser |
| [Health](health.md) | Disk, memory, load, failed units and containers checked; notified when a host turns red |
| [SSH tunnels](ssh-tunnels.md) | Tunnels of every tab with their state |
| [File transfer](file-transfer.md) | Drag files onto a tab, copy between PC and hosts or from host to host |
| [Copy, logs, Claude](terminal-text.md) | Copy a session or the last command, keep session logs, ask Claude about output |

## Requirements in short

- Windows 10/11 x64 with the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).
- PuTTY or KiTTY. PSCP for file transfer; **plink** (comes with PuTTY) for everything that runs in the
  background — Host Info, Containers, health checks, Run on Hosts. These background features need
  **key-based login** (e.g. Pageant) and a host key PuTTY has already accepted; see
  [Troubleshooting](troubleshooting.md).
