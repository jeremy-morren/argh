// Bug 49 - <br/> is dropped, joining words
// Group D: help and XML docs
//
// Run:      Bugs.exe --help | grep bug49
// Actual:   bug49   Line oneLine two
// Expected: bug49   Line one Line two   (a line break, or at least a space)
//
// Run:      Bugs.exe bug49 --help        -> Notes:  Remark oneRemark two
// Cause: FlattenNodes (Documentation.cs:684-747) has no "br" case.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug49
{
	/// <summary>Line one<br/>Line two</summary>
	/// <remarks>Remark one<br/>Remark two</remarks>
	[NoOptionsInjection]
	public static void Run() => Console.Out.WriteLine("bug49");

	public static void Register(IArghRootBuilder app) => app.Map("bug49", Run);
}
