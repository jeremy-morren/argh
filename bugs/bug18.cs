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

	public static void Register(IArghRootBuilder app) => app.Map("bug18", Run);
}
