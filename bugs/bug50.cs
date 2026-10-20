// Bug 50 - Multi-line Notes: starts with a blank line; two blank lines before it without Options:
// Group D: help and XML docs
//
// Run:      NO_COLOR=1 Bugs.exe bug50 --help | cat -A
// Actual:   two empty lines before "Notes:" (no Options: section), and an empty line right after "Notes:"
// Expected: one blank line before Notes:, none after it
//
// Cause: the inner XML starts with "\n" and XmlDocumentationRenderer.cs:35 only trims the end;
//        Emit.Help.cs:782 and :801 both print a blank line when there is no Options: section.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug50
{
	/// <summary>Command without options and with multi-line remarks.</summary>
	/// <remarks>
	/// <para>First paragraph.</para>
	/// <para>Second paragraph.</para>
	/// </remarks>
	[NoOptionsInjection]
	public static void Run() => Console.Out.WriteLine("bug50");

	public static void Register(IArghRootBuilder app) => app.Map("bug50", Run);
}
