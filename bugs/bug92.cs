// Bug 92 - A global option written after the namespace segment is silently dropped
// Group A: parsing and binding
//
// Run:      Bugs.exe bug92 --port 5 show
// Actual:   port:80 severity:Information region:-      exit 0      (--port 5 is dropped)
// Run:      Bugs.exe bug92 --severity warning show
// Actual:   port:80 severity:Information region:-      exit 0      (dropped too; "--severity bogus" is rejected,
//                                                                    so the value is parsed, just never used)
// Expected: port:5 / severity:Warning, as with "--port 5 bug92 show" or "bug92 show --port 5"
// Works:    Bugs.exe bug92 --region eu show   (members declared on the namespace options type itself)
//
// Cause: the namespace pre-parse stores the inherited global members only on the namespace's static
// (s_opts_Bugs_Bug92NsOpts.Port); the runner takes each member from the first static in the chain that declares it
// (Emit.Parsing.cs:371-381), which for an inherited global member is the global static (s_opts_Bugs_GlobalOpts.Port).
// Validation attributes on such a member are skipped too: EXE numeric-ns --zone EU show runs in the test host.
// Reported by session argh-a5 while building range-unbounded (#90).

using Nullean.Argh.Builder;

namespace Bugs;

/// <summary>Namespace options that inherit the global options.</summary>
public sealed class Bug92NsOpts : GlobalOpts
{
	/// <summary>Region.</summary>
	public string? Region { get; set; }
}

internal static class Bug92
{
	/// <summary>Prints the global and namespace options.</summary>
	public static void Show(Bug92NsOpts o) =>
		Console.Out.WriteLine($"port:{o.Port} severity:{o.Severity} region:{o.Region ?? "-"}");

	public static void Register(IArghRootBuilder app) =>
		app.MapNamespace("bug92", "Namespace whose options inherit the global options.", g =>
		{
			g.UseNamespaceOptions<Bug92NsOpts>();
			g.Map("show", Show);
		});
}
