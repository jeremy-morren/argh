using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Nullean.Argh;
using Point = Nullean.Argh.Tests.Fixtures.CliTestHandlers.Point;
using PointParser = Nullean.Argh.Tests.Fixtures.CliTestHandlers.PointParser;

namespace Nullean.Argh.Tests.Fixtures;

/// <summary>
/// The same flags as single values (<c>one</c>), as collections of <c>T</c> (<c>many</c>) and of <c>T?</c> (<c>many-maybe</c>):
/// each element must parse exactly as the single value does.
/// </summary>
internal static class CollectionElementHandlers
{
	/// <summary>Single values of each kind.</summary>
	/// <param name="n">An int.</param>
	/// <param name="amount">A decimal.</param>
	/// <param name="bit">A bool.</param>
	/// <param name="color">An enum.</param>
	/// <param name="id">A Guid.</param>
	/// <param name="day">A date.</param>
	/// <param name="wait">A time span.</param>
	/// <param name="link">A URI.</param>
	/// <param name="file">A file.</param>
	/// <param name="at">A point, with a custom parser.</param>
	/// <param name="name">A string.</param>
	[NoOptionsInjection]
	public static void One(int? n = null, decimal? amount = null, bool? bit = null, TestColor? color = null, Guid? id = null,
		DateOnly? day = null, TimeSpan? wait = null, Uri? link = null, FileInfo? file = null,
		[ArgumentParser(typeof(PointParser))] Point? at = null, string? name = null) =>
		Print(("n", n), ("amount", amount), ("bit", bit), ("color", color), ("id", id), ("day", day), ("wait", wait), ("link", link),
			("file", file), ("at", at), ("name", name));

	/// <summary>Collections of each kind.</summary>
	/// <param name="n">Ints.</param>
	/// <param name="amount">Decimals.</param>
	/// <param name="bit">Bools.</param>
	/// <param name="color">Enums.</param>
	/// <param name="id">Guids.</param>
	/// <param name="day">Dates.</param>
	/// <param name="wait">Time spans.</param>
	/// <param name="link">URIs.</param>
	/// <param name="file">Files.</param>
	/// <param name="at">Points, each with the custom parser.</param>
	/// <param name="name">Strings.</param>
	[NoOptionsInjection]
	public static void Many(int[]? n = null, List<decimal>? amount = null, bool[]? bit = null, IReadOnlySet<TestColor>? color = null,
		IReadOnlyList<Guid>? id = null, DateOnly[]? day = null, IEnumerable<TimeSpan>? wait = null, Uri[]? link = null,
		List<FileInfo>? file = null, [ArgumentParser(typeof(PointParser))] Point[]? at = null, string[]? name = null) =>
		Print(("n", n), ("amount", amount), ("bit", bit), ("color", color), ("id", id), ("day", day), ("wait", wait), ("link", link),
			("file", file), ("at", at), ("name", name));

	/// <summary>Collections of nullable values of each kind.</summary>
	/// <param name="n">Ints.</param>
	/// <param name="amount">Decimals.</param>
	/// <param name="bit">Bools.</param>
	/// <param name="color">Enums.</param>
	/// <param name="id">Guids.</param>
	/// <param name="day">Dates.</param>
	/// <param name="wait">Time spans.</param>
	/// <param name="link">URIs.</param>
	/// <param name="file">Files.</param>
	/// <param name="at">Points, each with the custom parser for <c>Point</c>.</param>
	/// <param name="name">Strings.</param>
	[NoOptionsInjection]
	public static void ManyMaybe(int?[]? n = null, List<decimal?>? amount = null, bool?[]? bit = null, TestColor?[]? color = null,
		IReadOnlySet<Guid?>? id = null, DateOnly?[]? day = null, List<TimeSpan?>? wait = null, Uri?[]? link = null,
		FileInfo?[]? file = null, [ArgumentParser(typeof(PointParser))] Point?[]? at = null, List<string?>? name = null) =>
		Print(("n", n), ("amount", amount), ("bit", bit), ("color", color), ("id", id), ("day", day), ("wait", wait), ("link", link),
			("file", file), ("at", at), ("name", name));

	/// <summary>Comma-separated values, one from an environment variable.</summary>
	/// <param name="ids">Comma-separated ints, or ARGH_TEST_IDS.</param>
	/// <param name="names">Semicolon-separated strings.</param>
	/// <param name="tags">Tags, or ARGH_TEST_TAGS as one tag.</param>
	[NoOptionsInjection]
	public static void Separated([Env("ARGH_TEST_IDS")][CollectionSyntax(Separator = ",")] List<int>? ids = null,
		[CollectionSyntax(Separator = ";")] string[]? names = null, [Env("ARGH_TEST_TAGS")] List<string>? tags = null) =>
		Print(("ids", ids), ("names", names), ("tags", tags));

	/// <summary>Ints as a variadic positional.</summary>
	/// <param name="values">The ints.</param>
	[NoOptionsInjection]
	public static void Variadic([Argument] params int[] values) => Print(("values", values));

	/// <summary>Constraints that apply to each element.</summary>
	/// <param name="small">Ints from 1 to 10, at most three of them.</param>
	/// <param name="letter">Each a or b.</param>
	/// <param name="site">Each an http or https URL.</param>
	/// <param name="secure">Each an https URI.</param>
	/// <param name="code">Each two lowercase letters.</param>
	[NoOptionsInjection]
	public static void Validated([Range(1, 10)][MaxLength(3)] int[]? small = null, [AllowedValues("a", "b")] List<string>? letter = null,
		[Url] Uri[]? site = null, [UriScheme("https")] List<Uri>? secure = null, [RegularExpression("^[a-z]{2}$")] string[]? code = null) =>
		Print(("small", small), ("letter", letter), ("site", site), ("secure", secure), ("code", code));

	/// <summary>Prints each value that was given, a collection as its elements joined with <c>;</c>.</summary>
	private static void Print(params (string Name, object? Value)[] values)
	{
		var parts = new List<string>();
		foreach (var (name, value) in values)
		{
			if (value is null)
				continue;
			parts.Add(name + "=" + (value is System.Collections.IEnumerable items and not string
				? string.Join(";", items.Cast<object?>().Select(Show))
				: Show(value)));
		}

		Console.Out.WriteLine(string.Join(" ", parts));
	}

	private static string Show(object? value) => value switch
	{
		null => "null",
		FileInfo f => f.Name,
		IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
		_ => value.ToString() ?? "",
	};
}
