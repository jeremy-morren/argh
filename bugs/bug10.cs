// Bug 10 - An enum parameter with a non-zero default doesn't compile (open issue #75)
// Group B: generator - uncompilable output and missing diagnostics
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG10
// Actual:   ArghGenerated.g.cs: error CS0266: Cannot implicitly convert type 'int' to 'Bugs.Level'
//           (generated: global::Bugs.Level level = 10;)
// Expected: compiles; help shows [default: high]
//
// Level.Low (= 0) compiles only because the literal 0 converts to any enum.
// Cause: TryGetDefaultLiteral (ParameterModel.cs:977-996) formats ExplicitDefaultValue, the underlying
//        integer, through IFormattable. Options-type properties go through QualifyOptionsEnumDefaultLiteral.

#if BUG10
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

public enum Level { Low = 0, Mid = 5, High = 10 }

internal static class Bug10
{
	/// <summary>Enum parameter with a non-zero default.</summary>
	[NoOptionsInjection]
	public static void Run(Level level = Level.High) => Console.Out.WriteLine($"level:{level}");

	public static void Register(IArghRootBuilder app) => app.Map("bug10", Run);
}
#endif
