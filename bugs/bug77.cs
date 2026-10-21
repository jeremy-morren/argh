// Bug 77 - The Console.CancelKeyPress handler is never removed and captures a disposed token source
// Group C: runtime, dispatch and hosting
//
// Program.cs runs ArghRuntime.RunAsync a second time when BUGS_RUN_TWICE is set.
// Run (in a real console):   BUGS_RUN_TWICE=1 Bugs.exe bug77      then press Ctrl+C during the SECOND run
// Actual:   Unhandled exception. System.ObjectDisposedException: The CancellationTokenSource has been disposed
//           (the first run's handler fires first and cancels its disposed source), exit 0xE0434352
// Expected: the second run is cancelled cleanly, like the first
//
// Generated code (Emit.Dispatch.cs:333-335):
//     using var cts = ...;
//     Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };   // never removed
// The same crash happens for a Ctrl+C that arrives after RunAsync returned but before the process exits.
// Not reproducible under a pipe (no console event reaches the process): observed with GenerateConsoleCtrlEvent.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug77
{
	/// <summary>Waits for Ctrl+C.</summary>
	[NoOptionsInjection]
	public static async Task Run(CancellationToken ct)
	{
		Console.Out.WriteLine("waiting 10s; press Ctrl+C");
		try
		{
			await Task.Delay(10_000, ct);
			Console.Out.WriteLine("not cancelled");
		}
		catch (OperationCanceledException)
		{
			Console.Out.WriteLine("cancelled cleanly");
		}
	}

	public static void Register(IArghRootBuilder app) => app.Map("bug77", Run);
}
