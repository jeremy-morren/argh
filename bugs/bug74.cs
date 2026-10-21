// Bug 74 - --help anywhere in the arguments wins, so it can't be passed as a value
// Group A: parsing and binding
//
// Run:      Bugs.exe bug74 --name --help
// Actual:   the help page, exit 0
// Expected: name:--help          (clap and cobra bind --help as the value of --name here)
//
// Also: bug74 --name x -- --help -> help (bug7: no terminator); bug74 --bogus --help -> help, no error;
//       only --name=--help binds. Namespaces don't pre-scan (bug29's `bug29 --bogus --help` is an error).
// Cause: Emit.Runner.cs:72-79 scans every argument for --help / -h before parsing.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug74
{
	/// <summary>Takes a name that might look like a flag.</summary>
	[NoOptionsInjection]
	public static void Run(string name) => Console.Out.WriteLine($"name:{name}");

	public static void Register(IArghRootBuilder app) => app.Map("bug74", Run);
}
