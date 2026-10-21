// Bug 90 - A bad [Range(typeof(T), ...)] on an options-type member gets no AGH0038, and the check is silently dropped
// Group B: generator - uncompilable output and missing diagnostics
//
// Build:    dotnet build bugs -c Release          (compiles; no AGH0038)
// Run:      Bugs.exe bug90 --amount 999 show
// Actual:   amount:999      exit 0          (no range check at all)
// Expected: build error AGH0038 on the bad bound, as for the same attribute on a method parameter
//
// On a method parameter, #90 parses [Range(typeof(T), "min", "max")] at build time and reports AGH0038 on the bad
// argument. The options-member path has no diagnostic accumulator, so the error is lost and the check dropped.
// Reported by session argh-a5 while building range-unbounded (#90).

using System.ComponentModel.DataAnnotations;
using Nullean.Argh.Builder;

namespace Bugs;

/// <summary>Namespace options with an unparseable range bound.</summary>
public sealed class Bug90NsOpts : GlobalOpts
{
	/// <summary>Amount; the upper bound "lots" isn't a decimal.</summary>
	[Range(typeof(decimal), "1", "lots")]
	public decimal Amount { get; set; } = 1m;
}

internal static class Bug90
{
	/// <summary>Prints the amount.</summary>
	public static void Show(Bug90NsOpts o) => Console.Out.WriteLine($"amount:{o.Amount}");

	public static void Register(IArghRootBuilder app) =>
		app.MapNamespace("bug90", "Namespace whose option has a bad range bound.", g =>
		{
			g.UseNamespaceOptions<Bug90NsOpts>();
			g.Map("show", Show);
		});
}
