// Bug 45 - String defaults show C# escape sequences
// Group D: help and XML docs
//
// Run:      Bugs.exe bug45 --help
// Actual:   --path <string>   ... [default: c:\\temp\\x]
//           --quote <string>  ... [default: say \"hi\"\\now]
//           --just <string>   ... [default: \]            (Trim('"') eats the escaped quote)
//           --tab <string>    ... [default: \t]
// Expected: the raw values: c:\temp\x, say "hi"\now, ", and a literal tab   (clap prints the raw value;
//           pflag prints %q on purpose; follow clap)
//
// Cause: Emit.Help.cs:939 only trims the quotes from the SymbolDisplay literal
//        (the same root cause as the [AllowedValues] quoting fixed in a1a542c).

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug45
{
	/// <summary>String defaults with backslashes, quotes and a tab.</summary>
	/// <param name="path">A path.</param>
	/// <param name="quote">A quoted value.</param>
	/// <param name="just">A single double quote.</param>
	/// <param name="tab">A tab.</param>
	[NoOptionsInjection]
	public static void Run(string path = @"c:\temp\x", string quote = "say \"hi\"\\now", string just = "\"", string tab = "\t") =>
		Console.Out.WriteLine($"{path}|{quote}|{just}|{tab}|");

	public static void Register(IArghRootBuilder app) => app.Map("bug45", Run);
}
