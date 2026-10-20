// Bug 39 - [Hidden(Schema = true)] on a namespace is silently dropped
// Group E: completion and __schema
//
// Run:      Bugs.exe __schema
// Actual:   the "bug39" namespace appears like any other; nothing marks it hidden
// Expected: per the attribute docs, "hidden": true   (cli-schema v1 has no hidden on namespaces yet:
//           cli-schema/cli-schema#7; until then omit the namespace or mark its commands hidden)
//
// Cause: CliNamespaceSchema has no hidden field, and EmitCliNamespaceSchemaBody (Schema.cs:243)
//        ignores ch.IsHiddenInSchema.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

/// <summary>Namespace hidden from help and, supposedly, from the schema.</summary>
[Hidden(Schema = true)]
internal sealed class Bug39Ns
{
	/// <summary>A command inside the hidden namespace.</summary>
	[NoOptionsInjection]
	public static void Inner() => Console.Out.WriteLine("inner");
}

internal static class Bug39
{
	public static void Register(IArghRootBuilder app) => app.MapNamespace<Bug39Ns>("bug39");
}
