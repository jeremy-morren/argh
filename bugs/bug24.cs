// Bug 24 - ValueTask lambdas aren't awaited
// Group C: runtime, dispatch and hosting
//
// Run:      Bugs.exe bug24 --msg m
// Actual:   (nothing)   exit 0, before the lambda finishes; the build emits CS4014 in the generated file
// Expected: done m
//
// Method-group handlers returning ValueTask are awaited correctly.
// Cause: EmitLambdaInvocation (Emit.Runner.cs:1775-1800) handles Task, Task<T>, Task<int>, ValueTask<int>
//        and int; everything else becomes `__lambdaDelegate(args);`.

using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug24
{
	public static void Register(IArghRootBuilder app) =>
		app.Map("bug24", async ValueTask (string msg) =>
		{
			await Task.Delay(200);
			Console.Out.WriteLine("done " + msg);
		});
}
