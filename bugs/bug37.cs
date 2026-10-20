// Bug 37 - __argh_root leaks into schema usage strings (and bug27: into CommandContext.CommandPath)
// Group E: completion and __schema
//
// Run:      Bugs.exe __schema
// Actual:   namespace bug37 -> "defaultCommand": { ..., "usage": "Bugs bug37 __argh_root" }
// Expected: "usage": "Bugs bug37"      (clap/cobra: `app storage [OPTIONS]`)
//
// Cause: Schema.cs:110 via BuildCommandUsageSynopsisTail with CommandName == "__argh_root".
// The same internal name reaches middleware as CommandContext.CommandPath = "bug37/__argh_root" (bug27,
// Emit.Runner.cs:597-611); see bug27.cs.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug37
{
	/// <summary>Namespace default handler.</summary>
	[NoOptionsInjection]
	public static void Root() => Console.Out.WriteLine("bug37 root");

	public static void Register(IArghRootBuilder app) =>
		app.MapNamespace("bug37", "Namespace with a default handler.", g => g.MapRoot(Root));
}
