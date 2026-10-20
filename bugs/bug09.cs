// Bug 09 - A required separator collection accepts zero items
// Group A: parsing and binding
//
// Run:      Bugs.exe bug09 --ids=
// Actual:   ids:   exit 0          (the flag is [required] in help)
// Expected: an error               (pflag IntSlice: "invalid argument \"\" for \"--ids\" flag: strconv.Atoi: parsing \"\": invalid syntax";
//                                   clap keeps the empty part and its integer parser rejects it)
//
// Also: Bugs.exe bug09 --ids ,,  -> ids:   exit 0
// Cause: the separator path (Emit.Parsing.cs:568-603) only checks that the flag is present and skips
//        empty parts at :587; the repeatable-flag path (:627-635) checks the item count.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug09
{
	/// <summary>Takes a comma-separated list of ids.</summary>
	/// <param name="ids">Comma-separated ids.</param>
	[NoOptionsInjection]
	public static void Run([CollectionSyntax(Separator = ",")] List<int> ids) =>
		Console.Out.WriteLine($"ids:{string.Join(",", ids)}");

	public static void Register(IArghRootBuilder app) => app.Map("bug09", Run);
}
