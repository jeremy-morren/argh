// Bug 83 - Command names are case-insensitive but flags aren't
// Group A: parsing and binding
//
// Run:      Bugs.exe BUG83 --name x       -> name:x          (command matched case-insensitively)
// Run:      Bugs.exe bug83 --NAME x       -> Error: unknown option '--NAME'
// Expected: one rule for both (clap and cobra are case-sensitive for commands and flags by default)
//
// Partly fixed on unmatched-args: -V now gives "unknown short option '-V'" before and after the command
// (it used to say "missing value for short flag '-V'" after it).

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
