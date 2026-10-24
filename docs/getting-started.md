# Getting started

## Install

1. Install the [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0) and
   [PuTTY](https://www.chiark.greenend.org.uk/~sgtatham/putty/) (or KiTTY). The PuTTY installer also brings
   `pscp.exe` and `plink.exe`.
2. Run `SuperPuttySetup.msi`. A newer version installs over the old one; settings and sessions are kept.

## First start

SuperPuTTY asks for:

- the location of `putty.exe` (or KiTTY) and `pscp.exe`,
- the **data folder** for sessions, layouts and commands (default `Documents\SuperPuTTY`).

Both can be changed later under **Tools → Options**.

## Get your sessions in

- **File → Import Sessions → From PuTTY Settings** takes the sessions saved in PuTTY.
- **From File** imports a SuperPuTTY sessions XML, **From PuTTY CM Export** a PuTTY Connection Manager export.
- Or create them yourself: right-click a folder in the **Sessions** pane → **New Session**.

Double-click a session to open it as a tab.

## Recommended for the background tools

Host Info, Containers, health checks and Run on Hosts log in with plink in the background. Set up once:

- **Key-based login**: load your key into **Pageant**. The status bar shows a green dot with the number of keys
  when Pageant is running with keys.
- **Accept host keys**: connect to each host once with a normal tab, so PuTTY knows its key.

## Command line

`SuperPutty.exe --help` lists the options. Examples:

```
SuperPutty.exe -layout MyLayout
SuperPutty.exe -session "Prod/web01"
SuperPutty.exe -ssh -P 22 -l admin -load MyPuttyProfile web01.example.com
```

With single-instance mode on (**Options → Advanced**), further launches open their session in the running SuperPuTTY.
