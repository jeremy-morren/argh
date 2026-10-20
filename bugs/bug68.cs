// Bug 68 - A non-constant initializer on an options property doesn't make it optional
// Group B: generator - uncompilable output and missing diagnostics
//
// Run:      Bugs.exe bug68 show
// Actual:   Error: missing required flag --timeout.
// Expected: timeout:00:00:30       (clap's default_value_t takes any expression; pflag defaults are ordinary values)
//
// The same goes for = TimeSpan.Zero, = new(...) and any method call. A literal default works.
// Cause: TryFormatInitializerOperation (CliParserGenerator.SymbolReaders.cs:392-435) recognises only literals
//        and constant or enum fields, and returns null for anything else, so the property counts as having no default.

using Nullean.Argh.Builder;

namespace Bugs;

/// <summary>Options for the bug68 namespace.</summary>
public sealed class Bug68NsOpts : GlobalOpts
{
	/// <summary>Timeout with a computed default.</summary>
	public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

internal static class Bug68
{
	/// <summary>Prints the timeout.</summary>
	public static void Show(Bug68NsOpts o) => Console.Out.WriteLine($"timeout:{o.Timeout}");

	public static void Register(IArghRootBuilder app) =>
		app.MapNamespace("bug68", "Namespace whose option has a computed default.", g =>
		{
			g.UseNamespaceOptions<Bug68NsOpts>();
			g.Map("show", Show);
		});
}
