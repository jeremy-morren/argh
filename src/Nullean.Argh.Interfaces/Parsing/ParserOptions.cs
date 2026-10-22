using System.Globalization;

namespace Nullean.Argh.Parsing;

/// <summary>
/// How Argh parses argument and environment-variable values. Set it with <c>UseParserOptions</c> on the app or hosting
/// builder; custom <see cref="IArgumentParser{T}"/> implementations receive it too.
/// </summary>
public sealed class ParserOptions
{
	private CultureInfo _culture = CultureInfo.InvariantCulture;
	private StringComparer _commandStringComparer = StringComparer.OrdinalIgnoreCase;
	private StringComparer _argumentStringComparer = StringComparer.Ordinal;

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

	/// <summary>
	/// What happens when a flag that takes a single value (or a <c>bool</c> flag) is given more than once, such as
	/// <c>--name a --name b</c>. Defaults to <see cref="RepeatedScalarBehaviour.Reject"/>, an error, as in clap; a flag's
	/// <see cref="RepeatedScalarAttribute"/> wins over it. Collections keep every value whatever this is.
	/// </summary>
	public RepeatedScalarBehaviour RepeatedScalarBehaviour { get; set; } = RepeatedScalarBehaviour.Reject;

	/// <summary>
	/// How a command or namespace name on the command line matches a registered one, for running, routing, help, shell
	/// completion and "did you mean" suggestions. Defaults to <see cref="StringComparer.OrdinalIgnoreCase"/>, so
	/// <c>DEPLOY</c> runs <c>deploy</c>; set <see cref="StringComparer.Ordinal"/> to match case, as clap and cobra do.
	/// Setting <see langword="null"/> restores the default. Two names in one scope that it treats as the same fail at
	/// startup with an <see cref="InvalidOperationException"/>.
	/// </summary>
	public StringComparer CommandStringComparer
	{
		get => _commandStringComparer;
		set => _commandStringComparer = value ?? StringComparer.OrdinalIgnoreCase;
	}

	/// <summary>
	/// How a long option on the command line, such as <c>--name</c> or <c>--no-color</c>, matches a flag's name or alias,
	/// including <c>--help</c> and <c>--version</c>, for parsing, shell completion and "did you mean" suggestions.
	/// Defaults to <see cref="StringComparer.Ordinal"/>, as in clap and cobra, so <c>--NAME</c> is an unknown option.
	/// Short options such as <c>-v</c> always match case, so <c>-v</c> and <c>-V</c> stay different flags. Setting
	/// <see langword="null"/> restores the default. Two flags of one command that it treats as the same fail when the
	/// app starts with an <see cref="InvalidOperationException"/>.
	/// </summary>
	public StringComparer ArgumentStringComparer
	{
		get => _argumentStringComparer;
		set => _argumentStringComparer = value ?? StringComparer.Ordinal;
	}
}
