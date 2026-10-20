// Bug 35 - timeSpanRange constraint values carry C# quotes in __schema
// Group E: completion and __schema
//
// Run:      Bugs.exe __schema
// Actual:   bug35 --window: {"kind": "timeSpanRange", "min": "\"5m\"", "max": "\"2h\""}
// Expected: "min": "PT5M", "max": "PT2H"   (cli-schema v1: "Duration range in ISO 8601 format (e.g. PT5M)")
//
// Help renders it correctly: [time-span-range: 5m-2h].
// Cause: Validation.cs:287 stores ToCSharpString() for the bounds. The allowed/denied values had the same
//        bug; that part is fixed on the enum-completion branch (a1a542c).

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug35
{
	/// <summary>TimeSpan with an inclusive range.</summary>
	/// <param name="window">Between 5 minutes and 2 hours.</param>
	[NoOptionsInjection]
	public static void Run([TimeSpanRange("5m", "2h")] TimeSpan window) => Console.Out.WriteLine($"window:{window}");

	public static void Register(IArghRootBuilder app) => app.Map("bug35", Run);
}
