// Bug 48 - Element-only <para> content is pretty-printed with newlines and indent
// Group D: help and XML docs
//
// Run:      Bugs.exe bug48 --help
// Actual:   Notes:
//               Bold
//               Italic
//               a
//               b                     (four lines, each indented)
// Expected: BoldItalic a b on one line (as the IDE renders it)
//
// Cause: XElement.ToString() (Documentation.cs:425, :317) pretty-prints element-only content, and remarks
//        mode preserves the result; the embedded string is "<para>\r\n  <b>Bold</b>\r\n  <i>Italic</i>\r\n</para>...".

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug48
{
	/// <summary>Remarks whose para holds only elements.</summary>
	/// <remarks>
	/// <para><b>Bold</b><i>Italic</i></para>
	/// <para><c>a</c><c>b</c></para>
	/// </remarks>
	[NoOptionsInjection]
	public static void Run() => Console.Out.WriteLine("bug48");

	public static void Register(IArghRootBuilder app) => app.Map("bug48", Run);
}
