// Bug 12 - Unsupported scalar types are treated as string and don't compile, with no diagnostic
// Group B: generator - uncompilable output and missing diagnostics
//
// Partly fixed on parsable-types (#89): on .NET 7+, any IParsable<T> type now binds (Guid, char, byte, sbyte,
// short, ushort, uint, ulong, Half, Int128, BigInteger, user IParsable/ISpanParsable types). What's left:
//   - a type that is neither built in nor IParsable, as below;
//   - all of those types on target frameworks before .NET 7, where IParsable<T> doesn't exist.
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG12
// Actual:   CS1503 in ArghGenerated.g.cs: cannot convert from 'string' to 'Bugs.Bug12Point'; no Argh diagnostic
// Expected: an AGH diagnostic naming the parameter and its type, suggesting [ArgumentParser] or IParsable<T>
//           (clap's derive macro fails with a message naming the type)
//
// Cause: GetSimpleTypeName (ParameterModel.cs:910-951) falls through to `return "string"`;
//        ClassifyScalar (:824) has no "unsupported type" diagnostic.

#if BUG12
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

/// <summary>A user type with no parser: not built in, not IParsable, no [ArgumentParser].</summary>
public readonly record struct Bug12Point(int X, int Y);

internal static class Bug12
{
	/// <summary>Takes a type the generator can't parse.</summary>
	[NoOptionsInjection]
	public static void Run(Bug12Point point) => Console.Out.WriteLine($"{point}");

	public static void Register(IArghRootBuilder app) => app.Map("bug12", Run);
}
#endif
