// Bug 17 - A nullable enum parameter loses its nullability
// Group B: generator - uncompilable output and missing diagnostics
//
// Run:      Bugs.exe bug17
// Actual:   level:Trace            (generated: global::Bugs.Severity level = default;)
// Expected: level:null             (clap: Option<Level> is None when absent)
//
// Also: Bugs.exe bug17 --help shows [default: default] (bug36) and __schema reports "defaultValue": "default".
// Cause: IsNullableAnnotated is set only on the options-property paths (ParameterModel.cs:349, 410, 639),
//        never on the method-parameter path (:253, :495), so GetCSharpCliType (Emit.Runner.cs:797-803)
//        returns the non-nullable enum.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug17
{
	/// <summary>Nullable enum with a null default.</summary>
	[NoOptionsInjection]
	public static void Run(Severity? level = null) => Console.Out.WriteLine($"level:{level?.ToString() ?? "null"}");

	public static void Register(IArghRootBuilder app) => app.Map("bug17", Run);
}
