using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.Help;

/// <summary>
/// <c>--help</c> and <c>-h</c> are read in order like any other flag. As in clap, one isn't the value of a flag before it,
/// which is then missing its value; <c>--name=--help</c> gives it as a value.
/// </summary>
public class HelpFlagPositionTests
{
	[Theory]
	[InlineData("--help")]
	[InlineData("-h")]
	public void Help_after_a_value_taking_flag_leaves_it_without_a_value(string help)
	{
		var result = CliHostRunner.Run("hello", "--name", help);
		result.ExitCode.Should().Be(2);
		CliHostRunner.StderrText(result).Should().StartWith("Error: a value is required for --name but none was supplied.");
		CliHostRunner.StdoutText(result).Should().NotContain("Usage:");
	}

	[Fact]
	public void Help_attached_to_a_flag_is_its_value()
	{
		var result = CliHostRunner.Run("hello", "--name=--help");
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		CliHostRunner.StdoutText(result).Trim().Should().Be("ok:--help");
	}

	[Theory]
	[InlineData("hello", "--help")]
	[InlineData("hello", "-h")]
	[InlineData("hello", "--name", "x", "--help")]
	[InlineData("hello", "--help", "--name", "x")]
	[InlineData("hello", "--name", "x", "-vh")]
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
