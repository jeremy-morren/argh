using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.Help;

/// <summary><c>--help</c> and <c>-h</c> are read in order like any other flag, so they can be the value of the flag before them.</summary>
public class HelpFlagPositionTests
{
	[Theory]
	[InlineData("--help")]
	[InlineData("-h")]
	public void Help_after_a_value_taking_flag_is_its_value(string help)
	{
		var result = CliHostRunner.Run("hello", "--name", help);
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		CliHostRunner.StdoutText(result).Trim().Should().Be($"ok:{help}");
	}

	[Theory]
	[InlineData("hello", "--help")]
	[InlineData("hello", "-h")]
	[InlineData("hello", "--name", "x", "--help")]
	[InlineData("hello", "--help", "--name", "x")]
	[InlineData("scalars-ns", "show", "--help")]
	public void Help_as_a_flag_of_its_own_shows_command_help(params string[] args)
	{
		var result = CliHostRunner.Run(args);
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		CliHostRunner.StdoutText(result).Should().Contain("Usage:");
	}

	[Fact]
	public void Unknown_option_before_help_is_an_error()
	{
		var result = CliHostRunner.Run("hello", "--bogus", "--help");
		result.ExitCode.Should().Be(2);
		CliHostRunner.StderrText(result).Should().Contain("unknown option '--bogus'");
	}
}
