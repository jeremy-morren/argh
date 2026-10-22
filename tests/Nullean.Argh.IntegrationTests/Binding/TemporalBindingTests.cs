using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.Binding;

public class TemporalBindingTests
{
	[Fact]
	public void TemporalCmd_compact_timespan_and_date_only()
	{
		var result = CliHostRunner.Run("temporal-cmd", "--duration", "5m", "--on", "2024-06-15");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("temporal:300:2024-06-15");
	}

	[Fact]
	public void TemporalCmd_standard_timespan_fallback()
	{
		var result = CliHostRunner.Run("temporal-cmd", "--duration", "01:30:00", "--on", "2024-01-01");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("temporal:5400:2024-01-01");
	}

	[Theory]
	[InlineData("1h30m", "5400")]
	[InlineData("1h 30m", "5400")]
	[InlineData("1.5h", "5400")]
	[InlineData("-5m", "-300")]
	[InlineData("300ms", "0.3")]
	[InlineData("2days 3hours", "183600")]
	[InlineData("1w", "604800")]
	[InlineData("0", "0")]
	[InlineData("1.02:03:04", "93784")]
	public void TemporalCmd_human_durations(string duration, string seconds)
	{
		var result = CliHostRunner.Run("temporal-cmd", "--duration", duration, "--on", "2024-01-01");
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		CliHostRunner.StdoutText(result).Trim().Should().Be($"temporal:{seconds}:2024-01-01");
	}

	[Theory]
	[InlineData("10", "Expected a unit after 10, such as 10s or 10m.")]
	[InlineData("5x", "Unknown unit 'x'.")]
	[InlineData("5H", "Unknown unit 'H'.")]
	[InlineData("150ns", "The duration is finer than a TimeSpan can hold (100 ns).")]
	[InlineData("30000y", "The duration is outside the TimeSpan range.")]
	[InlineData("25:00", "Expected [-][d.]hh:mm[:ss[.fffffff]], or a duration such as 1h30m.")]
	public void TemporalCmd_invalid_duration_says_why(string duration, string reason)
	{
		var result = CliHostRunner.Run("temporal-cmd", "--duration", duration, "--on", "2024-01-01");
		result.ExitCode.Should().Be(2);
		ConsoleOutput.Normalize(CliHostRunner.StderrText(result))
			.Should().Contain($"Error: invalid TimeSpan for --duration: '{duration}'. {reason}");
	}

	[Fact]
	public void TemporalCmd_reads_a_human_duration_from_the_environment()
	{
		var result = CliHostRunner.Run(new Dictionary<string, string> { ["ARGH_TEST_DURATION"] = "1h30m" }, "temporal-cmd", "--on", "2024-01-01");
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		CliHostRunner.StdoutText(result).Trim().Should().Be("temporal:5400:2024-01-01");
	}

	[Fact]
	public void TemporalCmd_rejects_a_bare_number_from_the_environment()
	{
		var result = CliHostRunner.Run(new Dictionary<string, string> { ["ARGH_TEST_DURATION"] = "10" }, "temporal-cmd", "--on", "2024-01-01");
		result.ExitCode.Should().Be(2);
		ConsoleOutput.Normalize(CliHostRunner.StderrText(result))
			.Should().Contain("Error: invalid TimeSpan for --duration: '10'. Expected a unit after 10");
	}

	[Theory]
	[InlineData("14:30", "time-only:14:30:00:none")]
	[InlineData("14:30:05", "time-only:14:30:05:none")]
	[InlineData("2:30 PM", "time-only:14:30:00:none")]
	public void TimeOnlyCmd_parses_time_of_day(string at, string expected)
	{
		var result = CliHostRunner.Run("time-only-cmd", "--at", at);
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be(expected);
	}

	[Fact]
	public void TimeOnlyCmd_nullable_time_only()
	{
		var result = CliHostRunner.Run("time-only-cmd", "--at", "08:00", "--until", "17:45");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("time-only:08:00:00:17:45:00");
	}

	[Theory]
	[InlineData("--at", "25:00")]
	[InlineData("--until", "noon")]
	public void TimeOnlyCmd_invalid_time_exits_2(string flag, string value)
	{
		var args = flag == "--at"
			? new[] { "time-only-cmd", "--at", value }
			: new[] { "time-only-cmd", "--at", "08:00", "--until", value };
		var result = CliHostRunner.Run(args);
		result.ExitCode.Should().Be(2);
		ConsoleOutput.Normalize(CliHostRunner.StderrText(result))
			.Should().Contain($"Error: invalid TimeOnly for {flag}: '{value}'.");
	}

	[Fact]
	public void TimeOnlyCmd_help_shows_time_only_hint()
	{
		var result = CliHostRunner.Run("time-only-cmd", "--help");
		result.ExitCode.Should().Be(0);
		ConsoleOutput.Normalize(CliHostRunner.StdoutText(result)).Should().Contain("--at <timeOnly>");
	}

	[Fact]
	public void ValidateTimeSpanRange_in_range_succeeds()
	{
		var result = CliHostRunner.Run("validate-timespan-range", "--window", "30m");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("ts-range:30");
	}

	[Theory]
	[InlineData("5m", "5")]
	[InlineData("2h", "120")]
	[InlineData("1h30m", "90")]
	[InlineData("00:05:00", "5")]
	public void ValidateTimeSpanRange_accepts_the_bounds_and_any_form_between_them(string window, string minutes)
	{
		var result = CliHostRunner.Run("validate-timespan-range", "--window", window);
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		CliHostRunner.StdoutText(result).Trim().Should().Be($"ts-range:{minutes}");
	}

	[Theory]
	[InlineData("3h")]
	[InlineData("4m59s")]
	[InlineData("2h 100ms")]
	public void ValidateTimeSpanRange_out_of_range_exits_2(string window)
	{
		var result = CliHostRunner.Run("validate-timespan-range", "--window", window);
		result.ExitCode.Should().Be(2);
		ConsoleOutput.Normalize(CliHostRunner.StderrText(result))
			.Should().Contain("value must be between 5m and 2h.");
	}

	[Fact]
	public void ValidateTimeSpanRange_help_shows_the_bounds_as_written()
	{
		var result = CliHostRunner.Run("validate-timespan-range", "--help");
		result.ExitCode.Should().Be(0);
		ConsoleOutput.Normalize(CliHostRunner.StdoutText(result)).Should().Contain("[time-span-range: 5m-2h]");
	}
}
