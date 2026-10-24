# SSH tunnels

Port forwardings of a session — from its PuTTY profile (*Connection → SSH → Tunnels*) and `-L`, `-R`, `-D` in
its extra arguments — are shown on its tab and checked every five seconds.

## On the tab

| Marker | Meaning |
|---|---|
| `↔2` | Two tunnels, all fine |
| `↔2!` | A local tunnel isn't listening, or its port is held by another program or a tab of another host |

The tab's tooltip lists every tunnel with its state. A port held by another tab of the **same** host (e.g. a
duplicate) counts as fine. Remote tunnels (`-R`) can't be checked from this PC. The first 20 seconds after opening
a tab aren't reported (PuTTY opens the ports after login).

## Overview

**Tools → SSH Tunnels…** lists the tunnels of all open tabs; **Go to Tab** jumps to the tab.

## Temporary tunnels for containers

Opening a container port that isn't reachable directly creates its own tunnel — see
[Containers](containers.md#opening-ports-in-the-browser).
