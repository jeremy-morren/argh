// Bug 26 - Ctrl+C prints a stack trace with generated-file paths and exits 1
// Group C: runtime, dispatch and hosting
//
// Run:      Bugs.exe bug26        then press Ctrl+C within 10 seconds
// Actual:   a 12-line OperationCanceledException stack trace including "ArghGenerated.g.cs:line NNNN", exit 1
// Expected: one line, no trace, non-zero exit   (cobra: "Error: context canceled", exit 1; a shell reports 130 for SIGINT)
//
// Any unhandled handler exception prints the same kind of trace:
// Run:      Bugs.exe bug26 --throw
// Actual:   System.InvalidOperationException: boom  + stack trace with generated-file paths
//
// The token itself works: both console Ctrl+C and IHostApplicationLifetime.StopApplication() cancel it.
// Cause: RunWithCancellationAsync catches Exception and writes ex.ToString() (Emit.Dispatch.cs:347-351).

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug26
{
	/// <summary>Waits on the cancellation token, or throws with --throw.</summary>
	[NoOptionsInjection]
	public static async Task Run(bool @throw, CancellationToken ct)
	{
		if (@throw)
			throw new InvalidOperationException("boom");
		Console.Out.WriteLine("waiting 10s; press Ctrl+C");
		await Task.Delay(10_000, ct);
		Console.Out.WriteLine("not cancelled");
	}

	public static void Register(IArghRootBuilder app) => app.Map("bug26", Run);
}
