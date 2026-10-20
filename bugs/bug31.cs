// Bug 31 - Fish completion never descends below the root
// Group E: completion and __schema
//
// The bug is in the fish script template (src/Nullean.Argh.Core/Help/CompletionScriptTemplates.cs:61), which
// sends `(commandline -opc)[2..-1]`. With -c, fish cuts at the cursor and excludes the token being typed, so
// the partial word is never sent and the engine treats the last committed word as the partial.
//
// What fish sends for `bugs bug29 <TAB>`:
// Run:      Bugs.exe __complete fish -- bug29
// Actual:   bug29
// Expected: inner                  (cobra: `$args[1] __complete $args[2..-1] $lastArg` with lastArg = `commandline -ct`;
//                                   clap: `... --cut-at-cursor) (commandline --current-token)`)
//
// Print the script:  Bugs.exe __completion fish

namespace Bugs;

internal static class Bug31
{
	// No code: the bug is in the generated shell script, not in a handler.
}
