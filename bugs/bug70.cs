// Bug 70 - A positional bool parameter doesn't compile
// Group B: generator - uncompilable output and missing diagnostics
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG70
// Actual:   ArghGenerated.g.cs: error CS0103: The name 'first' does not exist in the current context
//           Generated, after the positional loop:
//               if (positionals.Count <= 0) first = false; else { first = positionals[0]; // fallback }
//           `first` is never declared, and the raw string is assigned to a bool.
// Expected: compiles; `bug70 true false` binds both (or an AGH diagnostic if positional bools are unsupported)
//
// A single [Argument] bool fails the same way, so it isn't a naming collision.
// Cause: the positional binding path in Emit.Runner.cs (search for "// fallback") has no bool case.
// Reported by session argh-a5.

#if BUG70
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug70
{
	/// <summary>Two positional bools.</summary>
	[NoOptionsInjection]
	public static void Run([Argument] bool first, [Argument] bool second) => Console.Out.WriteLine($"{first} {second}");

	public static void Register(IArghRootBuilder app) => app.Map("bug70", Run);
}
#endif
