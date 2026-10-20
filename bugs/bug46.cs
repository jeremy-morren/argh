// Bug 46 - Single-line Notes: skips the remarks transforms that multi-line Notes: applies
// Group D: help and XML docs
//
// Run:      Bugs.exe bug46 --help
// Actual:   Notes:  Set name and see Bug46Multi; code and bold.          (bare names, no styling)
// Run:      Bugs.exe bug46-multi --help
// Actual:   Set --name and see Bugs bug46-multi --name <string>; ...     (transformed and styled)
// Expected: both the same: <paramref> becomes --name, <see cref> of a handler becomes its usage line
//
// Cause: Emit.Help.cs:112-122 prefers the plain cmd.RemarksRendered, built by FlattenNodes
//        (Documentation.cs:725-731, which appends the raw name); only the multi-line path (:128) uses
//        TransformRemarksInnerXmlForHelp (Documentation.cs:278-284).

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug46
{
	/// <summary>Single-line remarks.</summary>
	/// <remarks>Set <paramref name="name"/> and see <see cref="Multi"/>; <c>code</c> and <b>bold</b>.</remarks>
	/// <param name="name">The name.</param>
	[NoOptionsInjection]
	public static void Single(string name = "") => Console.Out.WriteLine(name);

	/// <summary>Multi-line remarks with the same content inside a para.</summary>
	/// <remarks>
	/// <para>Set <paramref name="name"/> and see <see cref="Single"/>; <c>code</c> and <b>bold</b>.</para>
	/// <para>A second paragraph, so the remarks are multi-line.</para>
	/// </remarks>
	/// <param name="name">The name.</param>
	[NoOptionsInjection]
	public static void Multi(string name = "") => Console.Out.WriteLine(name);

	public static void Register(IArghRootBuilder app)
	{
		app.Map("bug46", Single);
		app.Map("bug46-multi", Multi);
	}
}
