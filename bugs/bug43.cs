// Bug 43 - Inline styles leak to the rest of the line in colour mode
// Group D: help and XML docs
//
// Run (in a real terminal, colour on):   Bugs.exe bug43 --help
// Actual:   the text after <c>code</c> stays bold:  ESC[1mcode ESC[90mthen plain text that should not be bold.ESC[m
//           (capture with a ConPTY harness, or `script`, to see the escape codes)
// Expected: the style ends after the span
//
// Affects <c>, <b>, <i>, <u>, <see langword> and OSC 8 links.
// Cause: DocStyledSpan ends the span with the base colour (\x1b[90m or 37m) instead of a reset:
//        CliHelpFormatting.cs:105-111 (cont) and :122 (Osc8Hyperlink).

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug43
{
	/// <summary>Use <c>code</c> then plain text that should not be bold. Also <b>bold</b> and <see langword="null"/> here.</summary>
	/// <remarks>
	/// <para>A <c>styled</c> remark, followed by plain text, and a link <see href="https://example.com">example</see> then text.</para>
	/// </remarks>
	[NoOptionsInjection]
	public static void Run() => Console.Out.WriteLine("bug43");

	public static void Register(IArghRootBuilder app) => app.Map("bug43", Run);
}
