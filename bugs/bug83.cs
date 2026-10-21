// Bug 83 - Command names are case-insensitive but flags aren't, and the unknown -V message depends on position
// Group A: parsing and binding
//
// Run:      Bugs.exe BUG83 --name x       -> name:x          (command matched case-insensitively)
// Run:      Bugs.exe bug83 --NAME x       -> Error: unknown option '--NAME'
// Run:      Bugs.exe -V bug83             -> Error: unknown short option '-V'
// Run:      Bugs.exe bug83 -V             -> Error: missing value for short flag '-V'    (misleading: -V doesn't exist)
// Expected: one rule for both (clap and cobra are case-sensitive for commands and flags by default),
//           and one "unknown option" message whatever the position

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
