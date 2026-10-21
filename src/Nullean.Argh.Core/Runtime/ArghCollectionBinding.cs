using System.ComponentModel;

namespace Nullean.Argh.Runtime;

/// <summary>
/// Turns the values given for a collection into the elements generated code parses one at a time, each exactly as a single
/// value of the element type. Called from generated code; not intended for app code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ArghCollectionBinding
{
	/// <summary>
	/// The elements of a collection: each of <paramref name="values"/> (one per time the flag was given), or else
	/// <paramref name="environmentValue"/> when it's set, split on <paramref name="separator"/> when there is one. Empty parts
	/// are kept, as clap and pflag keep them, so <c>--ids=</c> is one empty element and <c>--ids 1,,3</c> three elements:
	/// the element type's parser rejects an empty one if it isn't valid (an <c>int</c>) and keeps it if it is (a <c>string</c>).
	/// </summary>
	public static List<string> Elements(List<string>? values, string? environmentValue, string? separator)
	{
		var elements = new List<string>();
		if (values is not null)
		{
			foreach (var value in values)
				Split(value, separator, elements);
		}
		else if (environmentValue is not null)
			Split(environmentValue, separator, elements);

		return elements;
	}

	private static void Split(string value, string? separator, List<string> elements)
	{
		if (string.IsNullOrEmpty(separator))
		{
			elements.Add(value);
			return;
		}

		var start = 0;
		int at;
		while ((at = value.IndexOf(separator!, start, StringComparison.Ordinal)) >= 0)
		{
			elements.Add(value.Substring(start, at - start));
			start = at + separator!.Length;
		}

		elements.Add(value.Substring(start));
	}
}
