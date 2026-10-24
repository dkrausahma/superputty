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

## Favorites, import and export

**Favorites** is always listed first. Import adds commands from another `Commands.XML` (skipping existing ones),
Export writes a section or all commands. The library is stored in `Commands.XML` in the data folder.
