using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.Completions;

/// <summary>
/// Users type values in the parse culture (<c>ARGH_TEST_CULTURE</c>), so completion offers numeric allowed values in that
/// culture, and help and errors show them the same way. <c>__schema</c> stays invariant. Names and values are matched
/// against the typed prefix without the culture, the same way parsing matches them.
/// </summary>
public class CultureCompletionTests
{
	private static Dictionary<string, string>? Culture(string? name) =>
		name is null ? null : new() { ["ARGH_TEST_CULTURE"] = name };

	private static string Complete(string? culture, params string[] words) => Complete(Culture(culture), words);

	private static string Complete(Dictionary<string, string>? env, params string[] words)
	{
		var result = CliHostRunner.Run(env, ["__complete", "bash", "--", .. words]);
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		return CliHostRunner.StdoutText(result);
	}

	[Theory]
	[InlineData(null, "1.5\n10\n2.5\n")]
	[InlineData("de-DE", "1,5\n10\n2,5\n")]
	[InlineData("fr-FR", "1,5\n10\n2,5\n")]
	public void Allowed_numeric_values_complete_in_the_parse_culture(string? culture, string expected) =>
		Complete(culture, "culture-choices", "--rate", "").Should().Be(expected);

	[Fact]
	public void Culture_formatted_values_filter_by_the_typed_prefix() =>
		Complete("de-DE", "culture-choices", "--rate", "1,").Should().Be("1,5\n");

	[Fact]
	public void Inline_flag_value_completes_in_the_parse_culture() =>
		Complete("de-DE", "culture-choices", "--rate=2").Should().Be("--rate=2,5\n");

	[Theory]
	[InlineData(null, "1.5")]
	[InlineData("de-DE", "2,5")]
	public void Completed_value_parses_back_to_the_same_number(string? culture, string completed)
	{
		var result = CliHostRunner.Run(Culture(culture), "culture-choices", "--rate", completed);
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		CliHostRunner.StdoutText(result).Trim().Should().StartWith(culture is null ? "1.5|" : "2.5|");
	}

	[Theory]
	[InlineData(null, "[allowed: 1.5|2.5|10]", "[denied: 0.5]", "[range: 0.5-9.5]")]
	[InlineData("de-DE", "[allowed: 1,5|2,5|10]", "[denied: 0,5]", "[range: 0,5-9,5]")]
	public void Help_shows_validation_values_in_the_parse_culture(string? culture, string allowed, string denied, string range)
	{
		var result = CliHostRunner.Run(Culture(culture), "culture-choices", "--help");
		result.ExitCode.Should().Be(0);
		var help = ConsoleOutput.Normalize(CliHostRunner.StdoutText(result));
		help.Should().Contain(allowed).And.Contain(denied).And.Contain(range);
	}

	[Theory]
	[InlineData("--rate", "3", "Error: --rate: value must be one of: 1,5, 2,5, 10.")]
	[InlineData("--step", "0,5", "Error: --step: value must not be: 0,5.")]
	[InlineData("--ratio", "10", "Error: --ratio: value must be between 0,5 and 9,5.")]
	public void Validation_errors_show_values_in_the_parse_culture(string flag, string value, string error)
	{
		var result = CliHostRunner.Run(Culture("de-DE"), "culture-choices", flag, value);
		result.ExitCode.Should().Be(2);
		ConsoleOutput.Normalize(CliHostRunner.StderrText(result)).Should().Contain(error);
	}

	[Fact]
	public void Schema_values_stay_invariant()
	{
		var result = CliHostRunner.Run(Culture("de-DE"), "__schema");
		result.ExitCode.Should().Be(0);
		var schema = CliHostRunner.StdoutText(result);
		schema.Should().Contain("\"1.5\"").And.Contain("\"9.5\"").And.NotContain("\"1,5\"");
	}

	// Turkish I: matching ignores case without the culture, so `I` still finds `information`, and `--RATI` finds `--ratio`
	// when option names ignore case.
	[Theory]
	[InlineData(new[] { "culture-choices", "--RATI" }, "--ratio\n")]
	[InlineData(new[] { "hello", "--severity", "I" }, "information\n")]
	public void Matching_ignores_case_without_the_culture(string[] words, string expected) =>
		Complete(new Dictionary<string, string> { ["ARGH_TEST_CULTURE"] = "tr-TR", ["ARGH_TEST_ARGUMENT_COMPARER"] = "OrdinalIgnoreCase" }, words)
			.Should().Be(expected);
}
