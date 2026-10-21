namespace Nullean.Argh.Help;

/// <summary>
/// Writes shell completion candidates to <see cref="Console.Out"/>, one per line. Lines end in <c>LF</c> on every OS:
/// bash's <c>read</c> and zsh's line splitting would keep the <c>CR</c> of a Windows <c>CRLF</c> in each candidate.
/// </summary>
public static class CompletionWriter
{
	/// <summary>
	/// Writes every candidate that matches <paramref name="prefix"/> (case-insensitive prefix filter, like command routing; empty prefix matches all).
	/// Candidates are ordered ordinally for stable output.
	/// </summary>
	public static void WriteFiltered(ReadOnlySpan<string> candidates, string prefix) =>
		WriteFiltered(candidates, prefix, "");

	/// <summary>
	/// Like <see cref="WriteFiltered(ReadOnlySpan{string}, string)"/>, but writes each match prepended with <paramref name="insertPrefix"/>
	/// (for example <c>--flag=</c> when completing a value typed in the same word as its flag).
	/// </summary>
	public static void WriteFiltered(ReadOnlySpan<string> candidates, string prefix, string insertPrefix)
	{
		if (candidates.Length == 0)
			return;
		var arr = new string[candidates.Length];
		candidates.CopyTo(arr);
		Array.Sort(arr, StringComparer.Ordinal);
		foreach (var c in arr)
		{
			if (prefix.Length == 0 || c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				Console.Out.Write(insertPrefix + c);
				Console.Out.Write('\n');
			}
		}
	}
}
