using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.Binding;

/// <summary>
/// Values parse with <c>ArghRuntime.ParserOptions</c>: invariant by default, or the culture and number styles the CLI host
/// sets from <c>ARGH_TEST_CULTURE</c>, <c>ARGH_TEST_NUMBER_STYLES</c> and <c>ARGH_TEST_DATE_STYLES</c>. <c>culture-cmd</c> prints every value in invariant
/// form, separated by <c>|</c>: amount, when, day, wait, at, amounts, env amount, tag, count, scale, ratio.
/// The CLI host runs on .NET 10, which uses ICU culture data on every OS, so these pin ICU behaviour;
/// <c>NetstandardFixtureTests</c> covers .NET Framework's NLS data.
/// </summary>
public class CultureBindingTests
{
	private const char NarrowNoBreakSpace = (char)0x202F;
	private const char NoBreakSpace = (char)0x00A0;
	private const char ArabicDecimalSeparator = (char)0x066B;
	private const char MinusSign = (char)0x2212;

	private static Dictionary<string, string> Culture(string name) => new() { ["ARGH_TEST_CULTURE"] = name };

	/// <summary>Adds <c>AllowThousands</c> to the default <c>Float</c> number styles.</summary>
	private static Dictionary<string, string> WithThousands(Dictionary<string, string>? environment = null)
	{
		environment ??= [];
		environment["ARGH_TEST_NUMBER_STYLES"] = "Float, AllowThousands";
		return environment;
	}

	private static string[] Fields(IReadOnlyDictionary<string, string>? environment, params string[] args) =>
		Output(environment, ["culture-cmd", .. args]).Split('|');

	private static string Output(IReadOnlyDictionary<string, string>? environment, params string[] args)
	{
		var result = CliHostRunner.Run(environment, args);
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		return CliHostRunner.StdoutText(result).Trim();
	}

	private static string Error(IReadOnlyDictionary<string, string>? environment, params string[] args)
	{
		var result = CliHostRunner.Run(environment, args);
		result.ExitCode.Should().Be(2);
		return CliHostRunner.StderrText(result);
	}

	// ── default and de-DE ──────────────────────────────────────────────────

	[Fact]
	public void Default_culture_is_invariant()
	{
		Output(null, "culture-cmd", "--amount", "1.5", "--when", "2024-06-15 14:30", "--wait", "00:00:01.5", "--tag", "x")
			.Should().Be("1.5|2024-06-15 14:30|none|1.5|none|none|none|x@invariant|none|none|1.5");
	}

	[Fact]
	public void Default_culture_rejects_a_german_date() =>
		Error(null, "culture-cmd", "--amount", "1", "--when", "15.06.2024").Should().Contain("invalid DateTime for --when: '15.06.2024'");

	[Fact]
	public void German_culture_parses_numbers_dates_and_times_its_way()
	{
		Output(Culture("de-DE"), "culture-cmd",
				"--amount", "1,5", "--when", "15.06.2024 14:30", "--day", "15.06.2024", "--wait", "00:00:01,5",
				"--at", "15.06.2024 14:30:00 +02:00", "--amounts", "1,5", "--amounts", "2,25", "--tag", "x")
			.Should().Be("1.5|2024-06-15 14:30|2024-06-15|1.5|2024-06-15T14:30:00.0000000+02:00|1.5,2.25|none|x@de-DE|none|none|1.5");
	}

	[Fact]
	public void German_culture_reads_a_dot_as_a_group_separator_only_when_they_are_allowed()
	{
		Error(Culture("de-DE"), "culture-cmd", "--amount", "1.5").Should().Contain("invalid decimal for --amount: '1.5'");
		Fields(WithThousands(Culture("de-DE")), "--amount", "1.5")[0].Should().Be("15");
	}

	[Fact]
	public void German_culture_still_accepts_iso_dates_and_compact_time_spans()
	{
		Output(Culture("de-DE"), "culture-cmd", "--amount", "1", "--day", "2024-06-15", "--wait", "5m")
			.Should().Be("1|none|2024-06-15|300|none|none|none|none|none|none|1.5");
	}

	[Fact]
	public void German_culture_parses_environment_variable_values()
	{
		var environment = new Dictionary<string, string>(Culture("de-DE")) { ["ARGH_TEST_AMOUNT"] = "2,5" };
		Fields(environment, "--amount", "1")[6].Should().Be("2.5");
	}

	[Fact]
	public void German_culture_parses_namespace_options()
	{
		var output = Output(Culture("de-DE"),
			"scalars-ns", "--span", "5m", "--stamp", "15.06.2024 14:30:00", "--offset", "2024-06-15T14:30:00+10:00",
			"--day", "15.06.2024", "--clock", "14:30", "--link", "https://example.com/a", "--doc", "notes.txt", "--folder", "out",
			"show");
		output.Should().Contain("day=2024-06-15").And.Contain("stamp=2024-06-15T14:30:00.0000000");
	}

