using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.ParseErrors;

/// <summary>Arguments nothing declares are rejected as clap rejects them; everything after <c>--</c> is positional.</summary>
public class UnexpectedArgumentTests
{
	private static readonly Dictionary<string, string> NoColor = new(StringComparer.Ordinal) { ["NO_COLOR"] = "1" };

	private static string Stderr(ProcNet.ProcessCaptureResult result) => ConsoleOutput.Normalize(CliHostRunner.StderrText(result));

	private static string Stdout(ProcNet.ProcessCaptureResult result) => CliHostRunner.StdoutText(result).Trim();

	[Theory]
	[InlineData("extra", "hello", "--name", "x", "extra")]
	[InlineData("foo", "count-cmd", "--count", "1", "foo", "bar")]
	[InlineData("extra", "build", "extra")]
	[InlineData("extra", "storage", "list", "extra")]
	[InlineData("extra", "temporal-cmd", "--duration", "1m", "--on", "2024-01-01", "extra")]
	public void Extra_positional_is_an_error(string unexpected, params string[] args)
	{
		var result = CliHostRunner.Run(NoColor, args);
		result.ExitCode.Should().Be(2);
		var err = Stderr(result);
		err.Should().StartWith($"Error: unexpected argument '{unexpected}' found.");
		var route = string.Join(" ", args.TakeWhile(a => !a.StartsWith('-') && a != unexpected));
		err.Should().Contain($"Run '{CliHostPaths.CliHostAssemblyName} {route} --help' for usage.");
		Stdout(result).Should().NotContain("ok:");
	}

	[Fact]
	public void Extra_positional_after_the_declared_ones_is_an_error()
	{
		var result = CliHostRunner.Run(NoColor, "env-positional", "bob", "2", "extra");
		result.ExitCode.Should().Be(2);
		Stderr(result).Should().StartWith("Error: unexpected argument 'extra' found.");
	}

	[Fact]
	public void Double_dash_alone_is_not_an_unknown_option()
	{
		var result = CliHostRunner.Run(NoColor, "hello", "--name", "x", "--");
		result.ExitCode.Should().Be(0, Stderr(result));
		Stdout(result).Should().EndWith("ok:x");
	}

	[Fact]
	public void Argument_after_double_dash_on_a_command_without_positionals_is_unexpected()
	{
		var result = CliHostRunner.Run(NoColor, "hello", "--name", "x", "--", "--verbose");
		result.ExitCode.Should().Be(2);
		Stderr(result).Should().StartWith("Error: unexpected argument '--verbose' found.");
	}

	[Fact]
	public void Everything_after_double_dash_is_positional()
	{
		var result = CliHostRunner.Run(NoColor, "copy-variadic", "red", "--", "-x", "--help", "-5");
		result.ExitCode.Should().Be(0, Stderr(result));
		Stdout(result).Should().Be("mode:Red files:-x,--help,-5");
	}

	[Fact]
	public void Double_dash_can_come_before_every_positional()
	{
		var result = CliHostRunner.Run(NoColor, "copy-variadic", "--", "blue", "-a");
		result.ExitCode.Should().Be(0, Stderr(result));
		Stdout(result).Should().Be("mode:Blue files:-a");
	}

	[Fact]
	public void Second_double_dash_is_a_positional()
	{
		var result = CliHostRunner.Run(NoColor, "copy-variadic", "red", "--", "--");
		result.ExitCode.Should().Be(0, Stderr(result));
		Stdout(result).Should().Be("mode:Red files:--");
	}

	[Fact]
	public void Root_default_command_takes_double_dash()
	{
		var result = CliHostRunner.Run(NoColor, "--");
		result.ExitCode.Should().Be(0, Stderr(result));
		Stdout(result).Should().EndWith("marker:root-default");
	}

	[Fact]
	public void Root_default_command_without_positionals_rejects_an_argument_after_double_dash()
	{
		var result = CliHostRunner.Run(NoColor, "--verbose", "--", "x");
		result.ExitCode.Should().Be(2);
		Stderr(result).Should().StartWith("Error: unexpected argument 'x' found.");
	}

	[Fact]
	public void Namespace_without_positionals_rejects_an_argument_after_double_dash()
	{
		var result = CliHostRunner.Run(NoColor, "storage", "--", "list");
		result.ExitCode.Should().Be(2);
		var err = Stderr(result);
		err.Should().StartWith("Error: unexpected argument 'list' found.");
		err.Should().Contain($"Run '{CliHostPaths.CliHostAssemblyName} storage --help' for usage.");
	}

	[Fact]
	public void Namespace_without_a_default_command_rejects_an_argument_after_double_dash()
	{
		var result = CliHostRunner.Run(NoColor, "billing", "--", "tools");
		result.ExitCode.Should().Be(2);
		var err = Stderr(result);
		err.Should().StartWith("Error: unexpected argument 'tools' found.");
		err.Should().Contain($"Run '{CliHostPaths.CliHostAssemblyName} billing --help' for usage.");
	}

	[Fact]
	public void Namespace_without_a_default_command_shows_its_help_for_double_dash_alone()
	{
		var result = CliHostRunner.Run(NoColor, "billing", "--");
		result.ExitCode.Should().Be(0, Stderr(result));
		Stdout(result).Should().Contain("billing tools");
	}

	[Fact]
	public void Short_option_like_value_without_double_dash_is_an_unknown_option()
	{
		var result = CliHostRunner.Run(NoColor, "copy-variadic", "red", "-5");
		result.ExitCode.Should().Be(2);
		Stderr(result).Should().StartWith("Error: unknown short option '-5'.");
	}

	[Fact]
	public void Known_short_option_without_its_value_is_still_missing_a_value()
	{
		var result = CliHostRunner.Run(NoColor, "hello", "-m");
		result.ExitCode.Should().Be(2);
		Stderr(result).Should().StartWith("Error: missing value for short flag '-m'.");
	}
}
