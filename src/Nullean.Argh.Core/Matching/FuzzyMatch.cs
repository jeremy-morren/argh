using System.ComponentModel;

namespace Nullean.Argh.Matching;

/// <summary>
/// Edit-distance helpers for suggesting close matches (e.g. unknown command names). Uses the Levenshtein metric; no external dependencies.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class FuzzyMatch
{
	/// <summary>
	/// Computes the Levenshtein (edit) distance between <paramref name="a"/> and <paramref name="b"/>.
	/// </summary>
	/// <param name="a">First string.</param>
	/// <param name="b">Second string.</param>
	/// <returns>Minimum number of single-character insertions, deletions, or substitutions.</returns>
	public static int LevenshteinDistance(string a, string b) => LevenshteinDistance(a, b, StringComparer.Ordinal);

	/// <summary>
	/// Computes the Levenshtein (edit) distance between <paramref name="a"/> and <paramref name="b"/>, with two characters
	/// the same when <paramref name="comparer"/> says so, such as <c>a</c> and <c>A</c> under <see cref="StringComparer.OrdinalIgnoreCase"/>.
	/// </summary>
	/// <param name="a">First string.</param>
	/// <param name="b">Second string.</param>
	/// <param name="comparer">Compares single characters.</param>
	/// <returns>Minimum number of single-character insertions, deletions, or substitutions.</returns>
	public static int LevenshteinDistance(string a, string b, StringComparer comparer)
	{
		if (comparer is null)
			throw new ArgumentNullException(nameof(comparer));
		if (a is null)
			throw new ArgumentNullException(nameof(a));
		if (b is null)
			throw new ArgumentNullException(nameof(b));

		if (a.Length == 0)
			return b.Length;
		if (b.Length == 0)
			return a.Length;

		var same = CharComparison(comparer);
		var previous = new int[b.Length + 1];
		var current = new int[b.Length + 1];
		for (var j = 0; j <= b.Length; j++)
			previous[j] = j;

		for (var i = 1; i <= a.Length; i++)
		{
			current[0] = i;
			for (var j = 1; j <= b.Length; j++)
			{
				var cost = same(a[i - 1], b[j - 1]) ? 0 : 1;
				var del = previous[j] + 1;
				var ins = current[j - 1] + 1;
				var sub = previous[j - 1] + cost;
				current[j] = del < ins ? (del < sub ? del : sub) : (ins < sub ? ins : sub);
			}

			(previous, current) = (current, previous);
		}

		return previous[b.Length];
	}

	/// <summary>
	/// Returns every candidate whose Levenshtein distance to <paramref name="input"/> equals the smallest distance among those at most <paramref name="maxDistance"/>.
	/// </summary>
	/// <param name="input">User input to compare.</param>
	/// <param name="candidates">Possible matches; null entries are ignored.</param>
	/// <param name="maxDistance">Maximum edit distance to consider (inclusive).</param>
	/// <returns>All ties at the minimum distance within the cap, or an empty list when none qualify.</returns>
	public static IReadOnlyList<string> FindClosest(string input, IEnumerable<string> candidates, int maxDistance) =>
		FindClosest(input, candidates, maxDistance, StringComparer.Ordinal);

	/// <summary>
	/// Returns every candidate whose Levenshtein distance to <paramref name="input"/>, comparing characters with
	/// <paramref name="comparer"/>, equals the smallest distance among those at most <paramref name="maxDistance"/>.
	/// </summary>
	/// <param name="input">User input to compare.</param>
	/// <param name="candidates">Possible matches; null entries are ignored.</param>
	/// <param name="maxDistance">Maximum edit distance to consider (inclusive).</param>
	/// <param name="comparer">Compares single characters, as names are matched.</param>
	/// <returns>All ties at the minimum distance within the cap, or an empty list when none qualify.</returns>
	public static IReadOnlyList<string> FindClosest(string input, IEnumerable<string> candidates, int maxDistance, StringComparer comparer)
	{
		if (comparer is null)
			throw new ArgumentNullException(nameof(comparer));
		if (input is null)
			throw new ArgumentNullException(nameof(input));
		if (candidates is null)
			throw new ArgumentNullException(nameof(candidates));
		if (maxDistance < 0)
			throw new ArgumentOutOfRangeException(nameof(maxDistance));

		var best = new List<string>();
		var bestDistance = int.MaxValue;

		foreach (var candidate in candidates)
		{
			if (candidate is null)
				continue;

			var d = LevenshteinDistance(input, candidate, comparer);
			if (d > maxDistance)
				continue;

			if (d < bestDistance)
			{
				bestDistance = d;
				best.Clear();
				best.Add(candidate);
			}
			else if (d == bestDistance)
				best.Add(candidate);
		}

		return best;
	}

	private static Func<char, char, bool> CharComparison(StringComparer comparer)
	{
		if (ReferenceEquals(comparer, StringComparer.Ordinal))
			return static (x, y) => x == y;
		if (ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase))
			return static (x, y) => x == y || char.ToUpperInvariant(x) == char.ToUpperInvariant(y);
		return (x, y) => x == y || comparer.Equals(x.ToString(), y.ToString());
	}
}
