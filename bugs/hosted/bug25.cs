// Bug 25 - Scoped handler types are resolved from the root provider
// Group C: runtime, dispatch and hosting
//
// Build:    dotnet build bugs/hosted -c Release
// Run:      DOTNET_ENVIRONMENT=Development Bugs.Hosted.exe bug25
// Actual:   InvalidOperationException: Cannot resolve scoped service 'Bugs.Hosted.Bug25Commands' from root provider   exit 1
//           In Production it runs, but "[Bug25Dep] disposed" is never printed.
// Expected: bug25 ran, then "[Bug25Dep] disposed"   (docs: "Resolved per command invocation")
//
// Cause: the generated code calls ArghServices.ServiceProvider?.GetService(typeof(T)) (Emit.Runner.cs:449-460,
//        Emit.Dispatch.cs:390-397) and Hosting assigns the root provider (ArghHostingCliService.cs:38).
//        No IServiceScope is created.

namespace Bugs.Hosted;

public sealed class Bug25Dep : IDisposable
{
	public void Dispose() => Console.Error.WriteLine("[Bug25Dep] disposed");
}

public sealed class Bug25Commands(Bug25Dep dep)
{
	/// <summary>Uses a scoped dependency.</summary>
	public void Bug25()
	{
		_ = dep;
		Console.Out.WriteLine("bug25 ran");
	}
}
