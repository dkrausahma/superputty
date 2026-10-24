# Scripts (SPSL)

SPSL is a small script language for sending input to terminals — for a login sequence when a session opens, or
from the script editor.

## Commands

One command per line; `#` starts a comment.

| Command | Effect |
|---|---|
| `SENDLINE text` | Types the text and Enter |
| `SENDCHAR text` | Types the text without Enter |
| `SENDKEY key` | Sends a key, e.g. `SENDKEY {ENTER}` |
| `SLEEP ms` | Waits the given milliseconds |
| `OPENSESSION id` | Opens a session by its id (folder/name) |
| `CLOSESESSION` | Closes the session |

## Example

```
# switch to the application user and its directory
SENDLINE sudo -iu app
SLEEP 500
SENDLINE cd /srv/app && git status
```

Set a script for a session in its settings (**Edit → SPSL Script**); it runs when the session opens.
