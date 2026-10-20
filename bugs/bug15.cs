// Bug 15 - Two flags whose C# local names collide don't compile, with no diagnostic
// Group B: generator - uncompilable output and missing diagnostics
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG15
// Actual:   ArghGenerated.g.cs: error CS0128: A local variable or function named 'foo_bar' is already defined
//           (and 'foo_barText')
// Expected: compiles, with distinct flags --foo-bar and --foo_bar, or an AGH diagnostic
//
// The CLI names differ (--foo-bar, --foo_bar), so AGH0007 can't see the collision.
// Cause: SafeLocalName (ParameterModel.cs:1001) maps the kebab name to `_`.

#if BUG15
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug15
{
	/// <summary>Two parameters with colliding local names.</summary>
	[NoOptionsInjection]
	public static void Run(string fooBar = "", string foo_bar = "") => Console.Out.WriteLine($"{fooBar} {foo_bar}");

	public static void Register(IArghRootBuilder app) => app.Map("bug15", Run);
}
#endif
