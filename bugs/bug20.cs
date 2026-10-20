// Bug 20 - `params string[]` is treated as required
// Group B: generator - uncompilable output and missing diagnostics
//
// Run:      Bugs.exe bug20
// Actual:   Error: missing required flag --items
// Expected: items:                 (a params array is optional by definition)
//
// Cause: ComputeRequired (ParameterModel.cs:953) ignores IParameterSymbol.IsParams.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug20
{
	/// <summary>A params array flag.</summary>
	[NoOptionsInjection]
	public static void Run(params string[] items) => Console.Out.WriteLine($"items:{string.Join(",", items)}");

	public static void Register(IArghRootBuilder app) => app.Map("bug20", Run);
}
