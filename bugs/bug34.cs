// Bug 34 - __schema advertises command aliases that don't route or complete
// Group E: completion and __schema
//
// Run:      Bugs.exe __schema            -> "name": "bug34-renamed", "aliases": ["b34"]
// Run:      Bugs.exe b34
// Actual:   Error: unknown command or namespace 'b34'.   exit 2
// Expected: bug34 ran                    (clap's aliases_to and cobra's HasAlias both route aliases)
//
// Run:      Bugs.exe __complete bash -- b3    -> only bug34-renamed (completing aliases is optional: cobra doesn't)
// Cause: CommandAliases is used nowhere except Schema.cs:160.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal sealed class Bug34Commands
{
	/// <summary>Command with an alias.</summary>
	[CommandName("bug34-renamed", "b34")]
	[NoOptionsInjection]
	public static void Renamed() => Console.Out.WriteLine("bug34 ran");
}

internal static class Bug34
{
	public static void Register(IArghRootBuilder app) => app.Map<Bug34Commands>();
}
