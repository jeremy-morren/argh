// Bug 38 - Root and namespace default handlers are missing from the schema when registered by alias
// Group E: completion and __schema
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG38     (MapAndRootAlias changes what `Bugs.exe` alone does)
// Run:      Bugs.exe                    -> bug38 root ran             (the alias handler runs with no arguments)
// Run:      Bugs.exe __schema
// Actual:   no "rootDefault" key
// Expected: "rootDefault": { ... } describing the alias target, so consumers can discover the invocation
//
// Cause: MapAndRootAlias targets live in RegistryNode.RootAlias, but EmitSchemaRootDefaultExpression
//        (Schema.cs:68) and EmitSchemaDefaultHandlerForNamespace (:94) read only RootCommand.

#if BUG38
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal sealed class Bug38Commands
{
	/// <summary>Alias target: runs when no command is given.</summary>
	[DefaultCommand]
	[CommandName("bug38-build")]
	[NoOptionsInjection]
	public static void Build() => Console.Out.WriteLine("bug38 root ran");
}

internal static class Bug38
{
	public static void Register(IArghRootBuilder app) => app.MapAndRootAlias<Bug38Commands>();
}
#endif
