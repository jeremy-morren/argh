// Bug 86 - The bash completion script needs bash 4 (mapfile), so macOS's system bash 3.2 can't complete anything
// Group E: completion and __schema
//
// The bash template (CompletionScriptTemplates.GetBash) reads candidates with
//     mapfile -t COMPREPLY < <(myapp __complete bash -- "${COMP_WORDS[@]:1}")
// mapfile/readarray exist only in bash 4+. macOS ships /bin/bash 3.2, so every completion fails with
//     bash: mapfile: command not found
// Repro:    on macOS /bin/bash, `eval "$(Bugs __completion bash)"`, then complete anything.
// Expected: works on bash 3.2   (cobra's bash script avoids mapfile and reads with a `while IFS='' read -r` loop)
//
// Confirmed live by the completion-test CI job (branch completion-test, session argh-a5):
// https://github.com/jeremy-morren/argh/actions/runs/39528730773

namespace Bugs;

internal static class Bug86
{
	// No code: the bug is in the generated bash script. Print it with: Bugs.exe __completion bash
}
