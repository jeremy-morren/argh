// Bug 35 - fileExtensions constraint values in __schema lack the leading dot
// Group E: completion and __schema
//
// Run:      Bugs.exe __schema
// Actual:   bug35-ext --doc: {"kind": "fileExtensions", "values": ["zip"]}
// Expected: "values": [".zip"]   (cli-schema v1's fileExtensions example: [".json", ".yaml"])
//
// Cause: Validation.cs:366 stores the extensions without the dot.
//
// Partly fixed on human-time: the timeSpanRange part. bug35 --window used to be
// {"kind": "timeSpanRange", "min": "\"5m\"", "max": "\"2h\""} (Validation.cs:287 stored ToCSharpString());
// it is now "min": "PT5M", "max": "PT2H". The allowed/denied values had the same bug; fixed on #82.

using System.ComponentModel.DataAnnotations;
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug35
{
	/// <summary>TimeSpan with an inclusive range.</summary>
	/// <param name="window">Between 5 minutes and 2 hours.</param>
	[NoOptionsInjection]
	public static void Run([TimeSpanRange("5m", "2h")] TimeSpan window) => Console.Out.WriteLine($"window:{window}");

	/// <summary>A file with an allowed extension.</summary>
	/// <param name="doc">A .zip file.</param>
	[NoOptionsInjection]
	public static void Ext([FileExtensions(Extensions = "zip")] FileInfo doc) => Console.Out.WriteLine($"doc:{doc.Name}");

	public static void Register(IArghRootBuilder app)
	{
		app.Map("bug35", Run);
		app.Map("bug35-ext", Ext);
	}
}
