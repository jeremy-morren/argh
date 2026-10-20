// Bug 13 - Handler parameters named like generated locals don't compile
// Group B: generator - uncompilable output and missing diagnostics
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG13
// Actual:   CS0128 / CS0136 / CS0841 in ArghGenerated.g.cs: `args` shadows the method parameter,
//           `flags` duplicates the Dictionary local, `i`, `a`, `eq` clash with the parse loop,
//           `ct` with the CancellationToken parameter, `idx` with the int[] index.
// Expected: compiles; the user's names are ordinary CLI flags (--args, --flags, ...)
//
// Each name alone is enough. Cause: SafeLocalName (ParameterModel.cs:998-1008) only escapes C# keywords;
// the generated locals are declared at Emit.Runner.cs:72, 85, 103, 111, 116 and Emit.Parsing.cs:53, 124, 127.

#if BUG13
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug13
{
	/// <summary>Parameter names that collide with generator locals.</summary>
	[NoOptionsInjection]
	public static void Run(string args, string flags = "", string i = "", string a = "", string eq = "", string ct = "") =>
		Console.Out.WriteLine($"{args} {flags} {i} {a} {eq} {ct}");

	public static void Register(IArghRootBuilder app) => app.Map("bug13", Run);
}
#endif
