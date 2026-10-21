using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.ParseErrors;

/// <summary>
/// Validation attributes on global and namespace options members are checked like method parameters, wherever the value
/// comes from: before the command, after it, or an environment variable.
/// </summary>
public class OptionsMemberValidationTests
{
	private static Dictionary<string, string> Env(string? culture = null, params (string Name, string Value)[] variables)
	{
		var env = new Dictionary<string, string>(StringComparer.Ordinal) { ["NO_COLOR"] = "1" };
		if (culture is not null)
			env["ARGH_TEST_CULTURE"] = culture;
		foreach (var (name, value) in variables)
			env[name] = value;
		return env;
	}

	private static string LastOutputLine(ProcNet.ProcessCaptureResult result) =>
		CliHostRunner.StdoutText(result).Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).Last();

	[Theory]
	// Namespace options member, after the command and before it.
	[InlineData("Error: --ceiling: value must be at most 100.", "numeric-ns", "show", "--ceiling", "101")]
	[InlineData("Error: --ceiling: value must be at most 100.", "numeric-ns", "--ceiling", "101", "show")]
	[InlineData("Error: --step: value must be one of: 0.1, 0.5.", "numeric-ns", "show", "--step", "0.2")]
	[InlineData("Error: --step: value must be one of: 0.1, 0.5.", "numeric-ns", "--step", "0.2", "show")]
	[InlineData("Error: --shards: value must be between 1 and 10.", "numeric-ns", "show", "--shards", "0")]
	[InlineData("Error: --shards: value must be between 1 and 10.", "numeric-ns", "--shards", "11", "show")]
	[InlineData("Error: --lucky: value must not be: 13.", "numeric-ns", "show", "--lucky", "13")]
	[InlineData("Error: --ratio: value must be between 0.5 and 1.5.", "numeric-ns", "show", "--ratio", "1.6")]
	[InlineData("Error: --ratio: value must be between 0.5 and 1.5.", "numeric-ns", "--ratio", "0.4", "show")]
	[InlineData("Error: --tag: value must be between 2 and 5 characters.", "numeric-ns", "show", "--tag", "a")]
	// Global options member, before the namespace, after the command, and around a root command.
	[InlineData("Error: --quota: value must be at least 1.", "--quota", "0", "numeric-ns", "show")]
	[InlineData("Error: --quota: value must be at least 1.", "numeric-ns", "show", "--quota", "0")]
	[InlineData("Error: --quota: value must be at least 1.", "--quota", "0", "numeric-defaults")]
	[InlineData("Error: --quota: value must be at least 1.", "numeric-defaults", "--quota", "-5")]
	[InlineData("Error: --zone: value must be one of: eu, us.", "--zone", "xx", "numeric-defaults")]
	[InlineData("Error: --zone: value must be one of: eu, us.", "numeric-ns", "show", "--zone", "EU")]
	[InlineData("Error: --weight: value must be between 0.5 and 2.5.", "numeric-defaults", "--weight", "2.6")]
	[InlineData("Error: --weight: value must be between 0.5 and 2.5.", "--weight", "0.49", "numeric-ns", "show")]
	public void Invalid_options_member_exits_2_with_the_parameter_error(string error, params string[] args)
	{
		var result = CliHostRunner.Run(Env(), args);
		result.ExitCode.Should().Be(2, CliHostRunner.StdoutText(result));
		var stderr = ConsoleOutput.Normalize(CliHostRunner.StderrText(result));
		stderr.Should().StartWith(error);
		stderr.Should().Contain("for usage.");
	}

	[Theory]
	[InlineData("ARGH_TEST_NS_SHARDS", "11", "Error: --shards: value must be between 1 and 10.", "numeric-ns", "show")]
	[InlineData("ARGH_TEST_QUOTA", "0", "Error: --quota: value must be at least 1.", "numeric-ns", "show")]
	[InlineData("ARGH_TEST_QUOTA", "0", "Error: --quota: value must be at least 1.", "numeric-defaults")]
	public void Invalid_value_from_an_environment_variable_is_rejected(string variable, string value, string error, params string[] args)
	{
		var result = CliHostRunner.Run(Env(null, (variable, value)), args);
		result.ExitCode.Should().Be(2);
		ConsoleOutput.Normalize(CliHostRunner.StderrText(result)).Should().StartWith(error);
	}

	[Theory]
	[InlineData("Error: --ratio: value must be between 0,5 and 1,5.", "numeric-ns", "show", "--ratio", "0,4")]
	[InlineData("Error: --ceiling: value must be at most 100.", "numeric-ns", "--ceiling", "100,5", "show")]
	[InlineData("Error: --step: value must be one of: 0,1, 0,5.", "numeric-ns", "show", "--step", "0,2")]
	[InlineData("Error: --weight: value must be between 0,5 and 2,5.", "--weight", "3", "numeric-defaults")]
	public void Errors_format_options_member_limits_in_the_parse_culture(string error, params string[] args)
	{
		var result = CliHostRunner.Run(Env("de-DE"), args);
		result.ExitCode.Should().Be(2);
		ConsoleOutput.Normalize(CliHostRunner.StderrText(result)).Should().StartWith(error);
	}

	[Fact]
	public void Error_footer_shows_the_options_member_help_row()
	{
		var result = CliHostRunner.Run(Env(), "numeric-ns", "show", "--lucky", "13");
		var stderr = ConsoleOutput.Normalize(CliHostRunner.StderrText(result));
		stderr.Should().Contain("--lucky <value>").And.Contain("[denied: 13]")
			.And.Contain("Run 'Nullean.Argh.Tests.CliHost numeric-ns show --help' for usage.");
	}

	[Fact]
	public void Valid_options_members_bind_from_every_position()
	{
		var result = CliHostRunner.Run(Env(null, ("ARGH_TEST_NS_SHARDS", "4")),
			"--quota", "3", "numeric-ns", "--ceiling", "100", "show", "--step", "0.5", "--lucky", "7", "--ratio", "1.5",
			"--anything", "-9223372036854775808", "--tag", "abcde", "--zone", "us", "--weight", "0.5");
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		LastOutputLine(result).Should().Be(
			"0.25|1.5|NaN|18446744073709551615|-7|High|100|0.5|4|7|1.5|-9223372036854775808|abcde|3|us|0.5");
	}

	[Theory]
	[InlineData("--quota", "2147483647")]
	[InlineData("--zone", "eu")]
	[InlineData("--weight", "2.5")]
	public void Valid_global_options_member_on_a_root_command_passes(string flag, string value)
	{
		var result = CliHostRunner.Run(Env(), "numeric-defaults", flag, value);
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
	}

	[Fact]
	public void Unset_options_members_pass_every_check()
	{
		var result = CliHostRunner.Run(Env(), "numeric-ns", "show");
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
	}

	[Theory]
	[InlineData("--shards", "[env: ARGH_TEST_NS_SHARDS] [range: 1-10]")]
	[InlineData("--lucky", "[denied: 13]")]
	[InlineData("--ratio", "[range: 0.5-1.5]")]
	[InlineData("--tag", "[length: 2-5]")]
	[InlineData("--quota", "[env: ARGH_TEST_QUOTA] [min: 1]")]
	[InlineData("--zone", "[allowed: eu|us]")]
	[InlineData("--weight", "[range: 0.5-2.5]")]
	public void Command_help_shows_options_member_hints(string flag, string hint)
	{
		var result = CliHostRunner.Run(Env(), "numeric-ns", "show", "--help");
		ConsoleOutput.Normalize(CliHostRunner.StdoutText(result)).Split('\n')
			.Single(l => l.TrimStart().StartsWith(flag + " ", StringComparison.Ordinal))
			.TrimEnd().Should().EndWith(hint);
	}

	[Fact]
	public void Unbounded_options_member_range_shows_no_hint()
	{
		var result = CliHostRunner.Run(Env(), "numeric-ns", "show", "--help");
		ConsoleOutput.Normalize(CliHostRunner.StdoutText(result)).Split('\n')
			.Single(l => l.TrimStart().StartsWith("--anything ", StringComparison.Ordinal))
			.Should().NotContain("[range").And.NotContain("[min").And.NotContain("[max");
	}
}
