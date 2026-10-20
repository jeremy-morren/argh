// Bug 52 - Trailing spaces in Usage: and listing rows
// Group D: help and XML docs
//
// Run:      NO_COLOR=1 Bugs.exe bug52 --help | cat -A
// Actual:   Usage: Bugs bug52 $        (trailing space when there are no usage hints)
// Run:      NO_COLOR=1 Bugs.exe --help | grep bug52 | cat -A
// Actual:   bug52-nodoc            $   (listing row padded although there is no summary)
// Expected: no trailing whitespace
//
// Cause: Emit.Help.cs:719 (usage), CliHelpFormatting.cs:146 (listing rows), and option rows with an
//        empty description.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug52
{
	/// <summary>Parameterless command.</summary>
	[NoOptionsInjection]
	public static void Run() => Console.Out.WriteLine("bug52");

	[NoOptionsInjection]
	public static void NoDoc() => Console.Out.WriteLine("bug52-nodoc");

	public static void Register(IArghRootBuilder app)
	{
		app.Map("bug52", Run);
		app.Map("bug52-nodoc", NoDoc);
	}
}
