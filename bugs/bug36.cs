// Bug 36 - defaultValue: "default" for nullable value types
// Group E: completion and __schema (also help)
//
// Run:      Bugs.exe __schema           -> bug36 --count has "defaultValue": "default"; so does --span
// Run:      Bugs.exe bug36 --help       -> --count <int>  ... [default: default]
// Expected: no defaultValue / no [default: ...]   (clap prints [default: X] only for a set default;
//                                                   pflag omits (default ...) for zero values)
//
// Cause: ParameterModel.cs:987 turns a null default on a value type into the literal "default";
//        GetSchemaDefaultValue (Schema.cs:486) filters only "null"; Emit.Help.cs:909 prints whatever it gets.
//        Nullable enums (bug17) hit the same path.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug36
{
	/// <summary>Nullable value types with null defaults.</summary>
	/// <param name="count">A count.</param>
	/// <param name="span">A span.</param>
	[NoOptionsInjection]
	public static void Run(int? count = null, TimeSpan span = default) =>
		Console.Out.WriteLine($"count:{count?.ToString() ?? "null"} span:{span}");

	public static void Register(IArghRootBuilder app) => app.Map("bug36", Run);
}
