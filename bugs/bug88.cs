// Bug 88 - [AllowedValues] with enum arguments shows C# member names in help, not the CLI names
// Group D: help and XML docs
//
// Run:      Bugs.exe bug88 --help
// Actual:   --level <enum>  ... [allowed: Bugs.Severity.Trace|Bugs.Severity.Warning]
// Expected: [allowed: trace|warning]   (the values the user types, as One of: <...> already shows)
//
// Cause: the hint is built from TypedConstant.ToCSharpString(); for enum arguments that's the qualified C# member.
// Reported by session argh-a5 while building range-unbounded (#90).

using System.ComponentModel.DataAnnotations;
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug88
{
	/// <summary>Enum flag restricted with [AllowedValues].</summary>
	/// <param name="level">A level.</param>
	[NoOptionsInjection]
	public static void Run([AllowedValues(Severity.Trace, Severity.Warning)] Severity level = Severity.Trace) =>
		Console.Out.WriteLine($"level:{level}");

	public static void Register(IArghRootBuilder app) => app.Map("bug88", Run);
}
