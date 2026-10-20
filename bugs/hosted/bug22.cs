// Bug 22 - MapScoped, MapSingleton and MapTransient register nothing
// Group C: runtime, dispatch and hosting
//
// Build:    dotnet build bugs/hosted -c Release
// Run:      Bugs.Hosted.exe bug22
// Actual:   Error: unknown command or namespace 'bug22'.      (with only MapScoped registrations: "No commands are registered.")
// Expected: bug22 ran
//
// b.Map<T>(ServiceLifetime.Scoped) works because the invocation is named Map.
// Cause: the generator scans invocations named Map, MapNamespace, MapRoot, MapAndRootAlias, UseGlobalOptions,
//        UseNamespaceOptions, UseMiddleware, UseCliDescription, UseSchemaVersion and DocumentEnvironmentVariables
//        (CliParserGenerator.cs:54-64, Analysis.cs:61-82). The hosting builder's lifetime variants
//        (ArghHostingBuilder.cs:46-56) aren't in the list, although the docs lead with them.

namespace Bugs.Hosted;

public sealed class Bug22Commands
{
	/// <summary>Registered with MapScoped.</summary>
	public void Bug22() => Console.Out.WriteLine("bug22 ran");
}
