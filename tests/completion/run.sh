#!/usr/bin/env bash
# Runs the completion tests for one shell. The app (examples/Basic, published as "Basic") must be on PATH.
# Usage: bash tests/completion/run.sh <os> <shell>
#   os:    ubuntu | macos | windows (only names the target in known-failures.txt)
#   shell: bash | zsh | fish | git-bash | powershell | pwsh
# bash and git-bash run /bin/bash, which is the system bash: 3.2 on macOS, as most macOS users have.
set -euo pipefail
os=$1
shell=$2
dir=$(cd "$(dirname "$0")" && pwd)
target="$os-$shell"

case $shell in
	bash | git-bash) /bin/bash "$dir/test-bash.sh" "$target" ;;
	zsh) zsh -f "$dir/test-zsh.zsh" "$target" ;;
	fish) fish --no-config "$dir/test-fish.fish" "$target" ;;
	powershell) powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$dir/test-powershell.ps1" -Target "$target" ;;
	pwsh) pwsh -NoProfile -File "$dir/test-powershell.ps1" -Target "$target" ;;
	*) echo "unknown shell '$shell'" >&2; exit 2 ;;
esac
