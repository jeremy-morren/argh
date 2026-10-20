// Bug 67 - A required namespace option is only accepted before the command
// Group A: parsing and binding
//
// Run:      Bugs.exe bug67 show --name x
// Actual:   Error: missing required flag --name.   exit 2
// Run:      Bugs.exe bug67 --name x show
// Actual:   name:x                                  (works)
// Expected: both work   (clap's global(true) arguments and cobra's persistent flags go before or after the
//           subcommand; required checks run after the whole line is parsed)
//
// Optional namespace options are accepted on either side.
// Cause: the required check runs in the namespace pre-parse, before the command is matched
//        (Emit.Parsing.cs:302-309), and fails before the command's own arguments are read.

using Nullean.Argh.Builder;

namespace Bugs;

/// <summary>Options for the bug67 namespace.</summary>
public sealed class Bug67NsOpts : GlobalOpts
{
	/// <summary>Required name.</summary>
	public string Name { get; set; } = null!;
}

internal static class Bug67
{
	/// <summary>Prints the required namespace option.</summary>
	public static void Show(Bug67NsOpts o) => Console.Out.WriteLine($"name:{o.Name}");

	public static void Register(IArghRootBuilder app) =>
		app.MapNamespace("bug67", "Namespace with a required option.", g =>
		{
			g.UseNamespaceOptions<Bug67NsOpts>();
			g.Map("show", Show);
		});
}
