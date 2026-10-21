// Bug 18 - Duplicate CLI names created by <param> overrides aren't diagnosed; both parameters bind
// Group B: generator - uncompilable output and missing diagnostics
//
// Run:      Bugs.exe bug18 --label A
// Actual:   name:A label:A         (one flag bound both parameters; --name is "unknown option")
// Expected: a build-time AGH0007 diagnostic (clap and cobra reject two arguments with the same long name)
//
// Also: Bugs.exe bug18 --help lists --label twice.
// Cause: ReportDuplicateCliNamesAcc runs on the raw models (CommandModel.cs:68, 103), but the explicit
//        names from <param> are applied later by ApplyParamDocumentation (:82, 117; body :338-375).
//
// Same family: a command parameter named like a global option compiles silently too.
// Run:      Bugs.exe bug18-port --help          -> only the global --port row; the command's --port is not listed
// Run:      Bugs.exe bug18-port --port 5        -> cmd-port:5 global-port:5    (one flag binds both)
// Run:      Bugs.exe --port 5 bug18-port        -> cmd-port:1 global-port:5    (only the global one)

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug18
{
	/// <summary>Two parameters that end up with the same CLI name.</summary>
	/// <param name="name">--label, The name, renamed to --label.</param>
	/// <param name="label">The label.</param>
	[NoOptionsInjection]
	public static void Run(string name = "", string label = "") => Console.Out.WriteLine($"name:{name} label:{label}");

	/// <summary>A command parameter named like the global --port.</summary>
	/// <param name="g">Global options.</param>
	/// <param name="port">The command's own port.</param>
	public static void SamePort(GlobalOpts g, int port = 1) => Console.Out.WriteLine($"cmd-port:{port} global-port:{g.Port}");

	public static void Register(IArghRootBuilder app)
	{
		app.Map("bug18", Run);
		app.Map("bug18-port", SamePort);
	}
}
