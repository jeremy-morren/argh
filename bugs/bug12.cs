// Bug 12 - Unsupported scalar types are treated as string and don't compile, with no diagnostic
// Group B: generator - uncompilable output and missing diagnostics
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG12
// Actual:   dozens of CS0029 / CS1503 errors in ArghGenerated.g.cs, e.g. `string? big = 18446744073709551615;`
// Expected: either the type is supported, or an AGH diagnostic names the parameter and its type
//           (clap's derive macro fails with a message naming the type)
//
// Affected: char, byte, sbyte, short, ushort, uint, ulong, Guid, and any struct implementing
// IParsable<T> without [ArgumentParser].
// Cause: GetSimpleTypeName (ParameterModel.cs:910-951) falls through to `return "string"`;
//        ClassifyScalar (:824) has no "unsupported type" diagnostic.

#if BUG12
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug12
{
	/// <summary>Unsupported scalar types.</summary>
	[NoOptionsInjection]
	public static void Run(Guid id, ulong big = ulong.MaxValue, short small = 1) =>
		Console.Out.WriteLine($"{id} {big} {small}");

	public static void Register(IArghRootBuilder app) => app.Map("bug12", Run);
}
#endif
