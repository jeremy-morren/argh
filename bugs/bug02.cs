// Bug 02 - `--flag=false` on a plain bool sets it to true
// Group A: parsing and binding
//
// Run:      Bugs.exe bug02 --force=false
// Actual:   force:True
// Expected: force:False  (pflag parses the attached value with strconv.ParseBool)
//           or an error  (clap SetTrue: "unexpected value 'false' for '--force' found; no more were expected")
//
// Also: --force=0, --force=False, -f=false.  bool? honours the value: see bug08's --dry-run=false.
// Cause: binding tests flags.ContainsKey(...) and ignores the stored value
//        (Emit.Runner.cs:298, :301; Emit.Parsing.cs:277, :404-407).

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug02
{
	/// <summary>Prints --force.</summary>
	/// <param name="force">-f, Force it.</param>
	[NoOptionsInjection]
	public static void Run(bool force = false) => Console.Out.WriteLine($"force:{force}");

	public static void Register(IArghRootBuilder app) => app.Map("bug02", Run);
}
