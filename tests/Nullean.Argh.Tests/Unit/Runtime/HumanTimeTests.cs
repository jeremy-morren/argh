using System.Globalization;
using FluentAssertions;
using Nullean.Argh;
using Xunit;

namespace Nullean.Argh.Tests.Unit.Runtime;

/// <summary>
/// <see cref="HumanTime"/> accepts what Go's <c>time.ParseDuration</c> and humantime's <c>parse_duration</c> accept; many
/// cases come from their tests and docs.
/// </summary>
public class HumanTimeTests
{
	private const long Second = TimeSpan.TicksPerSecond;
	private const long Minute = TimeSpan.TicksPerMinute;
	private const long Hour = TimeSpan.TicksPerHour;
	private const long Day = TimeSpan.TicksPerDay;

	public static TheoryData<string, long> Valid => new()
	{
		// Zero needs no unit.
		{ "0", 0 },
		{ "+0", 0 },
		{ "-0", 0 },
		{ "0s", 0 },
		{ "0.0s", 0 },
		// Go's examples.
		{ "300ms", 300 * TimeSpan.TicksPerMillisecond },
		{ "-1.5h", -90 * Minute },
		{ "2h45m", 165 * Minute },
		{ "1h30m", 90 * Minute },
		{ "1.5h", 90 * Minute },
		{ "-2m", -2 * Minute },
		{ "+5s", 5 * Second },
		{ "2h45m30.5s", 165 * Minute + 30 * Second + Second / 2 },
		{ ".5h", 30 * Minute },
		{ "1.h", Hour },
		{ "1.0000001s", Second + 1 },
		// humantime's examples.
		{ "2h 37min", 9420 * Second },
		{ "32ms", 32 * TimeSpan.TicksPerMillisecond },
		{ "4.2s", 42 * Second / 10 },
		{ "2h 15m", 8100 * Second },
		{ "20 min 17 usec", 1200 * Second + 170 },
		{ "1.234s 0.5us 100ns", 12_340_006 },
		{ "2days 3hours", 2 * Day + 3 * Hour },
		{ "1.5d", 36 * Hour },
		{ "12.1min", 726 * Second },
		{ "2.5h", 9000 * Second },
		// Whitespace around and between parts, and parts in any order, added up.
		{ "  5m  ", 5 * Minute },
		{ "30m1h", 90 * Minute },
		{ "1h1h", 2 * Hour },
		// Every spelling of every unit.
		{ "100ns", 1 }, { "100nsec", 1 }, { "100nanos", 1 },
		{ "1us", 10 }, { "1µs", 10 }, { "1μs", 10 }, { "1usec", 10 },
		{ "1ms", 10_000 }, { "1msec", 10_000 }, { "1millis", 10_000 },
		{ "1s", Second }, { "1sec", Second }, { "1secs", Second }, { "1second", Second }, { "1seconds", Second },
		{ "1m", Minute }, { "1min", Minute }, { "1mins", Minute }, { "1minute", Minute }, { "1minutes", Minute },
		{ "1h", Hour }, { "1hr", Hour }, { "1hrs", Hour }, { "1hour", Hour }, { "1hours", Hour },
		{ "1d", Day }, { "1day", Day }, { "1days", Day },
		{ "1w", 7 * Day }, { "1wk", 7 * Day }, { "1wks", 7 * Day }, { "1week", 7 * Day }, { "1weeks", 7 * Day },
		// A month is 30.44 days and a year 365.25 days, as in humantime.
		{ "1M", 2_630_016 * Second }, { "1month", 2_630_016 * Second }, { "1months", 2_630_016 * Second },
		{ "12M", 31_560_192 * Second },
		{ "1y", 31_557_600 * Second }, { "1yr", 31_557_600 * Second }, { "1yrs", 31_557_600 * Second },
		{ "1year", 31_557_600 * Second }, { "1years", 31_557_600 * Second },
		{ "17y", 536_479_200 * Second },
		// The TimeSpan limits are exact.
		{ "922337203685477580700ns", long.MaxValue },
		{ "-922337203685477580800ns", long.MinValue },
		{ "10675199d", 10_675_199 * Day },
		{ "29227y", 29_227L * 31_557_600 * Second },
	};

	[Theory]
	[MemberData(nameof(Valid))]
	public void Parses_a_duration(string text, long ticks)
	{
		HumanTime.TryParse(text, out var value).Should().BeTrue();
		value.Ticks.Should().Be(ticks);
		HumanTime.Parse(text).Ticks.Should().Be(ticks);
	}

