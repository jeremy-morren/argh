// Bug 06 - Boolean environment variables are parsed case-sensitively
// Group A: parsing and binding
//
// Run:      BUGS_FORCE=False Bugs.exe bug06
// Actual:   force:True
// Expected: force:False   (clap FalseyValueParser lowercases: n/no/f/false/off/0; Go ParseBool: 0/f/F/FALSE/false/False)
//
// Also: BUGS_FORCE=FALSE, BUGS_FORCE=no, BUGS_FORCE=off all give True. Only "0" and "false" give False.
// Cause: Emit.Runner.cs:298 tests EnvRaw != "0" && EnvRaw != "false"; same at Emit.Parsing.cs:404.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug06
{
	/// <summary>Prints --force, which falls back to BUGS_FORCE.</summary>
	[NoOptionsInjection]
	public static void Run([Env("BUGS_FORCE")] bool force = false) => Console.Out.WriteLine($"force:{force}");

	public static void Register(IArghRootBuilder app) => app.Map("bug06", Run);
}
