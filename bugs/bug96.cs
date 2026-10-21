// Bug 96 - [property: X] validation attributes on a positional record's [AsParameters] parameters are ignored
// Group A: parsing and binding
//
// Run:      Bugs.exe bug96 --count 99          -> count:99 ...   exit 0     ([property: Range(1, 10)])
// Run:      Bugs.exe bug96 --name abcdef       -> name:abcdef    exit 0     ([property: StringLength(3)])
// Run:      Bugs.exe bug96 --mode bogus        -> mode:bogus     exit 0     ([property: AllowedValues("fast", "slow")])
// Control:  Bugs.exe bug96 --level 99          -> Error: --level: value must be between 1 and 10.   ([Range] on the parameter)
// Expected: the same errors as the parameter-targeted attribute and as the attributes on a class's properties
//
// Cause: ReadValidationConstraints reads the constructor parameter's attributes only (ParameterModel.cs:497 at
// 45954b6); [property: ...] puts them on the synthesized property. Only [CollectionSyntax] looks there
// (SymbolReaders.cs:704-711).

using System.ComponentModel.DataAnnotations;
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

/// <summary>Positional record bound with [AsParameters].</summary>
/// <param name="Count">Count, 1 to 10 (property-targeted).</param>
/// <param name="Name">Name, at most 3 characters (property-targeted).</param>
/// <param name="Mode">Mode, fast or slow (property-targeted).</param>
/// <param name="Level">Level, 1 to 10 (parameter-targeted, the control).</param>
public sealed record Bug96Opts(
	[property: Range(1, 10)] int Count = 1,
	[property: StringLength(3)] string Name = "a",
	[property: AllowedValues("fast", "slow")] string Mode = "fast",
	[Range(1, 10)] int Level = 1);

internal static class Bug96
{
	/// <summary>Prints the record's values.</summary>
	/// <param name="o">The options.</param>
	[NoOptionsInjection]
	public static void Run([AsParameters] Bug96Opts o) =>
		Console.Out.WriteLine($"count:{o.Count} name:{o.Name} mode:{o.Mode} level:{o.Level}");

	public static void Register(IArghRootBuilder app) => app.Map("bug96", Run);
}
