// Bug 75 - A repeated scalar flag silently takes the last value
// Group A: parsing and binding (design)
//
// Run:      Bugs.exe bug75 --name a --name b
// Actual:   name:b
// Expected: clap: error "the argument '--name <NAME>' cannot be used multiple times" (unless Append / args_override_self);
//           pflag: last wins silently. Argh matches cobra, but by accident: there is no notion of "given twice".
//
// Also: bug75 -n a --name b -> b; globals too: --port 2 bug72 --port 1 -> 1.
// With bug3, `bug72 --port 1 --port x` silently keeps the default and loses the valid value as well.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug75
{
	/// <summary>A scalar flag.</summary>
	/// <param name="name">-n, The name.</param>
	[NoOptionsInjection]
	public static void Run(string name) => Console.Out.WriteLine($"name:{name}");

	public static void Register(IArghRootBuilder app) => app.Map("bug75", Run);
}
