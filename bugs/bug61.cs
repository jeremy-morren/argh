// Bug 61 - [NamespaceSegment] is silently ignored by MapNamespace<T>("literal"); AGH0018 never fires
// Group B: generator - uncompilable output and missing diagnostics
//
// Run:      Bugs.exe --help
// Actual:   the namespace is listed as "other61"; "seg61" doesn't exist
// Expected: an AGH0018 diagnostic for the conflict between the attribute and the literal
//
// Cause: ValidateNamespaceSegmentForExplicitName (Analysis.cs:1100), which would report AGH0018, has no callers.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

/// <summary>Namespace whose attribute says "seg61" while the registration says "other61".</summary>
[NamespaceSegment("seg61")]
internal sealed class Bug61Ns
{
	/// <summary>A command inside the namespace.</summary>
	[NoOptionsInjection]
	public static void Inner() => Console.Out.WriteLine("inner");
}

internal static class Bug61
{
	public static void Register(IArghRootBuilder app) => app.MapNamespace<Bug61Ns>("other61");
}
