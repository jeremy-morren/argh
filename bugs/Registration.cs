using System.Runtime.CompilerServices;
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

/// <summary>Severity levels (global option).</summary>
public enum Severity
{
	/// <summary>Everything.</summary>
	Trace,
	/// <summary>Normal.</summary>
	Information,
	/// <summary>Problems only.</summary>
	Warning,
}

/// <summary>Global options shared by every bug command. Namespace option types derive from it.</summary>
public class GlobalOpts
{
	/// <summary>API URL; falls back to BUGS_API_URL (bug01).</summary>
	[Env("BUGS_API_URL")]
	public string? ApiUrl { get; set; }

	/// <summary>Severity (bug03).</summary>
	public Severity Severity { get; set; } = Severity.Information;

	/// <summary>Port (bug75).</summary>
	public int Port { get; set; } = 80;
}

internal static class Registration
{
	[ModuleInitializer]
	internal static void Register()
	{
		IArghRootBuilder app = new ArghBuilder();
		app.UseGlobalOptions<GlobalOpts>();
		app.UseMiddleware<Bug27Middleware>();

		// A. Parsing and binding
		Bug01.Register(app);
		Bug02.Register(app);
		Bug03.Register(app);
		Bug04.Register(app);
		Bug05.Register(app);
		Bug06.Register(app);
		Bug07.Register(app);
		Bug08.Register(app);
		Bug09.Register(app);

		// B. Generator: uncompilable output and missing diagnostics.
		// The #if'd ones break the build on purpose: dotnet build bugs -p:DefineConstants=BUG10 etc.
#if BUG12
		Bug12.Register(app);
#endif
#if BUG14
		Bug14.Register(app);
#endif
#if BUG16
		Bug16.Register(app);
#endif
		Bug18.Register(app);
#if BUG21
		Bug21.Register(app);
#endif
#if BUG55
		Bug55.Register(app);
#endif
#if BUG59
		Bug59.Register(app);
#endif
#if BUG60
		Bug60.Register(app);
#endif
		Bug61.Register(app);
#if BUG70
		Bug70.Register(app);
#endif

		// C. Runtime, dispatch and hosting (bug22 and bug25 live in bugs/hosted)
		Bug23.Register(app);
		Bug24.Register(app);
		Bug26.Register(app);
		Bug28.Register(app);
		Bug29.Register(app);
		Bug30.Register(app);

		// D. Help and XML docs
		Bug42.Register(app);
		Bug43.Register(app);
		Bug44.Register(app);
		Bug45.Register(app);
		Bug46.Register(app);
		Bug47.Register(app);
		Bug48.Register(app);
		Bug49.Register(app);
		Bug50.Register(app);
		Bug51.Register(app);
		Bug52.Register(app);
		Bug54.Register(app);
		Bug56.Register(app);
		Bug57.Register(app);
#if BUG64
		Bug64.Register(app);
#endif

		// E. Completion and __schema (bug31, bug32, bug40 and bug41 are comment-only)
		Bug33.Register(app);
		Bug34.Register(app);
		Bug35.Register(app);
		Bug37.Register(app);
#if BUG38
		Bug38.Register(app);
#endif
		Bug39.Register(app);
		Bug62.Register(app);
		Bug63.Register(app);

		// Found on other branches
		Bug68.Register(app);
		Bug69.Register(app);

		// Third pass: parsing and runtime (bug78 and bug79 live in bugs/hosted)
		Bug73.Register(app);
		Bug75.Register(app);
		Bug76.Register(app);
		Bug77.Register(app);
		Bug80.Register(app);
		Bug81.Register(app);
#if BUG82
		Bug82.Register(app);
#endif
		Bug83.Register(app);

		// Found while building range-unbounded (#90)
		Bug88.Register(app);
		Bug91.Register(app);

		// Feature requests (see the Feature requests section of BugsB.md)
#if FEAT01
		Feat01.Register(app);
#endif
	}
}
