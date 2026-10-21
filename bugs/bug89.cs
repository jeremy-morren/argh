// Bug 89 - [Range] and [AllowedValues] on namespace or global options members are shown in help but never checked
// Group A: parsing and binding
//
// Run:      Bugs.exe bug89 --retries 99 show
// Actual:   retries:99 mode:bogus      exit 0
// Run:      Bugs.exe bug89 --mode bogus show
// Actual:   accepted too
// Expected: Error: --retries: value must be between 1 and 10.   exit 2   (as for the same attribute on a method parameter)
//
// Bugs.exe bug89 show --help shows [range: 1-10] and [allowed: fast|slow], so the user is told a rule that isn't enforced.
// Reported by session argh-a5 while building range-unbounded (#90).

using System.ComponentModel.DataAnnotations;
using Nullean.Argh.Builder;

namespace Bugs;

/// <summary>Namespace options with validation attributes.</summary>
public sealed class Bug89NsOpts : GlobalOpts
{
	/// <summary>Retries, between 1 and 10.</summary>
	[Range(1, 10)]
	public int Retries { get; set; } = 3;

	/// <summary>Mode, fast or slow.</summary>
	[AllowedValues("fast", "slow")]
	public string Mode { get; set; } = "fast";
}

internal static class Bug89
{
	/// <summary>Prints the namespace options.</summary>
	public static void Show(Bug89NsOpts o) => Console.Out.WriteLine($"retries:{o.Retries} mode:{o.Mode}");

	public static void Register(IArghRootBuilder app) =>
		app.MapNamespace("bug89", "Namespace whose options carry validation attributes.", g =>
		{
			g.UseNamespaceOptions<Bug89NsOpts>();
			g.Map("show", Show);
		});
}
