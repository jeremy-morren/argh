// Bug 73 - A required Uri binds empty, whitespace and garbage values as relative URIs
// Group A: parsing and binding
//
// Run:      Bugs.exe bug73 --u ""
// Actual:   u:[] absolute:False      exit 0
// Run:      Bugs.exe bug73 --u "not a uri"
// Actual:   u:[not a uri] absolute:False
// Expected: an error for empty and whitespace values   (clap's path/URL parsers reject an empty value)
//
// The optional Uri? treats whitespace as absent (--opt "" -> null), so the two paths disagree.
// Cause: Emit.Runner.cs:1284 guards the optional path with IsNullOrWhiteSpace; :1302 binds anything
//        Uri.TryCreate(..., UriKind.RelativeOrAbsolute) accepts.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug73
{
	/// <summary>Required and optional Uri flags.</summary>
	[NoOptionsInjection]
	public static void Run(Uri u, Uri? opt = null) =>
		Console.Out.WriteLine($"u:[{u}] absolute:{u.IsAbsoluteUri} opt:{opt?.ToString() ?? "null"}");

	public static void Register(IArghRootBuilder app) => app.Map("bug73", Run);
}
