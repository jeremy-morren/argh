// Bug 83 - Command names and long flags are matched case-insensitively
// Group A: parsing and binding
//
// Run:      Bugs.exe BUG83 --name x       -> name:x          (command matched case-insensitively)
// Run:      Bugs.exe bug83 --NAME x       -> name:x          (long flag too, since flag-tokens; was "unknown option")
// Run:      Bugs.exe bug81 -V             -> Error: unknown short option '-V'   (short flags stay case-sensitive)
// Expected: case-sensitive commands and flags, as clap and cobra by default (the dev note: Ordinal by default)
//
// Partly fixed on unmatched-args: -V now gives "unknown short option '-V'" before and after the command
// (it used to say "missing value for short flag '-V'" after it).
// Changed on flag-tokens: ArghTokenizer's long-flag table uses StringComparer.OrdinalIgnoreCase.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug83
{
	/// <summary>A flag, for case and message checks.</summary>
	[NoOptionsInjection]
	public static void Run(string name = "") => Console.Out.WriteLine($"name:{name}");

	public static void Register(IArghRootBuilder app) => app.Map("bug83", Run);
}
