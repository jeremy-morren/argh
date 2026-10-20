// Bug 42 - Multi-line <param> text breaks the option table
// Group D: help and XML docs
//
// Run:      Bugs.exe bug42 --help
// Actual:     -n, --name <string>  [required] The name to use.
//             This continues on a second line
//             and a third line.
//               --count <int>        Count of things.
//             Second para. [default: 1]
// Expected: one wrapped paragraph per option, as the summary already does   (clap joins doc-comment lines
//           into one paragraph unless verbatim_doc_comment is set)
//
// Cause: FlattenParam (Documentation.cs:659-670) and Emit.Help.cs:904-905 never collapse whitespace; the
//        summary does (:388).

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug42
{
	/// <summary>Options documented over several lines.</summary>
	/// <param name="name">-n, The name to use.
	/// This continues on a second line
	/// and a third line.</param>
	/// <param name="count">Count of things.
	///
	/// Second para.</param>
	[NoOptionsInjection]
	public static void Run(string name, int count = 1) => Console.Out.WriteLine($"{name} {count}");

	public static void Register(IArghRootBuilder app) => app.Map("bug42", Run);
}
