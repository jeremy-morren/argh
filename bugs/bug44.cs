// Bug 44 - Blank lines and code styling in remarks differ between colour and plain mode
// Group D: help and XML docs
//
// Run:      NO_COLOR=1 Bugs.exe bug44 --help      vs      Bugs.exe bug44 --help   (in a real terminal)
// Actual:   plain mode prints one blank line after "Notes:" and none between the paragraphs;
//           colour mode prints two and one, plus trailing blanks; in the <code> block the first line is
//           unstyled and the second is grey.
// Expected: identical layout in both modes
//
// Cause: whitespace-only and multi-line text nodes are wrapped whole in SGR (XmlDocumentationRenderer.cs:97),
//        so CollapseBlankRuns / TrimEnd (:35) and the empty-line check (:56) no longer see "\n\n", and code
//        lines glued to a trailing \x1b[0m skip DocRemarksLine.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug44
{
	/// <summary>Multi-paragraph remarks with a code block.</summary>
	/// <remarks>
	/// <para>First paragraph of the notes.</para>
	/// <para>Second paragraph of the notes.</para>
	/// <code>
	/// bugs bug44 --first
	/// bugs bug44 --second
	/// </code>
	/// <para>Closing paragraph.</para>
	/// </remarks>
	[NoOptionsInjection]
	public static void Run() => Console.Out.WriteLine("bug44");

	public static void Register(IArghRootBuilder app) => app.Map("bug44", Run);
}
