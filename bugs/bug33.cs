// Bug 33 - Hidden commands, namespaces and flags are completed
// Group E: completion and __schema
//
// Run:      Bugs.exe __complete bash -- bug33-h
// Actual:   bug33-hidden
// Expected: (nothing)             (cobra: IsAvailableCommand() is false for Hidden; clap's dynamic engine drops hidden candidates)
//
// Run:      Bugs.exe __complete bash -- bug33 -
// Actual:   includes --internal-id, which is [Hidden]
// Expected: --help -h only
//
// [Hidden] is documented as "hidden from user-facing help and autocomplete" (Attributes.cs:202).
// Cause: EmitNextSegmentsArray (Completion.cs:448) and CollectCompletionFlagStrings (:459) never check IsHidden.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug33
{
	/// <summary>Visible command with a hidden flag.</summary>
	[NoOptionsInjection]
	public static void Run([Hidden] string? internalId = null) => Console.Out.WriteLine($"internal:{internalId}");

	/// <summary>Hidden command.</summary>
	[Hidden]
	[NoOptionsInjection]
	public static void HiddenCmd() => Console.Out.WriteLine("hidden ran");

	public static void Register(IArghRootBuilder app)
	{
		app.Map("bug33", Run);
		app.Map("bug33-hidden", HiddenCmd);
	}
}
