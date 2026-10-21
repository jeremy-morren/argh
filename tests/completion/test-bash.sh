#!/usr/bin/env bash
# Tests the app's bash completion in the bash that runs this script (CI runs /bin/bash, which is 3.2 on macOS).
# It loads the script the way the docs say, eval "$(app __completion bash)", then calls the function that
# `complete` registered with COMP_WORDS, COMP_CWORD, COMP_LINE and COMP_POINT set, as bash does on Tab.
# Usage: bash test-bash.sh <target> [app]    e.g. bash test-bash.sh ubuntu-bash
# Keep this file bash 3.2 compatible: no mapfile, no associative arrays.
set -u
target=$1
app=${2:-Basic}
dir=$(cd "$(dirname "$0")" && pwd)
failures=0

echo "bash ${BASH_VERSION}, target ${target}"

script=$("$app" __completion bash) || { echo "FAIL: '$app __completion bash' exited with an error"; exit 1; }
if ! printf '%s\n' "$script" | "$BASH" -n; then
	echo "FAIL: bash can't parse the completion script"
	exit 1
fi
echo "ok   script parses"

eval "$script"
spec=$(complete -p "$app" 2>/dev/null) || { echo "FAIL: the script registered no completion for '$app'"; exit 1; }
fn=${spec##*-F }
fn=${fn%% *}

while IFS='|' read -r name line expected; do
	case $name in '' | '#'*) continue ;; esac

	read -r -a COMP_WORDS <<<"$line"
	if [[ $line == *' ' ]]; then COMP_WORDS+=(""); fi
	COMP_CWORD=$((${#COMP_WORDS[@]} - 1))
	COMP_LINE=$line
	COMP_POINT=${#line}
	COMPREPLY=()
	"$fn" "$app" "${COMP_WORDS[COMP_CWORD]}" "${COMP_WORDS[COMP_CWORD - 1]}" </dev/null

	missing=""
	for word in $expected; do
		found=0
		for candidate in ${COMPREPLY[@]+"${COMPREPLY[@]}"}; do
			if [[ $candidate == "$word" ]]; then found=1; fi
		done
		if [[ $found == 0 ]]; then missing="$missing $word"; fi
	done

	got=$(printf '%q ' ${COMPREPLY[@]+"${COMPREPLY[@]}"})
	known=$(grep -E "^${target}[[:space:]]+${name}([[:space:]]|$)" "$dir/known-failures.txt" | head -n 1)
	if [[ -z $missing && -z $known ]]; then
		echo "ok   $name: [$line] -> $got"
	elif [[ -z $missing ]]; then
		echo "FAIL $name: passes now; remove it from known-failures.txt ($known)"
		failures=$((failures + 1))
	elif [[ -n $known ]]; then
		echo "known $name: [$line] -> $got (missing:$missing) -- $known"
	else
		echo "FAIL $name: [$line] -> $got (missing:$missing)"
		failures=$((failures + 1))
	fi
done <"$dir/cases.txt"

exit $((failures > 0))
