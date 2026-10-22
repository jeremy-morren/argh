using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.Commands;

/// <summary>
/// Command and namespace names ignore case and long option names match case by default; <c>ParserOptions</c>'
/// <c>CommandStringComparer</c> and <c>ArgumentStringComparer</c> (set through <c>ARGH_TEST_COMMAND_COMPARER</c> and
/// <c>ARGH_TEST_ARGUMENT_COMPARER</c>) change that for running, help, "did you mean" and completion alike. Short options
/// always match case.
/// </summary>
public class NameMatchingTests
{
	private static Dictionary<string, string> Env(string? commands = null, string? arguments = null)
	{
		var env = new Dictionary<string, string>(StringComparer.Ordinal) { ["NO_COLOR"] = "1" };
		if (commands is not null)
			env["ARGH_TEST_COMMAND_COMPARER"] = commands;
		if (arguments is not null)
			env["ARGH_TEST_ARGUMENT_COMPARER"] = arguments;
		return env;
	}

	private static readonly Dictionary<string, string> OrdinalCommands = Env(commands: "Ordinal");

	private static readonly Dictionary<string, string> IgnoreCaseArguments = Env(arguments: "OrdinalIgnoreCase");

	private static string Stderr(ProcNet.ProcessCaptureResult result) => ConsoleOutput.Normalize(CliHostRunner.StderrText(result));

	private static string Stdout(ProcNet.ProcessCaptureResult result) => CliHostRunner.StdoutText(result).Trim();

	private static void Succeeds(ProcNet.ProcessCaptureResult result, string output)
	{
		result.ExitCode.Should().Be(0, Stderr(result));
		Stdout(result).Should().Be(output);
	}

	private static string Fails(ProcNet.ProcessCaptureResult result)
	{
		result.ExitCode.Should().Be(2, Stdout(result));
		return Stderr(result);
	}

	private static string Shorts(bool quiet = false, string name = "", string color = "null", string mode = "") =>
		$"quiet:{quiet} force:False name:{name} count:0 ratio:0 tags: color:{color} verbose:False mode:{mode}";

	private static string Complete(Dictionary<string, string>? env, params string[] words)
	{
		var result = CliHostRunner.Run(env, ["__complete", "bash", "--", .. words]);
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		return CliHostRunner.StdoutText(result);
	}

	// ── Commands and namespaces ───────────────────────────────────────────────

	[Theory]
	[InlineData("ok:x", "HELLO", "--name", "x")]
	[InlineData("storage-list", "Storage", "LIST")]
	[InlineData("quiet:True force:False name: count:0 ratio:0 tags: color:null verbose:False mode:", "TOKENS", "Shorts", "-q")]
	public void Command_and_namespace_names_ignore_case_by_default(string output, params string[] args) =>
		Succeeds(CliHostRunner.Run(Env(), args), output);

	[Theory]
	[InlineData("HELLO", "--name", "x")]
	[InlineData("Hello", "--name", "x")]
	public void An_ordinal_command_comparer_matches_case(params string[] args)
	{
		Fails(CliHostRunner.Run(OrdinalCommands, args)).Should().StartWith($"Error: unknown command or namespace '{args[0]}'.");
		Succeeds(CliHostRunner.Run(OrdinalCommands, "hello", "--name", "x"), "ok:x");
	}

	[Fact]
	public void An_ordinal_command_comparer_matches_case_in_a_namespace()
	{
		Fails(CliHostRunner.Run(OrdinalCommands, "storage", "LIST")).Should().StartWith("Error: unknown command or namespace 'LIST'.");
		Fails(CliHostRunner.Run(OrdinalCommands, "STORAGE", "list")).Should().StartWith("Error: unknown command or namespace 'STORAGE'.");
	}

	[Fact]
	public void Did_you_mean_compares_with_the_command_comparer()
	{
		Fails(CliHostRunner.Run(Env(), "HELO")).Should().StartWith("Error: unknown command or namespace 'HELO'. Did you mean 'hello'?");
		Fails(CliHostRunner.Run(OrdinalCommands, "HELO")).Should().StartWith("Error: unknown command or namespace 'HELO'.")
			.And.NotContain("Did you mean");
	}

