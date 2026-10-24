# Containers

**View → Containers**. The docker containers on the **active tab's host**, grouped by compose project.

## The list

Each container shows a **status dot** — green running/healthy, amber starting/restarting/paused, red
unhealthy or crashed, grey stopped — its name (without the project prefix), its status and uptime, the image,
and the **published ports** as small chips (`9089→8080`). Project headers count running containers and
problems. The stop icon in the toolbar also shows stopped containers.

## Actions

| Action | Effect |
|---|---|
| Double-click / ++enter++ | Shell inside the container in a new tab (bash, else sh) |
| Click a **port chip** | Opens the port in the browser |
| Right-click | Root shell, follow logs, inspect, **Open in Browser**, start/stop/restart (asks first), copy name |
| ++f5++ / refresh icon | Reload |

## Opening ports in the browser

A click on a port chip opens `http://host:port` directly when your PC can reach it. Otherwise — the port is
bound to the host's `127.0.0.1`, blocked by a firewall, or the host is behind a jump host — SuperPuTTY opens an
**SSH tunnel** and opens the browser on `localhost`. Tunnels are reused and closed when SuperPuTTY exits. Ports
443 and 8443 open with `https`.

## Requirements

The list and the actions run through plink: **key-based login** and a **known host key**. Without membership in
the `docker` group, SuperPuTTY tries `sudo -n docker`; container tabs then use `sudo` and may ask for the password.
