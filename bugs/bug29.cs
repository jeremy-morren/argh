// Bug 29 - An unknown flag in a namespace without a root handler is reported as a command typo
// Group C: runtime, dispatch and hosting
//
// Run:      Bugs.exe bug29 --bogus
// Actual:   Error: unknown command or namespace '--bogus'.      (and it is fuzzy-matched against command names)
// Expected: Error: unknown option '--bogus'.   (clap: "unexpected argument '--bogus' found"; cobra: "unknown flag: --bogus")
//
// Cause: Emit.Dispatch.cs:878-890 only falls through to flag handling when a root or alias handler exists.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug29
{
	/// <summary>A command inside a namespace that has no root handler.</summary>
	[NoOptionsInjection]
	public static void Inner() => Console.Out.WriteLine("inner");

	public static void Register(IArghRootBuilder app) =>
		app.MapNamespace("bug29", "Namespace without a root handler.", g => g.Map("inner", Inner));
}
