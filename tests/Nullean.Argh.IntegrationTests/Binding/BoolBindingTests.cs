using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.Binding;

/// <summary>
/// Booleans read like clap's <c>BoolishValueParser</c> on the command line and <c>FalseyValueParser</c> from environment
/// variables; the command line wins over the environment variable, which wins over the default.
/// </summary>
public class BoolBindingTests
{
	private static string Run(params string[] args) => Run(null, args);

	private static string Run(Dictionary<string, string>? env, params string[] args)
	{
		var result = CliHostRunner.Run(env, args);
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		return CliHostRunner.StdoutText(result).Trim();
	}

	private static string RunFails(params string[] args)
	{
		var result = CliHostRunner.Run(args);
		result.ExitCode.Should().Be(2);
		return ConsoleOutput.Normalize(CliHostRunner.StderrText(result));
	}

	private static Dictionary<string, string> Env(string name, string value) => new() { [name] = value };

	private const string Defaults = "force:False color:True dry-run:null from-env:False maybe:null";

	[Fact]
	public void Defaults_apply_when_nothing_is_given() =>
		Run("bool-flags").Should().Be(Defaults);

	[Theory]
	[InlineData("--force")]
	[InlineData("-f")]
	[InlineData("--force=y")]
	[InlineData("--force=Yes")]
	[InlineData("--force=T")]
	[InlineData("--force=TRUE")]
	[InlineData("--force=on")]
	[InlineData("--force=1")]
	[InlineData("-f=YES")]
	public void A_true_value_sets_a_plain_bool(string arg) =>
		Run("bool-flags", arg).Should().StartWith("force:True ");

	[Theory]
	[InlineData("--color=n")]
	[InlineData("--color=NO")]
	[InlineData("--color=f")]
	[InlineData("--color=False")]
	[InlineData("--color=OFF")]
	[InlineData("--color=0")]
	public void A_false_value_clears_a_plain_bool_that_starts_on(string arg) =>
		Run("bool-flags", arg).Should().Contain(" color:False ");

	[Theory]
	[InlineData("--force=false")]
	[InlineData("-f=false")]
	[InlineData("-f=No")]
	public void A_false_value_leaves_a_plain_bool_off(string arg) =>
		Run("bool-flags", arg).Should().StartWith("force:False ");

	[Theory]
	[InlineData("--dry-run", "True")]
	[InlineData("-d", "True")]
	[InlineData("--dry-run=on", "True")]
	[InlineData("--dry-run=No", "False")]
	[InlineData("--no-dry-run", "False")]
	public void A_nullable_bool_reads_its_value_and_negation(string arg, string expected) =>
		Run("bool-flags", arg).Should().Contain($" dry-run:{expected} ");

	[Theory]
	[InlineData("--force=bogus", "force", "bogus")]
	[InlineData("--force=", "force", "")]
	[InlineData("--force=2", "force", "2")]
	[InlineData("--force=yeah", "force", "yeah")]
	[InlineData("-f=nope", "force", "nope")]
	[InlineData("--dry-run=bogus", "dry-run", "bogus")]
	[InlineData("--dry-run=no-way", "dry-run", "no-way")]
	public void A_value_that_isnt_a_boolean_fails(string arg, string flag, string value) =>
		RunFails("bool-flags", arg).Should().StartWith($"Error: invalid bool for --{flag}: '{value}'. Expected true/false, yes/no, y/n, t/f, on/off or 1/0.");

	[Theory]
	[InlineData("--no-dry-run=true")]
	[InlineData("--no-dry-run=false")]
	[InlineData("--no-dry-run=")]
	[InlineData("--no-dry-run=maybe")]
	public void A_negated_flag_doesnt_take_a_value(string arg) =>
		RunFails("bool-flags", arg).TrimEnd().Should().Be(
			"Error: --no-dry-run doesn't take a value.\n\n" +
			"  -d, --[no-]dry-run   Nullable bool.\n\n" +
			$"Run '{CliHostPaths.CliHostAssemblyName} bool-flags --help' for usage.");

