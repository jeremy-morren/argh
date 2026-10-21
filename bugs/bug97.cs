// Bug 97 - [Url] on a Uri[] parameter generates code that doesn't compile
// Group B: generator - uncompilable output and missing diagnostics
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG97
// Actual:   ArghGenerated.g.cs: error CS1503: Argument 1: cannot convert from 'System.Uri[]' to 'string?'
//           ArghGenerated.g.cs: error CS1503: Argument 2: cannot convert from 'System.UriKind' to 'in System.UriCreationOptions'
//           Generated: if (__p_links is { } __url___p_links && !(global::System.Uri.TryCreate(__url___p_links,
//                      global::System.UriKind.Absolute, out var __urlCheck___p_links) && ...))
// Expected: compiles and checks each element (or an AGH diagnostic if [Url] isn't supported on Uri collections)
//
// Cause: EmitUrlConstraintCheck (Emit.Parsing.cs:1079-1094 at 45954b6) validates the variable as one string; for a
//        collection it is handed the whole array.

#if BUG97
using System.ComponentModel.DataAnnotations;
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug97
{
	/// <summary>A repeatable URL option.</summary>
	/// <param name="links">Links.</param>
	[NoOptionsInjection]
	public static void Run([Url] Uri[] links) => Console.Out.WriteLine($"links:{links.Length}");

	/// <summary>A single URL option, for comparison.</summary>
	/// <param name="link">A link.</param>
	[NoOptionsInjection]
	public static void One([Url] Uri link) => Console.Out.WriteLine($"link:{link}");

	public static void Register(IArghRootBuilder app)
	{
		app.Map("bug97", Run);
		app.Map("bug97-one", One);
	}
}
#endif
