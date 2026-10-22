using System.Globalization;

namespace Nullean.Argh;

/// <summary>
/// Parses human-readable durations such as <c>1h30m</c>, <c>1.5h</c>, <c>-2m</c>, <c>300ms</c> or <c>2days 3hours</c>
/// into a <see cref="TimeSpan"/>, accepting what Go's <c>time.ParseDuration</c> and Rust's humantime
/// <c>parse_duration</c> accept.
/// </summary>
/// <remarks>
/// <para>
/// A duration is an optional sign (<c>+</c> or <c>-</c>, which applies to the whole value) and one or more parts. A part
/// is a decimal number and a unit, such as <c>90m</c> or <c>1.5h</c>. Parts can be written together (<c>1h30m</c>) or
/// apart (<c>1h 30m</c>), whitespace may separate a number from its unit (<c>20 min</c>), and the parts add up in any
/// order. A number has digits before or after its decimal separator, or both (<c>1.5h</c>, <c>.5h</c>, <c>1.h</c>). The
/// separator is <c>.</c>, or the decimal separator of the format provider (<c>,</c> for <c>de-DE</c>). <c>0</c>,
/// <c>+0</c> and <c>-0</c> need no unit; any other number does, so <c>10</c> on its own isn't a duration.
/// </para>
/// <para>Units are case-sensitive, so <c>m</c> is minutes and <c>M</c> is months:</para>
/// <list type="table">
/// <listheader><term>Unit</term><description>Spellings</description></listheader>
/// <item><term>nanosecond</term><description><c>ns</c>, <c>nsec</c>, <c>nanos</c></description></item>
/// <item><term>microsecond</term><description><c>us</c>, <c>µs</c> (U+00B5), <c>μs</c> (U+03BC), <c>usec</c></description></item>
/// <item><term>millisecond</term><description><c>ms</c>, <c>msec</c>, <c>millis</c></description></item>
/// <item><term>second</term><description><c>s</c>, <c>sec</c>, <c>secs</c>, <c>second</c>, <c>seconds</c></description></item>
/// <item><term>minute</term><description><c>m</c>, <c>min</c>, <c>mins</c>, <c>minute</c>, <c>minutes</c></description></item>
/// <item><term>hour</term><description><c>h</c>, <c>hr</c>, <c>hrs</c>, <c>hour</c>, <c>hours</c></description></item>
/// <item><term>day (24 hours)</term><description><c>d</c>, <c>day</c>, <c>days</c></description></item>
/// <item><term>week (7 days)</term><description><c>w</c>, <c>wk</c>, <c>wks</c>, <c>week</c>, <c>weeks</c></description></item>
/// <item><term>month (30.44 days)</term><description><c>M</c>, <c>month</c>, <c>months</c></description></item>
/// <item><term>year (365.25 days)</term><description><c>y</c>, <c>yr</c>, <c>yrs</c>, <c>year</c>, <c>years</c></description></item>
/// </list>
/// <para>
/// The value is computed exactly and must be a whole number of ticks (100 ns), so <c>150ns</c> doesn't parse, as
/// humantime rejects what it can't hold in nanoseconds and <see cref="TimeSpan.Parse(string)"/> rejects an eighth
/// fractional digit. A value outside <see cref="TimeSpan.MinValue"/> to <see cref="TimeSpan.MaxValue"/> doesn't parse
/// either. Only ASCII digits are read, whatever the format provider.
/// </para>
/// <para>
/// See Go's <see href="https://github.com/golang/go/blob/go1.27.3/src/time/format.go#L1618-L1731">ParseDuration</see>
/// and humantime's <see href="https://github.com/chronotope/humantime/blob/v2.4.0/src/duration.rs#L112-L373">parse_duration</see>.
/// Go adds the sign and the <c>μs</c> spelling; humantime adds whitespace, the long unit names and days to years.
/// </para>
/// </remarks>
#if ARGH_GENERATOR
internal
#else
public
#endif
static class HumanTime
{
	// humantime's month and year in seconds (30.44 and 365.25 days).
	private const long SecondsPerMonth = 2_630_016;
	private const long SecondsPerYear = 31_557_600;

	/// <summary>Parses <paramref name="s"/> as a duration, with <c>.</c> as the decimal separator.</summary>
	/// <exception cref="ArgumentNullException"><paramref name="s"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException"><paramref name="s"/> isn't a duration; the message says why.</exception>
	/// <exception cref="OverflowException">The duration is outside the <see cref="TimeSpan"/> range or finer than a tick.</exception>
	public static TimeSpan Parse(string s) => Parse(s, CultureInfo.InvariantCulture);

