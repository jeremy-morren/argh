// Bug 55 - Generator errors don't halt emission, so compiler errors follow every AGH error
// Group B: generator - uncompilable output and missing diagnostics
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG55
// Actual:   error AGH0033 (duplicate short option '-v')  followed by
//           ArghGenerated.g.cs: error CS0152: ... multiple cases with the label value 'v'
// Expected: only the AGH0033 error. CLAUDE.md says "Errors halt code generation; warnings allow it to continue".
//
// Cause: TryBuildAppEmitModel (Analysis.cs:368-470) reports the diagnostics but never checks their
//        severity; Execute (CliParserGenerator.cs:214-218) emits nothing only when no commands exist.

#if BUG55
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug55
{
	/// <summary>Two flags with the same short option.</summary>
	/// <param name="verbose">-v, Verbose.</param>
	/// <param name="version">-v, Version.</param>
	[NoOptionsInjection]
	public static void Run(bool verbose, bool version) => Console.Out.WriteLine($"{verbose} {version}");

	public static void Register(IArghRootBuilder app) => app.Map("bug55", Run);
}
#endif
