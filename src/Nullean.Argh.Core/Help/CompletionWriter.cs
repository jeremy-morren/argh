using Nullean.Argh.Runtime;

namespace Nullean.Argh.Help;

/// <summary>
/// Writes shell completion candidates to <see cref="Console.Out"/>, one per line. Lines end in <c>LF</c> on every OS:
/// bash's <c>read</c> and zsh's line splitting would keep the <c>CR</c> of a Windows <c>CRLF</c> in each candidate.
/// </summary>
public static class CompletionWriter
{
	/// <summary>
	/// Writes every candidate that matches <paramref name="prefix"/> (case-insensitive prefix filter, as enum values are
	/// parsed; empty prefix matches all). Candidates are ordered ordinally for stable output.
	/// </summary>
	public static void WriteFiltered(ReadOnlySpan<string> candidates, string prefix) =>
		WriteFiltered(candidates, prefix, "");

	/// <summary>
	/// Like <see cref="WriteFiltered(ReadOnlySpan{string}, string)"/>, but writes each match prepended with <paramref name="insertPrefix"/>
	/// (for example <c>--flag=</c> when completing a value typed in the same word as its flag).
	/// </summary>
	public static void WriteFiltered(ReadOnlySpan<string> candidates, string prefix, string insertPrefix) =>
		Write(candidates, prefix, insertPrefix, static (c, p) => c.StartsWith(p, StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Writes every command and namespace name in <paramref name="candidates"/> that starts with <paramref name="prefix"/>
	/// under <see cref="Parsing.ParserOptions.CommandStringComparer"/>, as they are matched.
	/// </summary>
	public static void WriteCommands(ReadOnlySpan<string> candidates, string prefix)
	{
		var comparer = ArghNames.CommandComparer;
		Write(candidates, prefix, "", (c, p) => ArghNames.StartsWith(c, p, comparer));
	}

	/// <summary>
	/// Writes every option in <paramref name="candidates"/> that starts with <paramref name="prefix"/>: a long option under
	/// <see cref="Parsing.ParserOptions.ArgumentStringComparer"/>, a short one matching case, as they are matched.
	/// </summary>
	public static void WriteFlags(ReadOnlySpan<string> candidates, string prefix)
	{
		var comparer = ArghNames.ArgumentComparer;
		Write(candidates, prefix, "", (c, p) => ArghNames.IsShortSpelling(c)
			? c.StartsWith(p, StringComparison.Ordinal)
			: ArghNames.StartsWith(c, p, comparer));
	}

	private static void Write(ReadOnlySpan<string> candidates, string prefix, string insertPrefix, Func<string, string, bool> matches)
	{
		if (candidates.Length == 0)
			return;
		var arr = new string[candidates.Length];
		candidates.CopyTo(arr);
		Array.Sort(arr, StringComparer.Ordinal);
		foreach (var c in arr)
		{
			if (prefix.Length == 0 || matches(c, prefix))
			{
				Console.Out.Write(insertPrefix + c);
				Console.Out.Write('\n');
			}
		}
	}
}
