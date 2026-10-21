// Bug 72 - The trailing re-parse of global int/long is culture-sensitive
// Group A: parsing and binding
//
// Program.cs sets CultureInfo.CurrentCulture from BUGS_CULTURE before running, so:
// Run:      BUGS_CULTURE=sv-SE Bugs.exe bug72 --port −5        (U+2212 minus, which sv-SE uses)
// Actual:   port:-5                                          (trailing: current culture)
// Run:      BUGS_CULTURE=sv-SE Bugs.exe --port −5 bug72
// Actual:   Error: invalid int for --port: '−5'.             (leading: invariant)
// Expected: the same result in both positions, culture-independent   (clap and Go parse numbers culture-independently)
//
// Cause: Emit.Parsing.cs:439, 442 call int.TryParse(txt, out ...) with the current culture; every other
//        path is invariant. Trailing double (:446) also lacks AllowThousands while leading double has it (bug71).

using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug72
{
	/// <summary>Prints the global --port value.</summary>
	public static void Run(GlobalOpts g) => Console.Out.WriteLine($"port:{g.Port}");

	public static void Register(IArghRootBuilder app) => app.Map("bug72", Run);
}
