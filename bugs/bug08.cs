// Bug 08 - `bool?` with an unparseable value becomes true
// Group A: parsing and binding
//
// Run:      Bugs.exe bug08 --dry-run=bogus
// Actual:   dry-run:True
// Expected: an error   (clap: "invalid value 'bogus' for '--dry-run [<DRY_RUN>]'  [possible values: true, false]";
//                       pflag: "invalid argument \"bogus\" for \"--dry-run\" flag: strconv.ParseBool: ...")
//
// Also: --dry-run=no -> True.  --dry-run=false -> False works, which is what bug02 lacks for plain bool.
// Cause: ParseNullableBool in Emit.Dispatch.cs:537-539 returns fromYesSwitch when bool.TryParse fails.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug08
{
	/// <summary>Prints --dry-run / --no-dry-run.</summary>
	[NoOptionsInjection]
	public static void Run(bool? dryRun = null) => Console.Out.WriteLine($"dry-run:{dryRun?.ToString() ?? "null"}");

	public static void Register(IArghRootBuilder app) => app.Map("bug08", Run);
}
