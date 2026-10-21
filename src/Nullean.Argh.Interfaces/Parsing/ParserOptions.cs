using System.Globalization;

namespace Nullean.Argh.Parsing;

/// <summary>
/// How Argh parses argument and environment-variable values. Set it with <c>UseParserOptions</c> on the app or hosting
/// builder; custom <see cref="IArgumentParser{T}"/> implementations receive it too.
/// </summary>
public sealed class ParserOptions
{
	private CultureInfo _culture = CultureInfo.InvariantCulture;

	/// <summary>
	/// The culture for numbers, dates and times, and <c>TimeSpan</c> values. Defaults to <see cref="CultureInfo.InvariantCulture"/>;
	/// setting <see langword="null"/> restores it.
	/// </summary>
	public CultureInfo Culture
	{
		get => _culture;
		set => _culture = value ?? CultureInfo.InvariantCulture;
	}

	/// <summary>
	/// The <see cref="System.Globalization.NumberStyles"/> for every number type. Defaults to <c>Float</c>: a leading sign, a
	/// decimal point, an exponent, and leading and trailing whitespace, but no group separators, so <c>1.5</c>, <c>-2</c> and
	/// <c>1e3</c> parse and <c>1,000</c> doesn't. Integers take a decimal point or an exponent only for a whole number:
	/// <c>1.0</c> and <c>1e3</c> parse, <c>1.5</c> doesn't. Add <c>AllowThousands</c> to accept group separators.
	/// </summary>
	public NumberStyles NumberStyles { get; set; } = NumberStyles.Float;

	/// <summary>
	/// The <see cref="System.Globalization.DateTimeStyles"/> for <c>DateTime</c>, <c>DateTimeOffset</c>, <c>DateOnly</c> and
	/// <c>TimeOnly</c>. Defaults to <c>RoundtripKind | AllowWhiteSpaces</c>, so a value with <c>Z</c> or an offset keeps its kind.
	/// <c>DateOnly</c> and <c>TimeOnly</c> accept only the whitespace flags, so they get just those. <c>TimeSpan</c> doesn't use it.
	/// </summary>
	public DateTimeStyles DateTimeStyles { get; set; } = DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces;
}
