// Bug 94 - A namespace's --help lists only -h, --help: no global options and none of the namespace's own options
// Group D: help and XML docs
//
// Run:      Bugs.exe bug94 --help
// Actual:   Global options:
//             -h, --help  Show help.
//           Commands:
//             bug94 show  ...
// Expected: the global options (--api-url, --severity, --port) and the namespace option --region, as
//           "Bugs.exe bug94 show --help" already lists them (cobra: "Flags:" and "Global Flags:" on every command,
//           including a parent with only subcommands; clap: global args propagate into every subcommand's help)
// Also:     Bugs.exe --help lists only -h, --help and --version (the app has no root default command), and
//           Bugs.exe bug37 --help (a namespace with a default handler) does list the global options.
//
// Cause: Emit.Dispatch.cs:635-636 (root) and :729-731 (namespace) show those rows only when there is a default
//        command, or for a namespace an XML <summary>; MapNamespace("bug94", "description", ...) has neither.

using Nullean.Argh.Builder;

namespace Bugs;

/// <summary>Namespace options with one option of their own.</summary>
public sealed class Bug94NsOpts : GlobalOpts
{
	/// <summary>Region for every command in the namespace.</summary>
	public string? Region { get; set; }
}

internal static class Bug94
{
	/// <summary>Prints the region.</summary>
	public static void Show(Bug94NsOpts o) => Console.Out.WriteLine($"region:{o.Region ?? "-"} port:{o.Port}");

	public static void Register(IArghRootBuilder app) =>
		app.MapNamespace("bug94", "Namespace with its own option.", g =>
		{
			g.UseNamespaceOptions<Bug94NsOpts>();
			g.Map("show", Show);
		});
}
