// Bug 41 - A committed --flag=value breaks bash completion
// Group E: completion and __schema
//
// Bash splits --api-url=x into three words (= is in COMP_WORDBREAKS), so this is what the bash script sends
// for `bugs --api-url=x bug<TAB>`:
// Run:      Bugs.exe __complete bash -- --api-url = x bug
// Actual:   (nothing: the engine consumes "=" as the value and then sees "x" as unknown)
// Expected: bug01 bug02 ...         (cobra's bash script reassembles words split at '=')
//
// The template (CompletionScriptTemplates.cs:35) would need `_get_comp_words_by_ref -n =`.

namespace Bugs;

internal static class Bug41
{
	// No code: the bug is in the generated bash script.
}
