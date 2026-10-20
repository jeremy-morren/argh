# Shell completions

Tab completion for subcommands, namespaces, and flags is included out of the box. The source generator emits **lookup tables** at compile time (same model as routing and `--help`), and a small `__complete` handler answers the shell with one candidate per line.

:::{note}
`--completions` is not reserved. Use `__completion` / `__complete` only for Argh's integration.
:::

## Commands

| Command | Purpose |
|---------|---------|
| `myapp __completion bash\|zsh\|fish\|powershell` | Print an install snippet (substitutes your executable name). |
| `myapp __complete <shell> -- <words...>` | Return completion candidates; `words` are argv after the program name. |

## Installation

::::tab-set
:::tab-item Bash
:sync: bash

```bash
eval "$(myapp __completion bash)"
```

Add to `~/.bashrc` to persist.

:::
:::tab-item Zsh
:sync: zsh

```bash
source <(myapp __completion zsh)
```

Add to `~/.zshrc` to persist.

:::
:::tab-item Fish
:sync: fish

Fish 3.4+ required (for `commandline -opc`):

```sh
mkdir -p ~/.config/fish/completions
myapp __completion fish > ~/.config/fish/completions/myapp.fish
```

:::
:::tab-item PowerShell
:sync: powershell

Works in PowerShell 7+ (`pwsh`) and Windows PowerShell 5.1 (`powershell`). `pwsh` is accepted as an alias for the shell name.

Persist it by writing the script into your profile once. The script is a small, fixed stub that calls back into `myapp`, so it never needs regenerating and doesn't start `myapp` when the shell starts:

```powershell
New-Item -ItemType Directory -Force (Split-Path $PROFILE.CurrentUserAllHosts) | Out-Null
myapp __completion powershell | Add-Content $PROFILE.CurrentUserAllHosts
```

Or enable it for the current session only:

```powershell
myapp __completion powershell | Out-String | Invoke-Expression
```

Completion also works for `myapp.exe` and for paths such as `.\bin\myapp.exe`, so you can try it before the tool is on `PATH`. Matching ignores case. With `Set-PSReadLineKeyHandler -Key Tab -Function MenuComplete`, you pick from a menu and a trailing space is added for you.

As with cobra and clap, the completer evaluates the line up to the cursor as PowerShell, so variables and `$(...)` resolve to the values the command would get. A `$(...)` on the line runs each time you press Tab.

Windows PowerShell 5.1:

- Its default execution policy on Windows clients doesn't run profiles. Run `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` once.
- It never invokes argument completers for a bare `-` or `--`. Type one more character (`--v<Tab>`) to complete a flag name.

:::
::::

## How it works

The generator emits completion lookup tables alongside routing and help printers. When the shell invokes `__complete`, the handler walks the command tree using the provided context words and returns candidates that start with the partial word, ignoring case, one per line.

Completions cover:

- Subcommand and namespace names
- Flag names (including aliases and `--help`)
- Flag values with a fixed set of choices: enums (using `[EnumValue]` names), collections of enums, and `[AllowedValues]`. Values complete after `--flag ` and inside `--flag=`.