	[Theory]
	[InlineData("", "[default: 1.5]")]
	[InlineData("de-DE", "[default: 1,5]")]
	[InlineData("fr-FR", "[default: 1,5]")]
	public void Help_shows_numeric_defaults_in_the_parsing_culture(string culture, string expected) =>
		Output(culture.Length == 0 ? null : Culture(culture), "culture-cmd", "--help").Should().Contain(expected);

	// ── number styles ──────────────────────────────────────────────────────

	[Theory]
	[InlineData("--count", "1,000", "int")]
	[InlineData("--amount", "1,000.5", "decimal")]
	[InlineData("--scale", "1,000.5", "double")]
	[InlineData("--ratio", "1,000.5", "double")]
	public void Numbers_reject_a_group_separator_by_default(string flag, string value, string type)
	{
		string[] args = flag == "--amount" ? [flag, value] : ["--amount", "1", flag, value];
		Error(null, ["culture-cmd", .. args]).Should().Contain($"invalid {type} for {flag}: '{value}'");
	}

	[Fact]
	public void Allowing_thousands_accepts_group_separators_in_every_number()
	{
		var fields = Fields(WithThousands(), "--amount", "1,000.5", "--count", "1,000", "--scale", "1,000.5", "--ratio", "1,000.5", "--tag", "x");
		fields[0].Should().Be("1000.5");
		fields[8].Should().Be("1000");
		fields[9].Should().Be("1000.5");
		fields[10].Should().Be("1000.5");
		fields[7].Should().Be("x@invariant/AllowThousands, Float", "the custom parser receives the same options");
	}

	[Fact]
	public void Number_styles_apply_to_every_numeric_value()
	{
		var environment = new Dictionary<string, string> { ["ARGH_TEST_NUMBER_STYLES"] = "Integer, AllowThousands" };
		var fields = Fields(environment, "--amount", "1,000", "--count", "1,000", "--tag", "x");
		fields[0].Should().Be("1000");
		fields[8].Should().Be("1000");
		fields[7].Should().Be("x@invariant/Integer, AllowThousands", "the custom parser receives the same options");
		Error(environment, "culture-cmd", "--amount", "1e3").Should().Contain("invalid decimal for --amount: '1e3'");
	}

	[Fact]
	public void Numbers_accept_an_exponent_and_surrounding_whitespace_by_default()
	{
		var fields = Fields(null, "--amount", "2.5e1", "--count", " 1e3 ", "--scale", "1.5E2", "--ratio=\t1e-1\t");
		fields[0].Should().Be("25");
		fields[8].Should().Be("1000");
		fields[9].Should().Be("150");
		fields[10].Should().Be("0.1");
	}

	[Theory]
	[InlineData("1.0", "1")]
	[InlineData("1e3", "1000")]
	[InlineData("10e-1", "1")]
	[InlineData(" 7 ", "7")]
	[InlineData("+7", "7")]
	[InlineData("-7", "-7")]
	public void Int_accepts_a_whole_number_with_a_decimal_point_or_an_exponent(string value, string expected) =>
		Fields(null, "--amount", "1", $"--count={value}")[8].Should().Be(expected);

	[Theory]
	[InlineData("1.5")]
	[InlineData("15e-1")]
	[InlineData("1e10")]
	public void Int_rejects_a_fraction_or_an_overflow(string value) =>
		Error(null, "culture-cmd", "--amount", "1", $"--count={value}").Should().Contain($"invalid int for --count: '{value}'");

	// ── date styles ────────────────────────────────────────────────────────

	[Fact]
	public void Date_time_styles_apply_to_dates_and_times()
	{
		var environment = new Dictionary<string, string> { ["ARGH_TEST_DATE_STYLES"] = "AdjustToUniversal" };
		var fields = Fields(environment, "--amount", "1", "--when", "2024-06-15T14:30:00+02:00",
			"--at", "2024-06-15T14:30:00+02:00", "--day", "2024-06-15");
		fields[1].Should().Be("2024-06-15 12:30");
		fields[4].Should().Be("2024-06-15T12:30:00.0000000+00:00");
		fields[2].Should().Be("2024-06-15", "DateOnly gets only the whitespace flags, which it accepts");
	}

	[Fact]
	public void Time_only_parses_whatever_the_date_time_styles() =>
		Output(new Dictionary<string, string> { ["ARGH_TEST_DATE_STYLES"] = "AssumeUniversal" }, "time-only-cmd", "--at", "14:30")
			.Should().Be("time-only:14:30:00:none");

	// ── tr-TR: the culture never reaches name matching ─────────────────────

	[Theory]
	[InlineData("severity-cmd", "--severity", "INFORMATION", "severity:Information")]
	[InlineData("severity-cmd", "--severity", "information", "severity:Information")]
	[InlineData("TIME-ONLY-CMD", "--at", "14:30", "time-only:14:30:00:none")]
	public void Turkish_culture_does_not_change_command_or_enum_matching(string command, string flag, string value, string expected) =>
		Output(Culture("tr-TR"), command, flag, value).Should().Be(expected);

