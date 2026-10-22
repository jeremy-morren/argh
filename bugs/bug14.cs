// Bug 14 - A command and a namespace with the same name aren't diagnosed
// Group B: generator - uncompilable output and missing diagnostics
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG14      (compiles silently since name-matching)
// Run:      Bugs.exe bug14             -> command                       (the command wins)
// Run:      Bugs.exe bug14 inner       -> Error: unexpected argument 'inner' found.   (the namespace is unreachable)
//           Bugs.exe --help lists bug14 under both Namespaces and Commands.
// Before name-matching: ArghGenerated.g.cs: error CS0152: multiple cases with the label value 'bug14'.
// Expected: an AGH diagnostic naming the conflict (clap rejects duplicate subcommand names in debug asserts)
// #if'd because it changes the root listing.
//
// Cause: AGH0027 (Analysis.cs:475) compares commands only with commands; AGH0018 only covers segment
//        conflicts from attributes or XML.

#if BUG14
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug14
{
	/// <summary>A command.</summary>
	[NoOptionsInjection]
	public static void Plain() => Console.Out.WriteLine("command");

	/// <summary>A command inside the namespace.</summary>
	[NoOptionsInjection]
	public static void Inner() => Console.Out.WriteLine("namespace command");

	public static void Register(IArghRootBuilder app)
	{
		app.Map("bug14", Plain);
		app.MapNamespace("bug14", "Same name as the command.", g => g.Map("inner", Inner));
	}
}
#endif
