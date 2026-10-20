// Bug 07 - `--` isn't a terminator, so a positional can't start with `-`
// Group A: parsing and binding
//
// Run:      Bugs.exe bug07 red -- -x
// Actual:   Error: unknown option '--'
// Expected: mode:red files:-x      (clap and pflag: everything after `--` is positional)
//
// Also: Bugs.exe bug07 red -5           -> "missing value for short flag '-5'"
//       Bugs.exe bug07 --                -> "unknown option '--'. Did you mean '--on'?"-style fuzzy suggestion
//                                           (FailUnknownLongOption("") fuzzy-matches any flag of two characters or fewer)
// Cause: Emit.Runner.cs:114-119 takes a.Substring(2) -> "" and fails as an unknown long option;
//        Emit.Parsing.cs:120-160 does the same at the root.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug07
{
	/// <summary>Takes a mode and files.</summary>
	/// <param name="mode">The mode.</param>
	/// <param name="files">Files to copy.</param>
	/// <param name="on">Two-character flag, to show the fuzzy suggestion for '--'.</param>
	[NoOptionsInjection]
	public static void Run([Argument] string mode, bool on, [Argument] params string[] files) =>
		Console.Out.WriteLine($"mode:{mode} files:{string.Join(",", files)} on:{on}");

	public static void Register(IArghRootBuilder app) => app.Map("bug07", Run);
}
