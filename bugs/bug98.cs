// Bug 98 - A collection member on a namespace or global options class doesn't compile
// Group B: generator - uncompilable output and missing diagnostics
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG98
// Actual:   ArghGenerated.g.cs: error CS0029: Cannot implicitly convert type 'string' to 'string[]'
//           ArghGenerated.g.cs: error CS0029: Cannot implicitly convert type 'string' to 'int[]'
//           (no Argh diagnostic; a string[] on the global options class gives the same error once per command)
// Expected: binds like the same member on an [AsParameters] DTO, which works (repeatable --tags a --tags b),
//           or an AGH diagnostic if collections aren't supported on options classes
//
// Cause: the options pre-parse and the runner's reconstruction of options members (Emit.Parsing.cs:256-333 and
//        :364-559 at 45954b6) treat every member as a scalar; neither has a collection branch.

#if BUG98
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

/// <summary>Namespace options with collection members.</summary>
public sealed class Bug98NsOpts : GlobalOpts
{
	/// <summary>Tags.</summary>
	public string[] Tags { get; set; } = [];

	/// <summary>Ids.</summary>
	public int[] Ids { get; set; } = [];
}

internal static class Bug98
{
	/// <summary>Prints the namespace options.</summary>
	public static void Show(Bug98NsOpts o) => Console.Out.WriteLine($"tags:{string.Join(",", o.Tags)} ids:{string.Join(",", o.Ids)}");

	public static void Register(IArghRootBuilder app) =>
		app.MapNamespace("bug98", "Namespace whose options have collection members.", g =>
		{
			g.UseNamespaceOptions<Bug98NsOpts>();
			g.Map("show", Show);
		});
}
#endif
