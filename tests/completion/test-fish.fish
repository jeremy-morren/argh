#!/usr/bin/env fish
# Tests the app's fish completion. It installs the script the way the docs say, as <app>.fish in a
# completions directory that fish autoloads, then asks fish's own engine with complete --do-complete,
# which is what Tab uses.
# Usage: fish --no-config test-fish.fish <target> [app]    e.g. fish --no-config test-fish.fish ubuntu-fish
set -l target $argv[1]
set -l app Basic
if test (count $argv) -ge 2
    set app $argv[2]
end
set -l dir (status dirname)
set -l failures 0

echo "fish $version, target $target"

set -l completions (mktemp -d)
if not $app __completion fish >$completions/$app.fish
    echo "FAIL: '$app __completion fish' exited with an error"
    exit 1
end
if not fish --no-execute $completions/$app.fish
    echo "FAIL: fish can't parse the completion script"
    exit 1
end
echo "ok   script parses"

set -p fish_complete_path $completions

set -l known_failures (string match -rv '^(#|$)' <$dir/known-failures.txt)
for entry in (string match -rv '^(#|$)' <$dir/cases.txt)
    set -l parts (string split '|' -- $entry)
    set -l name $parts[1]
    set -l line $parts[2]
    set -l expected (string split -n ' ' -- $parts[3])

    # Each result is "candidate", or "candidate<TAB>description".
    set -l got (complete --do-complete "$line" | string replace -r '[[:space:]].*' '')
    set -l missing
    for word in $expected
        contains -- $word $got; or set -a missing $word
    end

    # Quote the regex parts: unquoted, fish would read $target[...] as a list index.
    set -l known (string match -r '^'"$target"'[[:space:]]+'"$name"'(?:[[:space:]].*)?$' -- $known_failures | head -n 1)
    set -l shown (string join ' ' -- (string escape -- $got))
    if test (count $missing) -eq 0; and test -z "$known"
        echo "ok   $name: [$line] -> $shown"
    else if test (count $missing) -eq 0
        echo "FAIL $name: passes now; remove it from known-failures.txt ($known)"
        set failures (math $failures + 1)
    else if test -n "$known"
        echo "known $name: [$line] -> $shown (missing: $missing) -- $known"
    else
        echo "FAIL $name: [$line] -> $shown (missing: $missing)"
        set failures (math $failures + 1)
    end
end

test $failures -eq 0
