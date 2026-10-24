# Troubleshooting

## Background tools fail (Host Info, Containers, health, Run on Hosts)

These tools log in with **plink** in batch mode: they never ask, they fail.

| Message | Cause and fix |
|---|---|
| *plink.exe was not found* | Install PuTTY with its tools; plink must be next to `putty.exe` or on the PATH |
| *The host key is not known yet* / *Cannot confirm a host key in batch mode* | Open the session once as a normal tab and accept the host key |
| *Background login failed* / *Access denied* | Use key-based login: load the key into Pageant (status bar shows the key count) or set `-i keyfile` in the session's extra arguments |
| *No access to docker on this host* | Add the user to the `docker` group, or allow `sudo docker` without a password |
| `bash: <host>: command not found` | Fixed since 1.5.1: happened with sessions whose PuTTY profile stores the host name |

Passwords are only passed to plink when **Options → GUI → Security → Pass stored passwords to PuTTY** is on — keys are
the better way.

## A tab stays empty or PuTTY opens outside

SuperPuTTY embeds the program's window after it appears. Programs that open their window very late or start a
second process (e.g. newer Mintty) may not be caught. Check the executable paths under Options.

## Drag and drop onto the terminal does nothing

PuTTY's window belongs to another program, so Windows may not pass drops on it to SuperPuTTY. Drop onto the
**tab header** or the upload icon instead.

## Log files

SuperPuTTY writes a log to `%TEMP%\SuperPuTTY.<date_time>.log`. **View → Log Viewer** shows it live inside
SuperPuTTY; **Help → Diagnostics** shows version and environment details. Attach both to a bug report.
