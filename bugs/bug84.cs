// Bug 84 - HashSet<T> parameters aren't recognised as collections and don't compile
// Group B: generator - uncompilable output and missing diagnostics
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG84
// Actual:   ArghGenerated.g.cs: error CS1503: Argument ...: cannot convert from 'string' to 'System.Collections.Generic.HashSet<string>'
//           No Argh diagnostic explains it.
// Expected: compiles as a repeatable flag, like List<T> / IReadOnlySet<T>; or an AGH diagnostic naming the type
//
// The generator recognises IEnumerable<T>, IReadOnlyList<T>, IReadOnlySet<T>, List<T> and arrays only
// (collection classification in ParameterModel.cs); HashSet<T> falls through to the scalar path (bug12).
// Reported by session argh-a5.

#if BUG84
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug84
{
	/// <summary>A HashSet flag.</summary>
	[NoOptionsInjection]
	public static void Run(HashSet<string> tags) => Console.Out.WriteLine($"tags:{string.Join(",", tags)}");

	public static void Register(IArghRootBuilder app) => app.Map("bug84", Run);
}
#endif
