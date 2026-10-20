// Bug 32 - The zsh script drops the first CLI word
// Group E: completion and __schema
//
// The zsh template (CompletionScriptTemplates.cs:49) uses "${words[@]:2}". In zsh the ${name:offset} form is
// zero-based ("0 refers to the first character or element regardless of the setting of the option KSH_ARRAYS"),
// so offset 2 skips words[1] (the program) AND words[2]. The comment on line 47 says "words[2]..", the 1-based intent.
//
// What zsh sends for `bugs bug29 in<TAB>` (words = bugs bug29 in):
// Run:      Bugs.exe __complete zsh -- in
// Actual:   (nothing: "in" is matched against root names)
// Expected: inner                 (cobra's zsh script passes "${words[2,-1]}", the 1-based subscript form)
//
// It works at the root by accident: `bugs bu<TAB>` sends nothing, the engine prints every root name, zsh filters.
// Print the script:  Bugs.exe __completion zsh

namespace Bugs;

internal static class Bug32
{
	// No code: the bug is in the generated shell script, not in a handler.
}
