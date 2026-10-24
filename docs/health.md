# Health checks and notifications

## What is checked

| Check | Warning (orange) | Critical (red) |
|---|---|---|
| Disk usage per filesystem | from 80 % | from 90 % |
| Memory | from 85 % | from 95 % |
| Load per CPU | from 1 | from 2 |
| Failed systemd units | any | — |
| Unhealthy docker containers | — | any |

Problem hosts are coloured in the session tree; folders take the colour of their worst host. The tooltip of a
host lists the problems and the last values.

## When hosts are checked

- **Open tabs**: automatically, each host at most every five minutes (**View → Health Checks for Open Tabs**).
  Every check is an SSH login and appears in the host's auth log, hence the interval.
- **Any session or folder**: right-click → **Check Health**.
- **[Host Info](host-info.md)** updates the host it shows continuously.

## Notifications

**View → Notify When a Host Turns Critical** (on by default): a Windows notification when a host **becomes
critical**, and another when it **recovers**. Warnings don't notify, a failed check doesn't count as recovery,
and a host that stays critical is reported once. Several changes at the same time (e.g. after start-up) come as
one notification.
