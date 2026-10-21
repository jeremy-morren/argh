// Feature F1 - Support the common collection shapes, not only T[], IEnumerable<T>, IReadOnlyList<T>, List<T> and IReadOnlySet<T>
// Feature request (see the "Feature requests" section of BugsB.md); formerly bug 84 (HashSet<T>)
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=FEAT01
// Actual:   IList<T>, ICollection<T>, IReadOnlyCollection<T>, ISet<T>, HashSet<T>, ImmutableArray<T> and
//           ImmutableList<T> fall through to the scalar path and fail with
//               CS1503: cannot convert from 'string' to '<type>'      (no Argh diagnostic; bug12's family)
//           IReadOnlySet<string> is rejected: AGH0028 "IReadOnlySet<T> only supports value-type or enum element types".
// Wanted:   all of these bind as a repeatable or separator flag with any supported element type, and the
//           IReadOnlySet<T> reference-type restriction is lifted.
//           (clap binds any FromIterator collection; pflag has StringSlice / IntSlice / StringArray.)
//
// Supported today, confirmed by building and running `c --v 1 --v 2`: T[], IEnumerable<T>, IReadOnlyList<T>,
// List<T>, IReadOnlySet<int>. Recognised definitions: CliParserGenerator.SymbolReaders.cs:679-682 (arrays via
// IArrayTypeSymbol in ParameterModel.cs); the IReadOnlySet restriction: Diagnostics.cs:245-246.
// Each new type must also work with [CollectionSyntax(Separator = ...)], completion of enum elements, __schema
// (array + elementType), defaults, nullable collections, and [AsParameters]/options members.

#if FEAT01
using System.Collections.Immutable;
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Feat01
{
	/// <summary>Collection shapes the generator doesn't recognise.</summary>
	[NoOptionsInjection]
	public static void Run(
		HashSet<string>? tags = null,
		IList<string>? list = null,
		ICollection<int>? coll = null,
		IReadOnlyCollection<string>? roColl = null,
		ISet<string>? set = null,
		ImmutableArray<int> immArr = default,
		ImmutableList<string>? immList = null,
		IReadOnlySet<string>? roSetOfStrings = null) =>
		Console.Out.WriteLine($"{tags?.Count} {list?.Count} {coll?.Count} {roColl?.Count} {set?.Count} {immArr.Length} {immList?.Count} {roSetOfStrings?.Count}");

	public static void Register(IArghRootBuilder app) => app.Map("feat01", Run);
}
#endif
