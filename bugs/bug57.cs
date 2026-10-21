// Bug 57 - Enum member docs are never shown
// Group D: help and XML docs
//
// Run:      Bugs.exe bug57 --help
// Actual:   --color <enum>  The colour. [default: red]
//                           One of: <red|blue>
// Expected: the member docs too:  One of: <red|blue> (red: Warm red.; blue: Deep blue.)
//           (clap's ValueEnum shows each variant's doc comment under "Possible values:")
//
// Partly fixed on range-unbounded (#90): the default used to print as [default: 0] and now prints the CLI name.
// Cause: GetEnumMemberDocs (ParameterModel.cs:887) reads doc.Root?.Element("summary"), which misses the
//        <member> wrapper, so EnumMemberDocs is always empty. Unwrapping <member> makes same-assembly enums
//        render; cross-assembly enums also need the metadata-XML fallback.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

/// <summary>Colours.</summary>
public enum Color
{
	/// <summary>Warm red.</summary>
	Red,
	/// <summary>Deep blue.</summary>
	Blue,
}

internal static class Bug57
{
	/// <summary>Enum with documented members.</summary>
	/// <param name="color">The colour.</param>
	[NoOptionsInjection]
	public static void Run(Color color = Color.Red) => Console.Out.WriteLine($"color:{color}");

	public static void Register(IArghRootBuilder app) => app.Map("bug57", Run);
}
