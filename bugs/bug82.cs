// Bug 82 - AGH0003 fires when injected options precede an [Argument] parameter
// Group B: generator - uncompilable output and missing diagnostics
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG82
// Actual:   error AGH0003: Parameters marked with [Argument] must start at position 0 and be consecutive.
// Expected: compiles; ([Argument] string who, GlobalOpts g) already does, and the injected options
//           aren't CLI positionals
//
// Cause: the injected options are expanded as flags at their parameter position before the check in
//        Validation.cs:183-196 runs (CommandModel.cs:70, 105).

#if BUG82
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug82
{
	/// <summary>Injected options first, then a positional.</summary>
	public static void Run(GlobalOpts g, [Argument] string who) => Console.Out.WriteLine($"{who} {g.Severity}");

	public static void Register(IArghRootBuilder app) => app.Map("bug82", Run);
}
#endif
