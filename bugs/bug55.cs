// Bug 55 - Generator errors don't halt emission, so compiler errors follow every AGH error
// Group B: generator - uncompilable output and missing diagnostics
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG55
// Actual:   error AGH0027 (command name 'bug55' registered more than once)  followed by
//           ArghGenerated.g.cs: error CS0152: The switch statement contains multiple cases with the label value '"bug55"'
// Expected: only the AGH0027 error. CLAUDE.md says "Errors halt code generation; warnings allow it to continue".
//
// Partly fixed on flag-tokens: a duplicate short option (AGH0033) no longer adds CS0152, because short options are
// no longer emitted as switch cases; this file used that case before and now uses a duplicate command name.
//
// Cause: TryBuildAppEmitModel (Analysis.cs:368-470) reports the diagnostics but never checks their
//        severity; Execute (CliParserGenerator.cs:214-218) emits nothing only when no commands exist.

#if BUG55
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug55
{
	/// <summary>First registration.</summary>
	[NoOptionsInjection]
	public static void First() => Console.Out.WriteLine("first");

	/// <summary>Second registration under the same name.</summary>
	[NoOptionsInjection]
	public static void Second() => Console.Out.WriteLine("second");

	public static void Register(IArghRootBuilder app)
	{
		app.Map("bug55", First);
		app.Map("bug55", Second);
	}
}
#endif
