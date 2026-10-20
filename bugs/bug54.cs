// Bug 54 - <inheritdoc/> produces no docs and loses the inherited short options
// Group D: help and XML docs
//
// Run:      Bugs.exe bug54 --help
// Actual:   no summary; --name and --count have no descriptions; the -n and -c short options from the
//           interface's <param> tags are gone. Bugs.exe --help lists bug54 with no summary. No diagnostic.
// Expected: the interface's docs (summary, descriptions, -n, -c), as the IDE shows them;
//           plans/vision.md lists <inheritdoc> as supported
//
// Also fails for a same-assembly base override, a cross-assembly base override, and <inheritdoc cref="..."/>.
// Cause: GetDocumentationCommentXml() returns the literal <inheritdoc/> unexpanded, and the trivia fallback
//        (Documentation.cs:218-238) parses the same text; nothing resolves the referenced member.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

/// <summary>Contract whose docs the command should inherit.</summary>
public interface IBug54
{
	/// <summary>Greets someone a number of times.</summary>
	/// <param name="name">-n, Who to greet.</param>
	/// <param name="count">-c, How many times.</param>
	void Bug54(string name, int count = 1);
}

internal sealed class Bug54Commands : IBug54
{
	/// <inheritdoc/>
	[NoOptionsInjection]
	public void Bug54(string name, int count = 1) => Console.Out.WriteLine($"{name} x{count}");
}

internal static class Bug54
{
	public static void Register(IArghRootBuilder app) => app.Map<Bug54Commands>();
}
