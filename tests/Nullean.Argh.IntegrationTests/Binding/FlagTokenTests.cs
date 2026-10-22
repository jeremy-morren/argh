using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.Binding;

/// <summary>
/// Short flags combine, the argument after a flag is its value unless it's an option, and a flag that takes one value is
/// given once, as in clap; <c>RepeatedScalarBehaviour</c> and <c>[RepeatedScalar]</c> change the last.
/// </summary>
public class FlagTokenTests
{
	private static readonly Dictionary<string, string> NoColor = new(StringComparer.Ordinal) { ["NO_COLOR"] = "1" };

	private static Dictionary<string, string> Repeated(string behaviour) =>
		new(StringComparer.Ordinal) { ["NO_COLOR"] = "1", ["ARGH_TEST_REPEATED"] = behaviour };

	private static string Stderr(ProcNet.ProcessCaptureResult result) => ConsoleOutput.Normalize(CliHostRunner.StderrText(result));

	private static string Stdout(ProcNet.ProcessCaptureResult result) => CliHostRunner.StdoutText(result).Trim();

	private static string Shorts(bool quiet = false, bool force = false, string name = "", int count = 0, string ratio = "0",
		string tags = "", string color = "null", bool verbose = false, string mode = "") =>
		$"quiet:{quiet} force:{force} name:{name} count:{count} ratio:{ratio} tags:{tags} color:{color} verbose:{verbose} mode:{mode}";

	private static void Succeeds(ProcNet.ProcessCaptureResult result, string output)
	{
		result.ExitCode.Should().Be(0, Stderr(result));
		Stdout(result).Should().Be(output);
	}

	private static void Fails(ProcNet.ProcessCaptureResult result, string error, string runHint = "tokens shorts")
	{
		result.ExitCode.Should().Be(2);
		var err = Stderr(result);
		err.Should().StartWith(error);
		var scope = runHint.Length == 0 ? "" : " " + runHint;
		err.Should().Contain($"Run '{CliHostPaths.CliHostAssemblyName}{scope} --help' for usage.");
		Stdout(result).Should().NotContain("quiet:");
	}

	// ── Combined short flags ──────────────────────────────────────────────────

	[Fact]
	public void Switches_combine() =>
		Succeeds(CliHostRunner.Run("tokens", "shorts", "-qf"), Shorts(quiet: true, force: true));

	[Theory]
	[InlineData("-qn", "foo")]
	[InlineData("-qnfoo")]
	[InlineData("-qn=foo")]
	public void Switches_combine_with_a_flag_that_takes_a_value(params string[] args) =>
		Succeeds(CliHostRunner.Run(["tokens", "shorts", .. args]), Shorts(quiet: true, name: "foo"));

	[Theory]
	[InlineData("-nfoo")]
	[InlineData("-n=foo")]
	[InlineData("-n", "foo")]
	public void Short_flag_takes_an_attached_value(params string[] args) =>
		Succeeds(CliHostRunner.Run(["tokens", "shorts", .. args]), Shorts(name: "foo"));

	[Fact]
	public void Attached_value_keeps_the_rest_of_the_argument() =>
		Succeeds(CliHostRunner.Run("tokens", "shorts", "-nqf"), Shorts(name: "qf"));

	[Fact]
	public void Switch_in_a_group_takes_a_value_after_an_equals_sign() =>
		Succeeds(CliHostRunner.Run("tokens", "shorts", "-fq=false"), Shorts(force: true));

	[Fact]
	public void Collection_short_flag_combines_and_repeats() =>
		Succeeds(CliHostRunner.Run("tokens", "shorts", "-qta", "-t", "b"), Shorts(quiet: true, tags: "a,b"));

	[Fact]
	public void Global_and_command_short_flags_combine_after_the_command() =>
		Succeeds(CliHostRunner.Run("tokens", "shorts", "-vqmx"), Shorts(quiet: true, verbose: true, mode: "x"));

	[Fact]
	public void Global_short_flags_combine_before_the_command() =>
		Succeeds(CliHostRunner.Run("-vm", "x", "tokens", "shorts"), Shorts(verbose: true, mode: "x"));

	[Fact]
	public void Unknown_short_flag_in_a_group_is_an_error() =>
		Fails(CliHostRunner.Run(NoColor, "tokens", "shorts", "-qz"), "Error: unknown short option '-z'.");

	[Fact]
	public void Help_in_a_group_shows_the_help()
	{
		var result = CliHostRunner.Run("tokens", "shorts", "-qh");
		result.ExitCode.Should().Be(0, Stderr(result));
		Stdout(result).Should().Contain("Usage:");
	}

