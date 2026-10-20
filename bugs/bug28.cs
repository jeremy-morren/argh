// Bug 28 - Namespace-option parse errors always hint at root help
// Group C: runtime, dispatch and hosting
//
// Run:      Bugs.exe bug28 --bogus
// Actual:   Error: unknown option '--bogus'.
//           Run 'Bugs --help' for usage.
// Expected: Run 'Bugs bug28 --help' for usage.   (clap prints the failing subcommand's usage; cobra its UsageString)
//
// Also: Bugs.exe bug28 --prefix      (missing value) hints at root help too.
// Cause: Emit.Parsing.cs:43-48 hard-codes "Run '<app> --help'".

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

/// <summary>Options for the bug28 namespace.</summary>
public sealed class Bug28NsOpts : GlobalOpts
{
	/// <summary>A namespace-level prefix.</summary>
	public string? Prefix { get; set; }

	/// <summary>A namespace-level timeout (bug03's namespace variant).</summary>
	public TimeSpan? Timeout { get; set; }
}

internal static class Bug28
{
	/// <summary>Prints the namespace options.</summary>
	public static void Show(Bug28NsOpts o) => Console.Out.WriteLine($"prefix:{o.Prefix} timeout:{o.Timeout?.ToString() ?? "null"}");

	public static void Register(IArghRootBuilder app) =>
		app.MapNamespace("bug28", "Namespace with its own options.", g =>
		{
			g.UseNamespaceOptions<Bug28NsOpts>();
			g.Map("show", Show);
		});
}
