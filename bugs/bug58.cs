// Bug 58 - [Range(typeof(decimal), "1", "2")] doesn't compile
// Group B: generator - uncompilable output and missing diagnostics
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG58
// Actual:   ArghGenerated.g.cs: error CS0019: Operator '<' cannot be applied to operands of type 'decimal' and 'Type'
//           (generated: if (d < typeof(decimal) || d > "1") ...; the message text reads "between typeof(decimal) and 1")
// Expected: compiles; [range: 1-2] in help; values outside 1..2 rejected
//
// [Range(1.5, 2.5)] on int and double parameters works.
// Cause: Validation.cs:283 reads constructor arguments 0 and 1 regardless of which RangeAttribute
//        constructor was used; the (Type, string, string) form puts the bounds at 1 and 2.

#if BUG58
using System.ComponentModel.DataAnnotations;
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug58
{
	/// <summary>Decimal range via the (Type, string, string) constructor.</summary>
	[NoOptionsInjection]
	public static void Run([Range(typeof(decimal), "1", "2")] decimal d) => Console.Out.WriteLine($"d:{d}");

	public static void Register(IArghRootBuilder app) => app.Map("bug58", Run);
}
#endif
