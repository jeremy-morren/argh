// Bug 51 - Numbered lists flatten to "-"; <listheader> and <term> are dropped
// Group D: help and XML docs
//
// Run:      Bugs.exe --help | grep -A2 bug51        (listing / single-line flattening)
// Actual:   bug51   Steps: - first - second
// Expected: Steps: 1. first 2. second
//
// Run:      Bugs.exe bug51 --help
// Actual:   the table list shows neither the header row nor the terms; items appear as bullets
// Expected: Name    Meaning / alpha   The first / beta    The second
//
// Cause: Documentation.cs:712 renders every list item as "-"; XmlDocumentationRenderer.cs:177-187 drops
//        <listheader>, and <term> whenever a <description> exists.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug51
{
	/// <summary>Steps: <list type="number"><item><description>first</description></item><item><description>second</description></item></list></summary>
	/// <remarks>
	/// <list type="table">
	///   <listheader><term>Name</term><description>Meaning</description></listheader>
	///   <item><term>alpha</term><description>The first</description></item>
	///   <item><term>beta</term><description>The second</description></item>
	/// </list>
	/// </remarks>
	[NoOptionsInjection]
	public static void Run() => Console.Out.WriteLine("bug51");

	public static void Register(IArghRootBuilder app) => app.Map("bug51", Run);
}
