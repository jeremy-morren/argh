// Bug 11 - float, decimal, char, NaN and Infinity defaults don't compile
// Group B: generator - uncompilable output and missing diagnostics
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG11
// Actual:   decimal r = 1.5;          CS0664 (literal of type double cannot be implicitly converted)
//           float f = 1.5;            CS0664
//           float? g = 2.5;           CS0266
//           double d = NaN;           CS0103
//           double i = Infinity;      CS0103
//           string? c = 'x';          CS0029
// Expected: compiles with the declared defaults
//
// Same cause as bug10: TryGetDefaultLiteral (ParameterModel.cs:977-996) drops the type suffix and
// uses Convert.ToString for the value. long.MinValue, 1e-7 and strings with quotes are fine.

#if BUG11
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug11
{
	/// <summary>Numeric and char defaults.</summary>
	[NoOptionsInjection]
	public static void Run(
		decimal r = 1.5m,
		float f = 1.5f,
		float? g = 2.5f,
		double d = double.NaN,
		double i = double.PositiveInfinity,
		char c = 'x') =>
		Console.Out.WriteLine($"{r} {f} {g} {d} {i} {c}");

	public static void Register(IArghRootBuilder app) => app.Map("bug11", Run);
}
#endif
