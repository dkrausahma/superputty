# Commands pane

**View → Commands**. A library of reusable commands, grouped by category, with a filter box.

## Using a command

| Action | Effect |
|---|---|
| Click | Types the command into the active terminal (without Enter) |
| Double-click / ++enter++ | Types and runs it |
| ++shift+enter++ | Types only |
| Right-click | Selects it and opens the menu: Run in New Tab, Run in All Terminals, Run in Terminals of Folder, Add to Favorites, Edit, Delete, Move Up/Down, Import, Export |

A named command shows its name and, below it in grey, the command itself. The green ▶ marks the row under the
mouse.

## Where a command applies

Each command applies to **all sessions**, a **folder** of the session tree or **one session**. The pane shows the
commands for the active tab in sections — *This session*, its folders, *All sessions*. Commands of deleted
sessions or folders appear under *Not assigned* and can be reassigned with Edit.

## Placeholders

`{{name}}` or `{{name:default}}` in a command are asked for before it is sent, e.g.

```
docker compose --project-directory {{compose dir:~}} logs -f {{service}}
```

The values are remembered per session. Placeholders are shown in purple. Docker's own templates such as
`{{.Names}}` are left alone.

## Keeping jobs running after you disconnect

The default library has a **Background** category for work that must survive closing the PuTTY tab:

- **tmux: new session** / **tmux: attach** — the clean way: start long work inside tmux, leave it with
  ++ctrl+b++ ++d++, attach again later with the output still there. **screen** works the same with ++ctrl+a++ ++d++.
- **Stopped job to background (after Ctrl+Z)** — a job already running in the foreground: press ++ctrl+z++, then
  run this entry (`bg %+ && disown -h %+`); the job continues and survives closing the session. Its output still goes
  to the closed terminal, which some programs don't survive — prefer tmux or nohup for new work.
- **Start with nohup** — start a command with its output in a log file; it survives the logout.
- **Run as a systemd unit** — independent of the session, logged in the journal.
- **Pull a running process into tmux (reptyr)** — rescue a running job into a tmux session (needs the `reptyr`
  package).

Libraries from older versions get this category once when SuperPuTTY updates them.

## Favorites, import and export

**Favorites** is always listed first. Import adds commands from another `Commands.XML` (skipping existing ones),
Export writes a section or all commands. The library is stored in `Commands.XML` in the data folder.
