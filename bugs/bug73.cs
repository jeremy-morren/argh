// Bug 73 - A required Uri binds empty, whitespace and garbage values as relative URIs
// Group A: parsing and binding
//
// Run:      Bugs.exe bug73 --u ""
// Actual:   u:[] absolute:False      exit 0
// Run:      Bugs.exe bug73 --u "not a uri"
// Actual:   u:[not a uri] absolute:False
// Expected: an error for empty and whitespace values   (clap's path/URL parsers reject an empty value)
//
// The optional Uri? treats whitespace as absent: Bugs.exe bug73-opt --opt "" -> opt:null. So the two paths disagree.
// (They are separate commands because a Uri and a Uri? in one method hit the __uri local collision that is
// fixed on the timeonly branch.)
// Cause: Emit.Runner.cs:1284 guards the optional path with IsNullOrWhiteSpace; :1302 binds anything
//        Uri.TryCreate(..., UriKind.RelativeOrAbsolute) accepts.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug73
{
	/// <summary>Required Uri flag.</summary>
	[NoOptionsInjection]
	public static void Required(Uri u) => Console.Out.WriteLine($"u:[{u}] absolute:{u.IsAbsoluteUri}");

	/// <summary>Optional Uri flag.</summary>
	[NoOptionsInjection]
	public static void Optional(Uri? opt = null) => Console.Out.WriteLine($"opt:{opt?.ToString() ?? "null"}");

	public static void Register(IArghRootBuilder app)
	{
		app.Map("bug73", Required);
		app.Map("bug73-opt", Optional);
	}
}