	[Theory]
	[InlineData("no-maybe", "bool-dto", "--no-maybe=yes")]
	[InlineData("no-tri", "bool-ns", "--no-tri=false", "show")]
	[InlineData("no-tri", "bool-ns", "show", "--no-tri=")]
	public void A_negated_member_or_option_doesnt_take_a_value(string flag, params string[] args) =>
		RunFails(args).Should().StartWith($"Error: --{flag} doesn't take a value.");

	[Theory]
	[InlineData("false", "False")]
	[InlineData("FALSE", "False")]
	[InlineData("False", "False")]
	[InlineData("no", "False")]
	[InlineData("Off", "False")]
	[InlineData("n", "False")]
	[InlineData("F", "False")]
	[InlineData("0", "False")]
	[InlineData("true", "True")]
	[InlineData("YES", "True")]
	[InlineData("1", "True")]
	[InlineData("2", "True")]
	[InlineData("anything", "True")]
	public void An_environment_variable_is_false_only_for_a_false_spelling(string value, string expected)
	{
		Run(Env("ARGH_TEST_BOOL", value), "bool-flags").Should().Contain($" from-env:{expected} ");
		Run(Env("ARGH_TEST_NBOOL", value), "bool-flags").Should().EndWith($" maybe:{expected}");
	}

	[Theory]
	[InlineData("ARGH_TEST_BOOL", "true", "--from-env=false", " from-env:False ")]
	[InlineData("ARGH_TEST_BOOL", "false", "--from-env", " from-env:True ")]
	[InlineData("ARGH_TEST_NBOOL", "true", "--no-maybe", " maybe:False")]
	[InlineData("ARGH_TEST_NBOOL", "no", "--maybe", " maybe:True")]
	public void The_command_line_wins_over_the_environment_variable(string name, string value, string arg, string expected) =>
		Run(Env(name, value), "bool-flags", arg).Should().Contain(expected);

	[Theory]
	[InlineData("first:False second:null")]
	[InlineData("first:True second:null", "yes")]
	[InlineData("first:False second:False", "Off", "0")]
	[InlineData("first:True second:True", "T", "on")]
	public void Bool_arguments_read_the_same_spellings(string expected, params string[] args) =>
		Run(["bool-args", .. args]).Should().Be(expected);

	[Fact]
	public void A_bool_argument_that_isnt_a_boolean_fails() =>
		RunFails("bool-args", "bogus").Should().StartWith("Error: invalid bool for <first>: 'bogus'. Expected true/false, yes/no, y/n, t/f, on/off or 1/0.");

	[Fact]
	public void A_bool_argument_falls_back_to_its_environment_variable()
	{
		Run(Env("ARGH_TEST_BOOL_ARG", "whatever"), "bool-args", "1").Should().Be("first:True second:True");
		Run(Env("ARGH_TEST_BOOL_ARG", "OFF"), "bool-args", "1").Should().Be("first:True second:False");
		Run(Env("ARGH_TEST_BOOL_ARG", "true"), "bool-args", "1", "no").Should().Be("first:True second:False");
	}

	[Fact]
	public void A_bool_collection_reads_each_element() =>
		Run("bool-list", "--bits", "ON", "--bits", "f", "--bits", "1").Should().Be("bits:True,False,True");

	[Fact]
	public void A_bool_collection_element_that_isnt_a_boolean_fails() =>
		RunFails("bool-list", "--bits", "true", "--bits", "maybe").Should().StartWith("Error: invalid bool for --bits: 'maybe'. Expected true/false, yes/no, y/n, t/f, on/off or 1/0.");

	[Theory]
	[InlineData("fast:False maybe:null lit:True")]
	[InlineData("fast:True maybe:True lit:True", "--fast", "--maybe")]
	[InlineData("fast:False maybe:False lit:False", "--fast=false", "--maybe=no", "--lit=0")]
	[InlineData("fast:False maybe:False lit:True", "--no-maybe")]
	public void AsParameters_bool_members_read_their_values(string expected, params string[] args) =>
		Run(["bool-dto", .. args]).Should().Be(expected);

