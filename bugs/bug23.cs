// Bug 23 - Lambda commands inside MapNamespace crash or run the wrong handler
// Group C: runtime, dispatch and hosting
//
// Run:      Bugs.exe bug23 lam --s x
// Actual:   System.NullReferenceException ...   exit 1
// Expected: ns-lam x
//
// Run:      Bugs.exe bug23
// Actual:   NullReferenceException (or, if the app also has a root MapRoot lambda, InvalidCastException
//           "Unable to cast Action<string> to Action" because it picks up the app-level lambda; with
//           matching signatures it would silently run the wrong handler)
// Expected: ns-root-default
//
// Cause: ArghApp.Map/MapRoot store lambdas under "ns/name" and "ns/__argh_root" (ArghApp.cs:25-29, 51-57),
//        but the generator computes LambdaStorageKey with an empty prefix (Analysis.cs:1225, 1331); the
//        re-prefix step only rewrites RoutePrefix and RunMethodName (:576-583, 605-609), so the generated
//        code calls GetRegisteredLambda("lam").

using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug23
{
	public static void Register(IArghRootBuilder app) =>
		app.MapNamespace("bug23", "Namespace with lambda commands.", g =>
		{
			g.Map("lam", (string s) => Console.Out.WriteLine("ns-lam " + s));
			g.MapRoot(() => Console.Out.WriteLine("ns-root-default"));
		});
}
