// Bug 30 - ANSI colour on stderr is decided by whether stdout is redirected
// Group C: runtime, dispatch and hosting
//
// Run (in a terminal, not redirected):   Bugs.exe bug30 2> err.txt
// Actual:   err.txt contains escape codes:  Error: missing required flag ESC[1m--name ESC[0m...
// Expected: plain text, because stderr isn't a terminal   (clap wraps stdout and stderr in separate AutoStreams;
//           anstream also honours NO_COLOR, CLICOLOR_FORCE, CLICOLOR=0 and TERM=dumb)
//
// The reverse also holds: Bugs.exe bug30 | cat   prints an uncoloured error to the terminal's stderr.
// Cause: UseAnsiColors checks only NO_COLOR and Console.IsOutputRedirected (CliHelpFormatting.cs:13-15),
//        but errors and flag help go to stderr (Emit.Dispatch.cs:311).

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug30
{
	/// <summary>Fails with a coloured error when --name is missing.</summary>
	[NoOptionsInjection]
	public static void Run(string name) => Console.Out.WriteLine($"name:{name}");

	public static void Register(IArghRootBuilder app) => app.Map("bug30", Run);
}
