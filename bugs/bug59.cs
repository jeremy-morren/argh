// Bug 59 - A namespace name containing '{' or '}' doesn't compile
// Group B: generator - uncompilable output and missing diagnostics
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG59
// Actual:   ArghGenerated.g.cs: error CS0103: The name 'x' does not exist in the current context  (x6)
// Expected: compiles, or an AGH diagnostic. Option names, aliases and command names with braces,
//           quotes or backslashes already work.
//
// Cause: Emit.Dispatch.cs:276-277 builds pathPrefix / nsHelp with Escape only and splices them into an
//        interpolated string at :296, :303, :323, where the braces become interpolation holes.
//        EscapeForHelpInterpolation exists for this.

#if BUG59
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug59
{
	/// <summary>A command inside the namespace.</summary>
	[NoOptionsInjection]
	public static void Inner() => Console.Out.WriteLine("inner");

	public static void Register(IArghRootBuilder app) =>
		app.MapNamespace("ns{x}", "Namespace with braces.", g => g.Map("inner", Inner));
}
#endif
