# Sessions and Session Detail

## The Sessions pane

**View → Sessions** (or the tree icon in the icon bar). Sessions are organised in folders; double-click opens a
session, right-click offers new sessions and folders, rename, connect all sessions of a folder, health checks
and [Run Command on Hosts](run-on-hosts.md).

- **Search**: type into the box at the top and press ++enter++ (the ✕ button clears it).
- **Recent sessions**: the clock button in front of the search box lists the **last 10 opened sessions**, newest
  first, each once, with its folder on the right. A click opens it; **Clear List** empties it. The same list is
  available as an icon on the [icon bar](icon-bar.md). Only sessions of the session list count — quick
  connections and command tabs don't.
- **Health colours**: hosts with problems are shown orange or red, folders in the colour of their worst host —
  see [Health](health.md).

## Session Detail

Shows the settings of the session selected in the tree, docked below the Sessions pane. It opens and closes
together with the Sessions pane. Edit a session with right-click → **Edit**.

## Session icons and environments

Each session can have an icon (**Edit → Icon**). The icons named `prod-…`, `qual-…`, `dev-…` mark the
**environment** — shape = role (server, web, database, docker, GPU, …), colour = environment (prod red, qual
amber, dev green). The environment is also taken from folder names such as *Prod*, *Production*, *QA*,
*Staging* or *Dev*.

The environment shows on the [tabs](tabs-and-layouts.md#environment-markers) and makes SuperPuTTY ask before a
command goes to production hosts.

## Stored passwords

A password can be stored with a session. It is saved **encrypted for your Windows user** (it can't be used by
other users or on other computers) and passed to PuTTY through a short-lived file, never on the command line.
Key-based login with Pageant is still the better choice — and the background tools need it.