	[Fact]
	public void Group_ending_with_a_flag_that_takes_a_value_needs_the_next_argument() =>
		Fails(CliHostRunner.Run(NoColor, "tokens", "shorts", "-qn"), "Error: a value is required for --name but none was supplied.");

	[Theory]
	[InlineData("hello", "--NAME", "x")]
	[InlineData("tokens", "shorts", "--Quiet")]
	[InlineData("--MODE", "x", "tokens", "shorts")]
	[InlineData("storage", "--PREFIX", "p", "list")]
	public void Long_flag_names_match_case(params string[] args)
	{
		var result = CliHostRunner.Run(NoColor, args);
		result.ExitCode.Should().Be(2);
		var option = args.First(a => a.StartsWith("--", StringComparison.Ordinal));
		Stderr(result).Should().StartWith($"Error: unknown option '{option}'.");
	}

	// ── The argument after a flag ─────────────────────────────────────────────

	[Theory]
	[InlineData("--name", "--count", "5")]
	[InlineData("--name", "-c", "5")]
	[InlineData("--name", "--help")]
	[InlineData("--name", "-h")]
	[InlineData("--name", "--")]
	[InlineData("-n", "--quiet")]
	[InlineData("--name", "--verbose")]
	[InlineData("--name", "-v")]
	[InlineData("--name", "--no-color")]
	public void Known_option_after_a_flag_leaves_it_without_a_value(params string[] args)
	{
		var result = CliHostRunner.Run(NoColor, ["tokens", "shorts", .. args]);
		Fails(result, "Error: a value is required for --name but none was supplied.");
		Stderr(result).Should().Contain("--name <string>");
	}

	[Fact]
	public void Known_option_after_a_numeric_flag_leaves_it_without_a_value() =>
		Fails(CliHostRunner.Run(NoColor, "tokens", "shorts", "--count", "--name", "x"),
			"Error: a value is required for --count but none was supplied.");

	[Fact]
	public void Unknown_long_option_after_a_flag_is_reported_as_unknown() =>
		Fails(CliHostRunner.Run(NoColor, "tokens", "shorts", "--name", "--bogus"), "Error: unknown option '--bogus'.");

	[Fact]
	public void Unknown_short_option_after_a_flag_is_reported_as_unknown() =>
		Fails(CliHostRunner.Run(NoColor, "tokens", "shorts", "--name", "-x"), "Error: unknown short option '-x'.");

	[Theory]
	[InlineData("--count", "-5", "count", "-5")]
	[InlineData("-c", "-5", "count", "-5")]
	[InlineData("-c-5", null, "count", "-5")]
	[InlineData("--ratio", "-1.5", "ratio", "-1.5")]
	[InlineData("--ratio", "-2e-3", "ratio", "-0.002")]
	[InlineData("--name", "-1", "name", "-1")]
	[InlineData("--name", "-", "name", "-")]
	[InlineData("--name=--help", null, "name", "--help")]
	[InlineData("-n=-h", null, "name", "-h")]
	public void Negative_number_dash_or_attached_value_is_a_value(string flag, string? value, string member, string bound)
	{
		var result = CliHostRunner.Run(value is null ? ["tokens", "shorts", flag] : ["tokens", "shorts", flag, value]);
		result.ExitCode.Should().Be(0, Stderr(result));
		Stdout(result).Should().Contain($"{member}:{bound} ");
	}

	[Fact]
	public void Known_option_after_a_global_flag_before_the_command_leaves_it_without_a_value() =>
		Fails(CliHostRunner.Run(NoColor, "--mode", "--verbose", "tokens", "shorts"),
			"Error: a value is required for --mode but none was supplied.", runHint: "");

	[Fact]
	public void Known_option_after_a_namespace_flag_leaves_it_without_a_value() =>
		Fails(CliHostRunner.Run(NoColor, "storage", "--prefix", "--verbose", "list"),
			"Error: a value is required for --prefix but none was supplied.", runHint: "");

	[Fact]
	public void Known_option_after_a_dto_member_leaves_it_without_a_value() =>
		Fails(CliHostRunner.Run(NoColor, "tokens", "repeats-dto", "--level", "--tag", "x"),
			"Error: a value is required for --level but none was supplied.", runHint: "tokens repeats-dto");

	// ── A flag given more than once ───────────────────────────────────────────

	[Theory]
	[InlineData("name", "--name", "a", "--name", "b")]
	[InlineData("name", "-n", "a", "--name", "a")]
	[InlineData("name", "-na", "-nb")]
	[InlineData("quiet", "-qq")]
	[InlineData("quiet", "--quiet", "-q")]
	[InlineData("color", "--color", "--no-color")]
	[InlineData("count", "--count", "1", "--count", "x")]
	public void Flag_given_twice_is_an_error(string flag, params string[] args)
	{
		var result = CliHostRunner.Run(NoColor, ["tokens", "shorts", .. args]);
		Fails(result, $"Error: the argument --{flag} cannot be used multiple times.");
		Stderr(result).Should().Contain($"--{flag}");
	}

