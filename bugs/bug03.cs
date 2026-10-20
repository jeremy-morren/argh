// Bug 03 - A global enum flag after the command accepts any value silently
// Group A: parsing and binding
//
// Run:      Bugs.exe bug03 --severity bogus
// Actual:   severity:Information   exit 0
// Expected: an error, exit 2       (clap: "invalid value 'bogus' for '--severity <SEVERITY>'  [possible values: ...]")
//
// Also: --severity 2 -> Warning (numeric index accepted), --severity= -> Information.
// Before the command it is validated: Bugs.exe --severity bogus bug03 -> "Error: invalid value".
//
// Namespace options of every special type (TimeSpan, DateTime, DateOnly, Uri, FileInfo, ...) go through
// the same re-parse (see bug28's namespace):
//           Bugs.exe bug28 --timeout nope show   -> Error: invalid ... exit 2
//           Bugs.exe bug28 show --timeout nope   -> prefix: timeout:null   exit 0   (expected: the same error)
// Cause: Emit.Runner.cs:286-287 skips OptionsInjected params; the trailing re-parse in
//        Emit.Parsing.cs:484-490 uses Enum.TryParse(txt, true, ...) and keeps the static fallback silently.

using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug03
{
	/// <summary>Prints the global --severity value.</summary>
	public static void Run(GlobalOpts g) => Console.Out.WriteLine($"severity:{g.Severity}");

	public static void Register(IArghRootBuilder app) => app.Map("bug03", Run);
}
