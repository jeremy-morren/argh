// Bug 21 - Literal command names aren't validated
// Group B: generator - uncompilable output and missing diagnostics
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG21     (compiles silently)
// Run:      Bugs.exe --help          -> lists "--help", "-x" and "has space" as commands
//           Bugs.exe --help          -> root help wins; the "--help" command is unreachable
//           Bugs.exe has space       -> "has" is an unknown command; the name can't be typed as one token
// Expected: an AGH diagnostic for names that start with '-', contain whitespace, or are empty
//           (clap's debug asserts reject such names)

#if BUG21
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug21
{
	/// <summary>A command with an unusable name.</summary>
	[NoOptionsInjection]
	public static void Run() => Console.Out.WriteLine("ran");

	public static void Register(IArghRootBuilder app)
	{
		app.Map("--help", Run);
		app.Map("-x", Run);
		app.Map("has space", Run);
		app.Map("", Run);
	}
}
#endif
