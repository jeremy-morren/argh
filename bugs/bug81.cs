// Bug 81 - Combined short flags (-vn foo, -nfoo) are rejected, and the limitation isn't documented
// Group A: parsing and binding
//
// Run:      Bugs.exe bug81 -vn foo
// Actual:   Error: combined short flags (e.g. -abc) are not supported.   followed by the full help
// Expected: verbose:True name:foo   (clap and pflag both accept -vn foo and -nfoo)
//
// Also rejected: -nfoo, -vv.  -n=foo works.  docs/ never states the limitation.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug81
{
	/// <summary>Two flags with short options.</summary>
	/// <param name="verbose">-v, Verbose.</param>
	/// <param name="name">-n, The name.</param>
	[NoOptionsInjection]
	public static void Run(bool verbose, string name = "") => Console.Out.WriteLine($"verbose:{verbose} name:{name}");

	public static void Register(IArghRootBuilder app) => app.Map("bug81", Run);
}