	[Theory]
	[InlineData("tokens shorts", "--mode", "a", "tokens", "shorts", "--mode", "b")]
	[InlineData("", "--mode", "a", "storage", "--mode", "b", "list")]
	[InlineData("storage list", "storage", "--mode", "a", "list", "--mode", "b")]
	[InlineData("storage list", "storage", "--prefix", "a", "list", "--prefix", "b")]
	public void Option_given_before_and_after_a_command_or_namespace_is_given_twice(string runHint, params string[] args) =>
		Fails(CliHostRunner.Run(NoColor, args), "Error: the argument --", runHint);

	[Fact]
	public void Collection_flag_keeps_every_value() =>
		Succeeds(CliHostRunner.Run("tokens", "shorts", "--tags", "a", "-t", "b", "--tags=c"), Shorts(tags: "a,b,c"));

	[Fact]
	public void Take_last_lets_the_last_value_win()
	{
		var env = Repeated("TakeLast");
		Succeeds(CliHostRunner.Run(env, "tokens", "shorts", "--name", "a", "-n", "b", "-qq"), Shorts(quiet: true, name: "b"));
		Succeeds(CliHostRunner.Run(env, "tokens", "shorts", "--color", "--no-color"), Shorts(color: "False"));
		Succeeds(CliHostRunner.Run(env, "--mode", "a", "tokens", "shorts", "--mode", "b"), Shorts(mode: "b"));
		var nsResult = CliHostRunner.Run(env, "--mode", "before", "storage", "--mode", "between", "list");
		nsResult.ExitCode.Should().Be(0, Stderr(nsResult));
		Stdout(nsResult).Should().Be("storage-list:between");
	}

	[Fact]
	public void Allow_identical_accepts_the_same_value_again()
	{
		var env = Repeated("AllowIdentical");
		Succeeds(CliHostRunner.Run(env, "tokens", "shorts", "--name", "a", "-n", "a", "-q", "--quiet=true"), Shorts(quiet: true, name: "a"));
		Succeeds(CliHostRunner.Run(env, "--mode", "a", "tokens", "shorts", "-m", "a"), Shorts(mode: "a"));
	}

	[Fact]
	public void Allow_identical_rejects_a_different_value() =>
		Fails(CliHostRunner.Run(Repeated("AllowIdentical"), "tokens", "shorts", "--name", "a", "--name", "b"),
			"Error: the argument --name cannot be used multiple times with different values: 'a' and 'b'.");

	[Fact]
	public void Flag_rule_wins_over_the_parser_options()
	{
		Succeeds(CliHostRunner.Run(NoColor, "tokens", "repeats", "--last", "a", "--last", "b", "--same", "x", "--same", "x", "-l", "--loud"),
			"last:b same:x strict: loud:True plain: mode:");
		Fails(CliHostRunner.Run(NoColor, "tokens", "repeats", "--same", "x", "--same", "y"),
			"Error: the argument --same cannot be used multiple times with different values: 'x' and 'y'.", "tokens repeats");
		Fails(CliHostRunner.Run(Repeated("TakeLast"), "tokens", "repeats", "--strict", "a", "--strict", "a"),
			"Error: the argument --strict cannot be used multiple times.", "tokens repeats");
		Succeeds(CliHostRunner.Run(Repeated("TakeLast"), "tokens", "repeats", "--plain", "a", "--plain", "b"),
			"last: same: strict: loud:False plain:b mode:");
	}

	[Fact]
	public void Flag_rule_applies_to_dto_members_and_namespace_options()
	{
		Succeeds(CliHostRunner.Run(NoColor, "tokens", "repeats-dto", "--level", "a", "--level", "b", "--tag", "t"), "level:b tag:t");
		Fails(CliHostRunner.Run(NoColor, "tokens", "repeats-dto", "--tag", "a", "--tag", "b"),
			"Error: the argument --tag cannot be used multiple times.", "tokens repeats-dto");
		Succeeds(CliHostRunner.Run(NoColor, "tokens", "ns", "--region", "a", "show", "--region", "b", "--stage", "s"), "region:b stage:s mode:");
		Fails(CliHostRunner.Run(NoColor, "tokens", "ns", "--stage", "a", "show", "--stage", "b"),
			"Error: the argument --stage cannot be used multiple times.", "tokens ns show");
	}
}