	[Fact]
	public void Completion_matches_command_names_with_the_command_comparer()
	{
		Complete(Env(), "HELL").Should().Be("hello\n");
		Complete(Env(), "storage", "LI").Should().Be("list\n");
		Complete(OrdinalCommands, "HELL").Should().BeEmpty();
		Complete(OrdinalCommands, "hell").Should().Be("hello\n");
		Complete(Env(), "STORAGE", "bl").Should().Be("blob\n");
		Complete(OrdinalCommands, "STORAGE", "bl").Should().BeEmpty();
	}

	// ── Options ───────────────────────────────────────────────────────────────

	[Theory]
	[InlineData("hello", "--Help")]
	[InlineData("tokens", "shorts", "--HELP")]
	public void Help_matches_case_by_default(params string[] args) =>
		Fails(CliHostRunner.Run(Env(), args)).Should().StartWith($"Error: unknown option '{args[^1]}'.");

	[Fact]
	public void An_ignore_case_argument_comparer_matches_long_options_in_any_case()
	{
		Succeeds(CliHostRunner.Run(IgnoreCaseArguments, "hello", "--NAME", "x"), "ok:x");
		Succeeds(CliHostRunner.Run(IgnoreCaseArguments, "tokens", "shorts", "--Quiet", "--NO-COLOR", "--Name=x"),
			Shorts(quiet: true, name: "x", color: "False"));
		Succeeds(CliHostRunner.Run(IgnoreCaseArguments, "--MODE", "m", "tokens", "shorts"), Shorts(mode: "m"));
		Succeeds(CliHostRunner.Run(IgnoreCaseArguments, "storage", "--PREFIX", "p", "list"), "storage-list");
	}

	[Theory]
	[InlineData("hello", "--HELP")]
	[InlineData("--Help")]
	[InlineData("storage", "--HELP")]
	public void An_ignore_case_argument_comparer_matches_help(params string[] args)
	{
		var result = CliHostRunner.Run(IgnoreCaseArguments, args);
		result.ExitCode.Should().Be(0, Stderr(result));
		Stdout(result).Should().Contain("Usage:");
	}

	[Fact]
	public void An_ignore_case_argument_comparer_matches_version()
	{
		var result = CliHostRunner.Run(IgnoreCaseArguments, "--VERSION");
		result.ExitCode.Should().Be(0, Stderr(result));
		Stdout(result).Should().Be(Stdout(CliHostRunner.Run(Env(), "--version")));
	}

	[Theory]
	[InlineData("-Q")]
	[InlineData("-N", "x")]
	public void Short_options_match_case_whatever_the_argument_comparer(params string[] args) =>
		Fails(CliHostRunner.Run(IgnoreCaseArguments, ["tokens", "shorts", .. args])).Should().StartWith($"Error: unknown short option '{args[0]}'.");

	[Fact]
	public void Did_you_mean_compares_options_with_the_argument_comparer()
	{
		Fails(CliHostRunner.Run(Env(), "hello", "--NAEM", "x")).Should().StartWith("Error: unknown option '--NAEM'.")
			.And.NotContain("Did you mean");
		Fails(CliHostRunner.Run(IgnoreCaseArguments, "hello", "--NAEM", "x")).Should().StartWith("Error: unknown option '--NAEM'. Did you mean '--name'?");
	}

	[Fact]
	public void Completion_matches_options_with_the_argument_comparer()
	{
		Complete(Env(), "hello", "--NA").Should().BeEmpty();
		Complete(Env(), "hello", "--na").Should().Be("--name\n");
		Complete(IgnoreCaseArguments, "hello", "--NA").Should().Be("--name\n");
		Complete(IgnoreCaseArguments, "tokens", "shorts", "-Q").Should().BeEmpty();
		Complete(IgnoreCaseArguments, "tokens", "shorts", "-q").Should().Be("-q\n");
	}

	[Fact]
	public void Completion_of_values_after_an_option_matches_it_with_the_argument_comparer()
	{
		Complete(Env(), "hello", "--SEVERITY", "w").Should().BeEmpty();
		Complete(IgnoreCaseArguments, "hello", "--SEVERITY", "w").Should().Be("warning\n");
		Complete(IgnoreCaseArguments, "hello", "--Severity=w").Should().Be("--Severity=warning\n");
	}
}
