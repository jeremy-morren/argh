// Bug 87 - On Windows every completion candidate ends in '\r', so bash (Git Bash) never matches one
// Group E: completion and __schema
//
// CompletionWriter writes candidates with Console.Out.WriteLine, which is "\r\n" on Windows:
// Run:      Bugs.exe __complete bash -- bug0 | od -c        -> b u g 0 1 \r \n ...
// In Git Bash (bash 5.3) `mapfile -t` strips only the "\n", so each COMPREPLY entry keeps a trailing "\r"
// and nothing the user typed matches it.
// Expected: candidates end in "\n" on every platform. Either the protocol writes "\n" explicitly (better: one
//           line ending everywhere, for every shell), or the bash template strips it (${line%$'\r'}).
//
// Confirmed live by the completion-test CI job (branch completion-test, session argh-a5):
// https://github.com/jeremy-morren/argh/actions/runs/39528730773

namespace Bugs;

internal static class Bug87
{
	// No code: see CompletionWriter.WriteFiltered (src/Nullean.Argh.Core/Help/CompletionWriter.cs).
}
