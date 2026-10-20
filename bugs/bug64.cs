// Bug 64 - A very long command name widens the listing column without a cap
// Group D: help and XML docs
//
// Run:      NO_COLOR=1 Bugs.exe --help
// Actual:   every command's summary is squeezed into 12-character slivers to the right of a 97-column name column
// Expected: the long name wraps onto its own line and the column stays narrow   (clap starts the description on
//           the next line when a name is too long; cobra pads to the longest name like Argh)
//
// No exception and no negative width: Emit.Dispatch.cs:653, 749 take the longest name, CliHelpFormatting.cs:137
// widens further at runtime, and :152 clamps the summary column to at least 12.
// This file is #if'd because it makes the whole listing unreadable:  -p:DefineConstants=BUG64

#if BUG64
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug64
{
	/// <summary>Command with a ninety-seven character name.</summary>
	[NoOptionsInjection]
	public static void Run() => Console.Out.WriteLine("bug64");

	public static void Register(IArghRootBuilder app) =>
		app.Map("bug64-" + new string('x', 91), Run);
}
#endif