	/// <summary>
	/// Parses <paramref name="s"/> as a duration. Besides <c>.</c>, the decimal separator of <paramref name="provider"/>
	/// (the current culture when <see langword="null"/>) separates a fraction.
	/// </summary>
	/// <exception cref="ArgumentNullException"><paramref name="s"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException"><paramref name="s"/> isn't a duration; the message says why.</exception>
	/// <exception cref="OverflowException">The duration is outside the <see cref="TimeSpan"/> range or finer than a tick.</exception>
	public static TimeSpan Parse(string s, IFormatProvider? provider)
	{
		if (s is null)
			throw new ArgumentNullException(nameof(s));
		return TryParseCore(s, provider, out var value, out var error, out var overflow)
			? value
			: throw (overflow ? new OverflowException(error) : new FormatException(error));
	}

	/// <summary>Parses <paramref name="s"/> as a duration, with <c>.</c> as the decimal separator.</summary>
	/// <returns><see langword="true"/> when <paramref name="s"/> is a duration that fits a <see cref="TimeSpan"/>.</returns>
	public static bool TryParse(string? s, out TimeSpan value) => TryParse(s, CultureInfo.InvariantCulture, out value);

	/// <summary>
	/// Parses <paramref name="s"/> as a duration. Besides <c>.</c>, the decimal separator of <paramref name="provider"/>
	/// (the current culture when <see langword="null"/>) separates a fraction.
	/// </summary>
	/// <returns><see langword="true"/> when <paramref name="s"/> is a duration that fits a <see cref="TimeSpan"/>.</returns>
	public static bool TryParse(string? s, IFormatProvider? provider, out TimeSpan value) =>
		TryParse(s, provider, out value, out _);

	/// <summary>
	/// Like <see cref="TryParse(string?, IFormatProvider?, out TimeSpan)"/>, and gives the reason a value doesn't parse
	/// in <paramref name="error"/>, a sentence such as <c>Expected a unit after 10, such as 10s or 10m.</c>
	/// </summary>
	public static bool TryParse(string? s, IFormatProvider? provider, out TimeSpan value, out string? error) =>
		TryParseCore(s, provider, out value, out error, out _);

	private static bool TryParseCore(string? s, IFormatProvider? provider, out TimeSpan value, out string? error, out bool overflow)
	{
		value = default;
		overflow = false;
		// Positions in messages count from the start of the value, leading whitespace included.
		var text = s?.TrimEnd() ?? "";
		var i = 0;
		while (i < text.Length && char.IsWhiteSpace(text[i]))
			i++;
		if (i == text.Length)
			return Fail("The duration is empty.", out error);

		var separator = NumberFormatInfo.GetInstance(provider).NumberDecimalSeparator;
		var negative = false;
		if (text[i] is '+' or '-')
		{
			negative = text[i] == '-';
			i++;
			if (i == text.Length)
				return Fail("Expected a number after the sign.", out error);
		}

		// Go's special case: a lone zero needs no unit.
		if (text.Length - i == 1 && text[i] == '0')
		{
			error = null;
			return true;
		}

		decimal totalTicks = 0;
		while (i < text.Length)
		{
			var numberStart = i;
			if (!TryReadNumber(text, ref i, separator, out var number, out error, out overflow))
				return false;
			var numberText = text.Substring(numberStart, i - numberStart);

			while (i < text.Length && char.IsWhiteSpace(text[i]))
				i++;
			var unitStart = i;
			while (i < text.Length && IsUnitChar(text[i]))
				i++;
			if (i == unitStart)
			{
				return i < text.Length && !IsNumberStart(text, i, separator)
					? Fail($"Unexpected '{text[i]}' at position {i + 1}.", out error)
					: Fail($"Expected a unit after {numberText}, such as {numberText}s or {numberText}m.", out error);
			}

			var unit = text.Substring(unitStart, i - unitStart);
			if (TicksPerUnit(unit) is not { } ticksPerUnit)
				return Fail($"Unknown unit '{unit}'. Use ns, us, ms, s, m, h, d, w, M (months) or y, or a longer name such as sec, min or hours.", out error);

			try
			{
				totalTicks += number * ticksPerUnit;
			}
			catch (OverflowException)
			{
				return Overflow(out error, out overflow);
			}

			if (totalTicks > (decimal)long.MaxValue + 1)
				return Overflow(out error, out overflow);

			while (i < text.Length && char.IsWhiteSpace(text[i]))
				i++;
		}

		if (decimal.Truncate(totalTicks) != totalTicks)
		{
			overflow = true;
			return Fail("The duration is finer than a TimeSpan can hold (100 ns).", out error);
		}

		if (negative ? totalTicks > (decimal)long.MaxValue + 1 : totalTicks > long.MaxValue)
			return Overflow(out error, out overflow);

		value = negative
			? new TimeSpan(totalTicks == (decimal)long.MaxValue + 1 ? long.MinValue : -(long)totalTicks)
			: new TimeSpan((long)totalTicks);
		error = null;
		return true;
	}