	[Theory]
	[InlineData("", "The duration is empty.")]
	[InlineData("   ", "The duration is empty.")]
	[InlineData("10", "Expected a unit after 10, such as 10s or 10m.")]
	[InlineData("1.5", "Expected a unit after 1.5, such as 1.5s or 1.5m.")]
	[InlineData("00", "Expected a unit after 00, such as 00s or 00m.")]
	[InlineData("1h30", "Expected a unit after 30, such as 30s or 30m.")]
	[InlineData("10 20s", "Expected a unit after 10, such as 10s or 10m.")]
	[InlineData("-", "Expected a number after the sign.")]
	[InlineData("+-5s", "Expected a number at position 2, but found '-'.")]
	[InlineData("1h -30m", "Expected a number at position 4, but found '-'.")]
	[InlineData("h", "Expected a number at position 1, but found 'h'.")]
	[InlineData(".", "Expected a number at position 1.")]
	[InlineData(".s", "Expected a number at position 1, but found '.'.")]
	[InlineData("5 m s", "Expected a number at position 5, but found 's'.")]
	[InlineData("PT10S", "Expected a number at position 1, but found 'P'.")]
	[InlineData("1~", "Unexpected '~' at position 2.")]
	[InlineData("1:30", "Unexpected ':' at position 2.")]
	[InlineData("1,5h", "Unexpected ',' at position 2.")]
	[InlineData("5x", "Unknown unit 'x'.")]
	[InlineData("10nights", "Unknown unit 'nights'.")]
	[InlineData("5H", "Unknown unit 'H'.")]
	[InlineData("5S", "Unknown unit 'S'.")]
	[InlineData("5D", "Unknown unit 'D'.")]
	[InlineData("1e3s", "Unknown unit 'e'.")]
	public void Rejects_what_isnt_a_duration_with_a_format_error(string text, string reason)
	{
		HumanTime.TryParse(text, CultureInfo.InvariantCulture, out var value, out var error).Should().BeFalse();
		value.Should().Be(TimeSpan.Zero);
		error.Should().StartWith(reason);

		var parse = () => HumanTime.Parse(text);
		parse.Should().Throw<FormatException>().Which.Message.Should().StartWith(reason);
	}

	[Theory]
	[InlineData("922337203685477580800ns")]
	[InlineData("-922337203685477580900ns")]
	[InlineData("10675200d")]
	[InlineData("29228y")]
	[InlineData("10675199d 1d")]
	[InlineData("99999999999999999999999h")]
	[InlineData("10000000000000000000000ns")]
	[InlineData("1000000000000000000000000000000000000y")]
	public void Rejects_a_duration_outside_the_TimeSpan_range_with_an_overflow_error(string text)
	{
		HumanTime.TryParse(text, CultureInfo.InvariantCulture, out _, out var error).Should().BeFalse();
		error.Should().Be("The duration is outside the TimeSpan range.");

		var parse = () => HumanTime.Parse(text);
		parse.Should().Throw<OverflowException>();
	}

	[Theory]
	[InlineData("1ns")]
	[InlineData("150ns")]
	[InlineData("0.5us 1ns")]
	[InlineData("0.00000001s")]
	[InlineData("1.234s 789ns")]
	public void Rejects_a_duration_finer_than_a_tick(string text)
	{
		HumanTime.TryParse(text, CultureInfo.InvariantCulture, out _, out var error).Should().BeFalse();
		error.Should().Be("The duration is finer than a TimeSpan can hold (100 ns).");

		var parse = () => HumanTime.Parse(text);
		parse.Should().Throw<OverflowException>();
	}

	[Theory]
	[InlineData("de-DE", "1,5h")]
	[InlineData("de-DE", "1.5h")]
	[InlineData("fr-FR", "1,5h")]
	[InlineData("fr-FR", "1 h 30 min")]
	[InlineData("en-US", "1.5h")]
	public void Reads_the_decimal_separator_of_the_culture_as_well_as_a_dot(string culture, string text)
	{
		HumanTime.TryParse(text, CultureInfo.GetCultureInfo(culture), out var value).Should().BeTrue();
		value.Should().Be(TimeSpan.FromMinutes(90));
		HumanTime.Parse(text, CultureInfo.GetCultureInfo(culture)).Should().Be(TimeSpan.FromMinutes(90));
	}

	[Fact]
	public void A_null_value_does_not_parse()
	{
		HumanTime.TryParse(null, out _).Should().BeFalse();
		var parse = () => HumanTime.Parse(null!);
		parse.Should().Throw<ArgumentNullException>();
	}
}
