// Bug 79 - Hosting's Environment.Exit skips StopAsync and disposal of other services
// Group C: runtime, dispatch and hosting
//
// Build:    dotnet build bugs/hosted -c Release
// Run:      Bugs.Hosted.exe bug78 --body hi      (or --help)
// Actual:   "[Bug79Service] started" is printed; "stopped" and "disposed" never are
// Expected: StopAsync and Dispose run on shutdown, as for any host
//
// docs/hosting/index.md:34 says the host doesn't continue after the CLI, but not that shutdown is skipped.
// Cause: ArghHostingCliService.cs:59 calls Environment.Exit.

using Microsoft.Extensions.Hosting;

namespace Bugs.Hosted;

public sealed class Bug79Service : IHostedService, IDisposable
{
	public Task StartAsync(CancellationToken cancellationToken)
	{
		Console.Error.WriteLine("[Bug79Service] started");
		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		Console.Error.WriteLine("[Bug79Service] stopped");
		return Task.CompletedTask;
	}

	public void Dispose() => Console.Error.WriteLine("[Bug79Service] disposed");
}
