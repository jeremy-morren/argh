// Bug 55 - Generator errors don't halt emission
// Group B: generator - uncompilable output and missing diagnostics
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG55
// Actual:   error AGH0027 (command name 'bug55' registered more than once), and the same build still writes the
//           whole .artifacts/obj/Bugs/generated/.../ArghGenerated.g.cs (Bugs.csproj sets EmitCompilerGeneratedFiles):
//           it has 'bug55' in the command list twice and the dispatch for it (case "bug55":).
// Expected: no generated code after an AGH error. CLAUDE.md says "Errors halt code generation; warnings allow it
//           to continue".
//
// Changed on flag-tokens and name-matching: the cascading C# errors are gone. Until flag-tokens a duplicate short
// option (AGH0033) added CS0152, and until name-matching this AGH0027 case added CS0152 for "bug55"; AGH0028 and
// AGH0021 don't cascade either now. Short options and command names are no longer emitted as switch cases.
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