	/// <summary>
	/// Reads digits with an optional fraction at <paramref name="i"/>: <c>1</c>, <c>1.5</c>, <c>.5</c> or <c>1.</c>.
	/// Fraction digits beyond what a <see cref="decimal"/> holds are ignored, as Go's <c>leadingFraction</c> stops
	/// accumulating precision.
	/// </summary>
	private static bool TryReadNumber(string text, ref int i, string separator, out decimal number, out string? error, out bool overflow)
	{
		number = 0;
		overflow = false;
		var start = i;
		var digits = 0;
		while (i < text.Length && text[i] is >= '0' and <= '9')
		{
			// No unit is shorter than a nanosecond (0.01 ticks), so 1e22 of anything is beyond TimeSpan.MaxValue.
			if (number >= 1e22m)
				return Overflow(out error, out overflow);

			number = number * 10 + (text[i] - '0');
			digits++;
			i++;
		}

		if (SeparatorAt(text, i, separator) is var length and > 0)
		{
			i += length;
			var scale = 0.1m;
			while (i < text.Length && text[i] is >= '0' and <= '9')
			{
				if (scale != 0)
				{
					number += (text[i] - '0') * scale;
					scale /= 10;
				}

				digits++;
				i++;
			}
		}

		if (digits == 0)
		{
			error = i < text.Length
				? $"Expected a number at position {start + 1}, but found '{text[start]}'."
				: $"Expected a number at position {start + 1}.";
			return false;
		}

		error = null;
		return true;
	}

	/// <summary>The length of the decimal separator at <paramref name="i"/>: <c>.</c> or <paramref name="separator"/>; 0 when there's none.</summary>
	private static int SeparatorAt(string text, int i, string separator)
	{
		if (i >= text.Length)
			return 0;
		if (text[i] == '.')
			return 1;
		return separator.Length > 0 && string.CompareOrdinal(text, i, separator, 0, separator.Length) == 0 ? separator.Length : 0;
	}

	private static bool IsNumberStart(string text, int i, string separator) =>
		text[i] is >= '0' and <= '9' || SeparatorAt(text, i, separator) > 0;

	/// <summary>A character of a unit name: an ASCII letter, or the micro sign or Greek mu of <c>µs</c>.</summary>
	private static bool IsUnitChar(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or 'µ' or 'μ';

	/// <summary>How many ticks one <paramref name="unit"/> is, or <see langword="null"/> for an unknown unit.</summary>
	private static decimal? TicksPerUnit(string unit) =>
		unit switch
		{
			"ns" or "nsec" or "nanos" => 0.01m,
			"us" or "µs" or "μs" or "usec" => 10m,
			"ms" or "msec" or "millis" => TimeSpan.TicksPerMillisecond,
			"s" or "sec" or "secs" or "second" or "seconds" => TimeSpan.TicksPerSecond,
			"m" or "min" or "mins" or "minute" or "minutes" => TimeSpan.TicksPerMinute,
			"h" or "hr" or "hrs" or "hour" or "hours" => TimeSpan.TicksPerHour,
			"d" or "day" or "days" => TimeSpan.TicksPerDay,
			"w" or "wk" or "wks" or "week" or "weeks" => 7 * TimeSpan.TicksPerDay,
			"M" or "month" or "months" => SecondsPerMonth * TimeSpan.TicksPerSecond,
			"y" or "yr" or "yrs" or "year" or "years" => SecondsPerYear * TimeSpan.TicksPerSecond,
			_ => null,
		};

	private static bool Fail(string message, out string? error)
	{
		error = message;
		return false;
	}

	private static bool Overflow(out string? error, out bool overflow)
	{
		overflow = true;
		return Fail("The duration is outside the TimeSpan range.", out error);
	}
}
