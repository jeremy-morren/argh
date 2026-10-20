// Bug 04 - Extra positional arguments are ignored
// Group A: parsing and binding
//
// Run:      Bugs.exe bug04 --name x extra more
// Actual:   name:x   exit 0
// Expected: "error: unexpected argument 'extra' found", exit 2   (clap; cobra's leaf commands accept extras)
//
// Cause: Emit.Runner.cs:279 collects every non-dash token into `positionals`; nothing after binding
//        (:358-397) compares positionals.Count with the declared positionals.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug04
{
	/// <summary>Takes one flag and no positionals.</summary>
	[NoOptionsInjection]
	public static void Run(string name) => Console.Out.WriteLine($"name:{name}");

	public static void Register(IArghRootBuilder app) => app.Map("bug04", Run);
}
