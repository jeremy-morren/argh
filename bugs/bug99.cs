// Bug 99 - [Url] on a Uri accepts only http and https, on a string also ftp
// Group A: parsing and binding
//
// Run:      Bugs.exe bug99-uri --link ftp://x        -> Error: --link: URI scheme must be one of: http, https.
// Run:      Bugs.exe bug99-str --url ftp://x         -> url:ftp://x
// Expected: the same rule on both: http, https and ftp, as .NET's UrlAttribute checks for a Uri and a string
//
// Cause: Validation.cs:352-357 at 45954b6 turns [Url] on a Uri into UriSchemeConstraint("http", "https"); on a string
//        it becomes UrlConstraint, whose check (Emit.Parsing.cs:1079-1094) allows http, https and ftp.

using System.ComponentModel.DataAnnotations;
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug99
{
	/// <summary>[Url] on a Uri.</summary>
	/// <param name="link">A URL.</param>
	[NoOptionsInjection]
	public static void OnUri([Url] Uri link) => Console.Out.WriteLine($"link:{link}");

	/// <summary>[Url] on a string.</summary>
	/// <param name="url">A URL.</param>
	[NoOptionsInjection]
	public static void OnString([Url] string url) => Console.Out.WriteLine($"url:{url}");

	public static void Register(IArghRootBuilder app)
	{
		app.Map("bug99-uri", OnUri);
		app.Map("bug99-str", OnString);
	}
}