	[Fact]
	public void Turkish_culture_parses_numbers()
	{
		var fields = Fields(Culture("tr-TR"), "--amount", "1,5", "--count", "7", "--scale", "2,5");
		fields[0].Should().Be("1.5");
		fields[8].Should().Be("7");
		fields[9].Should().Be("2.5");
	}

	// ── en-US and en-GB: the configured culture, not the machine's ─────────

	[Theory]
	[InlineData("en-US", "2024-06-07")]
	[InlineData("en-GB", "2024-07-06")]
	public void English_cultures_read_day_and_month_in_their_own_order(string culture, string expected)
	{
		var fields = Fields(Culture(culture), "--amount", "1", "--day", "06/07/2024", "--when", "06/07/2024");
		fields[1].Should().Be(expected + " 00:00");
		fields[2].Should().Be(expected);
	}

	// ── fr-FR: decimal comma, and the group separator is a narrow no-break space ──

	[Fact]
	public void French_culture_uses_a_decimal_comma()
	{
		Fields(Culture("fr-FR"), "--amount", "1,5")[0].Should().Be("1.5");
		Error(Culture("fr-FR"), "culture-cmd", "--amount", "1.5").Should().Contain("invalid decimal");
	}

	// ICU's fr-FR group separator is U+202F. With AllowThousands, .NET also accepts a plain space for it, but not U+00A0.
	[Theory]
	[InlineData(NarrowNoBreakSpace, true)]
	[InlineData(' ', true)]
	[InlineData(NoBreakSpace, false)]
	public void French_culture_group_separators_on_icu(char separator, bool parses)
	{
		var amount = $"1{separator}234,5";
		if (parses)
			Fields(WithThousands(Culture("fr-FR")), "--amount", amount)[0].Should().Be("1234.5");
		else
			Error(WithThousands(Culture("fr-FR")), "culture-cmd", "--amount", amount).Should().Contain("invalid decimal");
	}

	// ── fa-IR: the Persian calendar ────────────────────────────────────────

	// fa-IR's default calendar is Persian, and .NET reads a culture's dates in its calendar: an ISO date names a
	// Persian year, so 2024-06-15 is year 2024 of the Persian calendar, about 2645 in the Gregorian one. Apps that
	// expect ISO dates should keep the invariant culture.
	[Fact]
	public void Persian_culture_reads_iso_dates_in_the_persian_calendar()
	{
		var fields = Fields(Culture("fa-IR"), "--amount", "1", "--day", "2024-06-15", "--when", "2024-06-15");
		fields[2].Should().Be("2645-09-06");
		fields[1].Should().Be("2645-09-06 00:00");
	}

	[Fact]
	public void Persian_culture_reads_persian_dates()
	{
		var fields = Fields(Culture("fa-IR"), "--amount", "1", "--day", "1403/03/26", "--when", "1403/03/26");
		fields[2].Should().Be("2024-06-15");
		fields[1].Should().Be("2024-06-15 00:00");
	}

	[Fact]
	public void Persian_culture_reads_round_trip_dates_with_an_offset_as_gregorian() =>
		Fields(Culture("fa-IR"), "--amount", "1", "--at", "2024-06-15T14:30:00+02:00")[4].Should().Be("2024-06-15T14:30:00.0000000+02:00");

	// ICU's fa-IR decimal separator is U+066B (.NET Framework's NLS data uses '/').
	[Fact]
	public void Persian_culture_uses_the_arabic_decimal_separator_on_icu() =>
		Fields(Culture("fa-IR"), "--amount", $"1{ArabicDecimalSeparator}5")[0].Should().Be("1.5");

	// ── sv-SE: the minus sign ──────────────────────────────────────────────

	// ICU's sv-SE negative sign is U+2212; .NET also accepts the ASCII hyphen-minus. A separate "-5" token is
	// still read as the value of --amount, not as a flag.
	[Theory]
	[InlineData('-', true)]
	[InlineData(MinusSign, true)]
	[InlineData('-', false)]
	[InlineData(MinusSign, false)]
	public void Swedish_culture_accepts_both_minus_signs_on_icu(char minus, bool inline)
	{
		var value = $"{minus}5";
		var fields = inline
			? Fields(Culture("sv-SE"), $"--amount={value}", $"--count={value}")
			: Fields(Culture("sv-SE"), "--amount", value, "--count", value);
		fields[0].Should().Be("-5");
		fields[8].Should().Be("-5");
	}

	[Fact]
	public void Invariant_culture_rejects_the_unicode_minus_sign() =>
		Error(null, "culture-cmd", $"--amount={MinusSign}5").Should().Contain("invalid decimal");
}
