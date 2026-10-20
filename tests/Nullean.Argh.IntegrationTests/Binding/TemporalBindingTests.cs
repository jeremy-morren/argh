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

	[Fact]
	public void ValidateTimeSpanRange_out_of_range_exits_2()
	{
		var result = CliHostRunner.Run("validate-timespan-range", "--window", "3h");
		result.ExitCode.Should().Be(2);
		ConsoleOutput.Normalize(CliHostRunner.StderrText(result))
			.Should().Contain("value must be between");
	}
}
