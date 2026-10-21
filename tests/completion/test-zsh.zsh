#!/usr/bin/env zsh
# Tests the app's zsh completion. It loads the script the way the docs say, source <(app __completion zsh),
# after the real compinit, finds the function compdef registered for the app, and calls it with words and
# CURRENT set, as zsh does on Tab. Only compadd is replaced: it works only inside a completion widget, so a
# function records the candidates instead.
# Usage: zsh -f test-zsh.zsh <target> [app]    e.g. zsh -f test-zsh.zsh ubuntu-zsh
target=$1
app=${2:-Basic}
dir=${0:A:h}
integer failures=0

print -r -- "zsh $ZSH_VERSION, target $target"

script=$("$app" __completion zsh) || { print -r -- "FAIL: '$app __completion zsh' exited with an error"; exit 1 }
if ! zsh -n <<<"$script"; then
	print -r -- "FAIL: zsh can't parse the completion script"
	exit 1
fi
print -r -- "ok   script parses"

autoload -Uz compinit && compinit -u -D
source <("$app" __completion zsh)
fn=${_comps[$app]}
[[ -n $fn ]] || { print -r -- "FAIL: compdef registered no completion for '$app'"; exit 1 }

typeset -ga captured
compadd() {
	# Record candidates from "compadd -a array..." and "compadd [options] -- words".
	local arrays=0
	while (( $# )); do
		case $1 in
			-a) arrays=1 ;;
			--) shift; captured+=("$@"); return 0 ;;
			-*) ;;
			*) if (( arrays )); then captured+=("${(@P)1}"); else captured+=("$1"); fi ;;
		esac
		shift
	done
	return 0
}

while IFS='|' read -r name line expected; do
	[[ -z $name || $name == '#'* ]] && continue

	words=(${(z)line})
	[[ $line == *' ' ]] && words+=("")
	CURRENT=$#words
	captured=()
	$fn </dev/null

	missing=()
	for word in ${=expected}; do
		(( ${captured[(Ie)$word]} )) || missing+=("$word")
	done

	got=${(j: :)${(qq)captured}}
	known=$(grep -E "^${target}[[:space:]]+${name}([[:space:]]|$)" "$dir/known-failures.txt" | head -n 1)
	if (( ! $#missing )) && [[ -z $known ]]; then
		print -r -- "ok   $name: [$line] -> $got"
	elif (( ! $#missing )); then
		print -r -- "FAIL $name: passes now; remove it from known-failures.txt ($known)"
		(( failures++ ))
	elif [[ -n $known ]]; then
		print -r -- "known $name: [$line] -> $got (missing: $missing) -- $known"
	else
		print -r -- "FAIL $name: [$line] -> $got (missing: $missing)"
		(( failures++ ))
	fi
done <"$dir/cases.txt"

(( failures == 0 ))
