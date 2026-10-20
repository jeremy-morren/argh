// Bug 16 - A cross-assembly [AsParameters] DTO with `required` members doesn't compile
// Group B: generator - uncompilable output and missing diagnostics
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG16
// Actual:   ArghGenerated.g.cs: error CS9035: Required member 'Bug16Req.Name' must be set in the object
//           initializer or attribute constructor   (generated: var __rt_default_... = new global::Bugs.Lib.Bug16Req();)
// Expected: compiles; --name is required
//
// The DTO lives in bugs/lib/bug16.cs. A `required` member in the same assembly works.
// Cause: isCrossAssemblyDefault (ParameterModel.cs:316, 604) ignores IPropertySymbol.IsRequired, so the
//        emit at Emit.Runner.cs:636-645 instantiates the type to read its defaults.

#if BUG16
using Bugs.Lib;
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug16
{
	/// <summary>Binds a DTO from another assembly that has a required member.</summary>
	[NoOptionsInjection]
	public static void Run([AsParameters] Bug16Req r) => Console.Out.WriteLine($"{r.Name}:{r.Count}");

	public static void Register(IArghRootBuilder app) => app.Map("bug16", Run);
}
#endif
