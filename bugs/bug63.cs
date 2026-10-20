// Bug 63 - Enum collections lose their member names in __schema
// Group E: completion and __schema
//
// Run:      Bugs.exe __schema
// Actual:   bug63 --levels: "type": "array", "elementType": "string"   (no enumValues)
//           bug63 --level:  "type": "enum", "enumValues": ["trace", "information", "warning"]
// Expected: the choices for --levels too. cli-schema v1 only allows string|integer|number|boolean as elementType,
//           so the spec-legal shape is elementType "string" plus
//           "validations": [{"kind": "allowed", "values": ["trace", "information", "warning"]}]
//
// Cause: Schema.cs:450 emits enumValues only when type == "enum"; :460 forces elementType to "string".

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug63
{
	/// <summary>A single enum flag and a collection of enums.</summary>
	/// <param name="level">One level.</param>
	/// <param name="levels">Several levels.</param>
	[NoOptionsInjection]
	public static void Run(Severity level = Severity.Trace, List<Severity>? levels = null) =>
		Console.Out.WriteLine($"level:{level} levels:{string.Join(",", levels ?? [])}");

	public static void Register(IArghRootBuilder app) => app.Map("bug63", Run);
}