	[Fact]
	public void AsParameters_bool_member_reads_its_environment_variable_after_the_command_line()
	{
		Run(Env("ARGH_TEST_DTO_LIT", "false"), "bool-dto").Should().EndWith(" lit:False");
		Run(Env("ARGH_TEST_DTO_LIT", "false"), "bool-dto", "--lit").Should().EndWith(" lit:True");
	}

	[Fact]
	public void AsParameters_bool_member_that_isnt_a_boolean_fails() =>
		RunFails("bool-dto", "--fast=bogus").Should().StartWith("Error: invalid bool for --fast: 'bogus'. Expected true/false, yes/no, y/n, t/f, on/off or 1/0.");

	[Theory]
	[InlineData("verbose:False progress:True", "bool-global")]
	[InlineData("verbose:True progress:True", "--verbose", "bool-global")]
	[InlineData("verbose:False progress:True", "--verbose=false", "bool-global")]
	[InlineData("verbose:False progress:True", "bool-global", "-v=no")]
	[InlineData("verbose:False progress:False", "--progress=off", "bool-global")]
	[InlineData("verbose:False progress:False", "bool-global", "--progress=0")]
	public void A_bool_global_option_reads_its_value_before_or_after_the_command(string expected, params string[] args) =>
		Run(args).Should().Be(expected);

	[Theory]
	[InlineData("0", "progress:False", "bool-global")]
	[InlineData("0", "progress:True", "--progress", "bool-global")]
	[InlineData("0", "progress:True", "bool-global", "--progress")]
	[InlineData("yes", "progress:False", "--progress=no", "bool-global")]
	[InlineData("yes", "progress:False", "bool-global", "--progress=no")]
	public void A_bool_global_option_given_anywhere_wins_over_its_environment_variable(string env, string expected, params string[] args) =>
		Run(Env("ARGH_TEST_PROGRESS", env), args).Should().EndWith(expected);

	[Fact]
	public void A_bool_global_option_that_isnt_a_boolean_fails_before_the_command() =>
		RunFails("--progress=bogus", "bool-global").Should().StartWith("Error: invalid bool for --progress: 'bogus'. Expected true/false, yes/no, y/n, t/f, on/off or 1/0.");

	[Theory]
	[InlineData("0", "progress:True", "--progress", "bool-ns", "show")]
	[InlineData("0", "progress:True", "bool-ns", "--progress", "show")]
	[InlineData("0", "progress:True", "bool-ns", "show", "--progress")]
	[InlineData("0", "progress:False", "bool-ns", "show")]
	public void A_global_bool_in_a_namespace_wins_over_its_environment_variable_in_every_position(string env, string expected, params string[] args) =>
		Run(Env("ARGH_TEST_PROGRESS", env), args).Should().StartWith(expected + " ");

	[Theory]
	[InlineData("1", "loud:True", "bool-ns", "show")]
	[InlineData("1", "loud:False", "bool-ns", "--loud=false", "show")]
	[InlineData("1", "loud:False", "bool-ns", "show", "--loud=false")]
	[InlineData("no", "loud:True", "bool-ns", "--loud", "show")]
	[InlineData("no", "loud:True", "bool-ns", "show", "--loud")]
	public void A_namespace_bool_wins_over_its_environment_variable_before_or_after_the_command(string env, string expected, params string[] args) =>
		Run(Env("ARGH_TEST_NS_LOUD", env), args).Should().Contain($" {expected} ");

	[Theory]
	[InlineData("yes", "tri:True", "bool-ns", "show")]
	[InlineData("yes", "tri:False", "bool-ns", "--no-tri", "show")]
	[InlineData("yes", "tri:False", "bool-ns", "show", "--no-tri")]
	[InlineData("off", "tri:True", "bool-ns", "--tri", "show")]
	[InlineData("off", "tri:False", "bool-ns", "show")]
	public void A_namespace_nullable_bool_wins_over_its_environment_variable_before_or_after_the_command(string env, string expected, params string[] args) =>
		Run(Env("ARGH_TEST_NS_TRI", env), args).Should().EndWith($" {expected}");
}
