// Bug 69 - Help pads columns by UTF-16 length, so CJK command names misalign
// Group D: help and XML docs
//
// Run:      Bugs.exe --help      (with a UTF-8 console: [Console]::OutputEncoding = [Text.Encoding]::UTF8 in pwsh)
// Actual:     bug69-café    Accented command name.
//             bug69-日本語   CJK command name.          <- summary starts three columns too far right
// Expected: aligned summaries   (clap measures display width with unicode-width; cobra pads by byte length and misaligns too)
//
// 日本語 is three UTF-16 characters but six terminal columns.
// Cause: WriteHelpListNameAndDescription (CliHelpFormatting.cs:137-141) pads with name.Length.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug69
{
	/// <summary>Accented command name.</summary>
	[NoOptionsInjection]
	public static void Cafe() => Console.Out.WriteLine("café");

	/// <summary>CJK command name.</summary>
	[NoOptionsInjection]
	public static void Cjk() => Console.Out.WriteLine("日本語");

	public static void Register(IArghRootBuilder app)
	{
		app.Map("bug69-café", Cafe);
		app.Map("bug69-日本語", Cjk);
	}
}
