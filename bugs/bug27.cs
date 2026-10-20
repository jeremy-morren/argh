// Bug 27 - CommandContext.CommandPath leaks __argh_root for MapRoot handlers
// Group C: runtime, dispatch and hosting
//
// Run:      BUGS_ECHO_PATH=1 Bugs.exe bug37
// Actual:   [bug27] path=bug37/__argh_root
// Expected: [bug27] path=bug37        (the docs say an empty command name gives string.Empty)
//
// Cause: EmitCommandPathLiteral (Emit.Runner.cs:597-611) appends cmd.CommandName, which is
//        RootDefaultInternalCommandName (Analysis.cs:1298). The same name reaches __schema (bug37).

using Nullean.Argh.Middleware;

namespace Bugs;

/// <summary>Prints the command path when BUGS_ECHO_PATH is set.</summary>
internal sealed class Bug27Middleware : ICommandMiddleware
{
	public async ValueTask InvokeAsync(CommandContext context, CommandMiddlewareDelegate next)
	{
		if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BUGS_ECHO_PATH")))
			Console.Error.WriteLine($"[bug27] path={string.Join("/", context.CommandPath)}");
		await next(context);
	}
}
