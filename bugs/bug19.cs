// Bug 19 - [Env] on a positional argument is shown in help but never read
// Group B: generator - uncompilable output and missing diagnostics
//
// Run:      BUGS_NAME=fromenv Bugs.exe bug19
// Actual:   Error: missing required argument <name>
// Expected: name:fromenv           (clap's `env` works on positionals as on options); or a diagnostic
//
// Bugs.exe bug19 --help prints `<name>  The name. [env: BUGS_NAME]`.
// Cause: Emit.Runner.cs:289 skips positionals in env binding; the help emitter still appends the tag.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug19
{
	/// <summary>Positional argument with an env fallback.</summary>
	/// <param name="name">The name.</param>
	[NoOptionsInjection]
	public static void Run([Argument, Env("BUGS_NAME")] string name) => Console.Out.WriteLine($"name:{name}");

	public static void Register(IArghRootBuilder app) => app.Map("bug19", Run);
}
