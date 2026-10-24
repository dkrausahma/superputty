# Host Info

**View → Host Info** (monitor icon). Live facts about the **active tab's host**, opened as a tab in front of
Session Detail.

## What it shows

- **Host name**, operating system and kernel.
- **Public and private addresses** of the host's own network interfaces (container bridges and loopback left
  out — no outside service is asked).
- **Uptime**.
- **CPU** usage with the load average; **memory** with swap; **disks** (one bar per filesystem, the fullest
  first, at most six).
- **GPUs** on hosts with NVIDIA GPUs: utilisation, VRAM and temperature per GPU, and the processes holding GPU
  memory.
- **Logged-in users** (who, terminal, from where, since when), the number of **processes** and the **busiest**
  ones with their CPU share (like `top`, 100 % = one CPU).
- **Failed systemd units** and **unhealthy containers**, if any.

The bars use the colours of the [health checks](health.md): amber and red mean the same everywhere. A GPU bar
turns amber/red with its temperature; VRAM only turns amber when practically full, because inference servers
reserve most of it on purpose.

**Copy** puts everything on the clipboard as text, ready for a ticket or chat.

## Updates

| Values | Refreshed |
|---|---|
| CPU, load, memory, processes, GPUs | every 5 seconds |
| Disks, logged-in users, failed units, containers | every 60 seconds |
| Host name, OS, addresses | once per connection |

Host Info keeps **one SSH connection** open to the host instead of logging in for every refresh, and only while
the pane is visible: it pauses when Host Info is a background tab or SuperPuTTY is minimised, and continues
when you come back. The values also update the host's [health colour](health.md) in the session tree.

If the connection fails, the reason is shown and SuperPuTTY tries again after 30 seconds; **Reconnect**
(++f5++) tries at once.
