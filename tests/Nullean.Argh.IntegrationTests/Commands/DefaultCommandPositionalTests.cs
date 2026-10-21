using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.Commands;

/// <summary>
/// The <c>ls</c> namespace: a default command with a variadic positional takes every argument no command matches,
/// and <c>--</c> lets through values that start with <c>-</c>.
/// </summary>
public class DefaultCommandPositionalTests
{
	private static readonly Dictionary<string, string> NoColor = new(StringComparer.Ordinal) { ["NO_COLOR"] = "1" };

	private static ProcNet.ProcessCaptureResult Run(params string[] args) => CliHostRunner.Run(NoColor, ["ls", .. args]);

	private static string Stdout(ProcNet.ProcessCaptureResult result) => CliHostRunner.StdoutText(result).Trim();

	[Theory]
	[InlineData("ls all:False long:False paths:")]
	[InlineData("ls all:False long:False paths:a,b", "a", "b")]
	[InlineData("ls all:True long:True paths:dir1,dir2,-weird-name", "-a", "-l", "dir1", "dir2", "--", "-weird-name")]
	[InlineData("ls all:False long:True paths:dir1", "dir1", "--long-format")]
	[InlineData("ls all:False long:False paths:version", "--", "version")]
	[InlineData("ls all:False long:False paths:-,x", "-", "x")]
	public void Default_command_takes_unmatched_arguments(string expected, params string[] args)
	{
		var result = Run(args);
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		Stdout(result).Should().Be(expected);
	}

	[Fact]
	public void A_command_name_still_runs_the_command()
	{
		var result = Run("version");
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		Stdout(result).Should().Be("ls-version");
	}

	[Fact]
	public void Unknown_option_is_still_an_error()
	{
		var result = Run("dir1", "--bogus");
		result.ExitCode.Should().Be(2);
		ConsoleOutput.Normalize(CliHostRunner.StderrText(result)).Should().StartWith("Error: unknown option '--bogus'.");
	}

	[Fact]
	public void Help_shows_the_default_command_usage_and_arguments()
	{
		var result = Run("--help");
		result.ExitCode.Should().Be(0);
		var help = ConsoleOutput.Normalize(CliHostRunner.StdoutText(result));
		help.Should().Contain($"Usage: {CliHostPaths.CliHostAssemblyName} ls <command> [options]\n");
		help.Should().Contain($"       {CliHostPaths.CliHostAssemblyName} ls [<paths...>] [options]\n");
		help.Should().Contain("Arguments for this default:\n  [<paths...>]  [variadic] Paths to list.\n");
	}
}
