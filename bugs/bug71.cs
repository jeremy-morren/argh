// Bug 71 - float, double and decimal accept thousands separators, so "1,5" parses as 15
// Group A: parsing and binding
//
// Run:      Bugs.exe bug71 --f 1,5 --d 1,5 --m 1,5
// Actual:   f:15 d:15 m:15
// Expected: an error   (Rust f64::from_str and Go strconv.ParseFloat take no thousands separators)
//
// Also: --f 1,5,5 -> 155, --f 5, -> 5, and in every path (required, DTO, env var, positional, leading global).
// int rejects 1,000, so the types disagree. +5, " 5 " and (float/double) 1e3, NaN, Infinity are accepted too.
// Cause: NumberStyles.Float | NumberStyles.AllowThousands at Emit.Runner.cs:1370, 1379 and :1498-1506;
//        NumberStyles.Number for decimal at :1388; Emit.Parsing.cs:448, 452, 454, 456.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug71
{
	/// <summary>Floating-point flags.</summary>
	[NoOptionsInjection]
	public static void Run(float? f = null, double? d = null, decimal? m = null) =>
		Console.Out.WriteLine($"f:{f} d:{d} m:{m}");

	public static void Register(IArghRootBuilder app) => app.Map("bug71", Run);
}
