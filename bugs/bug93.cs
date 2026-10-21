// Bug 93 - An optional non-nullable string with a string validation attribute gets CS8601/CS8604 warnings in generated code
// Group B: generator - uncompilable output and missing diagnostics
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG93
// Actual:   warning CS8601: Possible null reference assignment.          (__dto.Mode = __p_Mode; in TryParseDto_Bugs_Bug93NsOpts
//                                                                         and TryParseDtoExact_Bugs_Bug93NsOpts, per member)
//           warning CS8604: Possible null reference argument for parameter 'mode' in 'void Bug93.Run(string mode = "fast")'.
// Expected: no warnings; the default build of bugs/ has none
//
// #if'd because it adds warnings to an otherwise warning-free build; it compiles and runs.
// Cause: the constraint emitters guard the check with "x != null &&" whenever the value is optional (isNullable =
//        !p.IsRequired), even when the declared type is a non-nullable string. The null test makes the compiler treat the
//        string? local as maybe-null afterwards (Emit.Parsing.cs:939-948, 974-982, 991-1007, 1026-1042, 1061-1067).

#if BUG93
using System.ComponentModel.DataAnnotations;
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

/// <summary>Namespace options: optional non-nullable strings with string validation attributes.</summary>
public sealed class Bug93NsOpts : GlobalOpts
{
	/// <summary>Mode, fast or slow.</summary>
	[AllowedValues("fast", "slow")]
	public string Mode { get; set; } = "fast";

	/// <summary>Name, at most 10 characters.</summary>
	[StringLength(10)]
	public string Name { get; set; } = "x";

	/// <summary>Code, letters only.</summary>
	[RegularExpression("^[a-z]+$")]
	public string Code { get; set; } = "abc";

	/// <summary>Tag, anything but "none".</summary>
	[DeniedValues("none")]
	public string Tag { get; set; } = "t";

	/// <summary>Mail address.</summary>
	[EmailAddress]
	public string Mail { get; set; } = "a@b.c";

	/// <summary>Label, no attribute: no warning.</summary>
	public string Label { get; set; } = "l";
}

internal static class Bug93
{
	/// <summary>Prints the namespace options.</summary>
	public static void Show(Bug93NsOpts o) => Console.Out.WriteLine($"mode:{o.Mode} name:{o.Name}");

	/// <summary>Method parameter: the same shape gives CS8604 at the handler call.</summary>
	/// <param name="mode">Mode, fast or slow.</param>
	[NoOptionsInjection]
	public static void Run([AllowedValues("fast", "slow")] string mode = "fast") => Console.Out.WriteLine($"mode:{mode}");

	public static void Register(IArghRootBuilder app)
	{
		app.MapNamespace("bug93", "Namespace whose options carry string validation attributes.", g =>
		{
			g.UseNamespaceOptions<Bug93NsOpts>();
			g.Map("show", Show);
		});
		app.Map("bug93-param", Run);
	}
}
#endif
