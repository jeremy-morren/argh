// Bug 95 - A value-taking flag followed by another flag takes that flag as its value
// Group A: parsing and binding
//
// Run:      Bugs.exe bug95 --name --port 5             -> Error: unexpected argument '5' found.
// Run:      Bugs.exe bug95 --port --severity warning   -> Error: unexpected argument 'warning' found.
// Run:      Bugs.exe bug95 --port --severity=warning   -> Error: invalid int for --port: '--severity=warning'.
// Run:      Bugs.exe --port --severity warning bug95   -> Error: invalid int for --port: '--severity'.
// Run:      Bugs.exe bug95 --name --help               -> name:--help    (help is not shown)
// Run:      Bugs.exe bug95 --name -x                   -> name:-x
// Expected: clap: error: a value is required for '--name <NAME>' but none was supplied   (unless the option
//           allows hyphen values); an undefined "-x" is an unexpected argument
// Keep:     Bugs.exe bug95 --offset -5                 -> offset:-5      (negative numbers must stay values)
//
// Cause: the parser takes the token after a value-taking flag as its value whatever it looks like (pflag's
// rule); the small-fixes branch did this on purpose so that "--name --help" binds, see BugsA.md.

using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug95
{
	/// <summary>Prints a string option, a numeric option and two global options.</summary>
	/// <param name="g">Global options.</param>
	/// <param name="name">A string option.</param>
	/// <param name="offset">A numeric option that may be negative.</param>
	public static void Run(GlobalOpts g, string name = "", int offset = 0) =>
		Console.Out.WriteLine($"name:{name} offset:{offset} port:{g.Port} severity:{g.Severity}");

	public static void Register(IArghRootBuilder app) => app.Map("bug95", Run);
}
