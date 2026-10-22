using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.Binding;

/// <summary>
/// Each element of a collection is parsed exactly as a single value of the element type: the values given are split on
/// the separator with empty parts kept, and the element type's parser (built-in or <c>[ArgumentParser]</c>) accepts or
/// rejects each one. <c>collections one</c> takes single <c>T?</c> values, <c>many</c> collections of <c>T</c> and
/// <c>many-maybe</c> collections of <c>T?</c>, all with the same flags.
/// </summary>
public class CollectionElementTests
{
	private static readonly Dictionary<string, string> NoColor = new(StringComparer.Ordinal) { ["NO_COLOR"] = "1" };

	private static ProcNet.ProcessCaptureResult Run(params string[] args) => CliHostRunner.Run(NoColor, args);

	private static ProcNet.ProcessCaptureResult RunWithEnv(Dictionary<string, string> environment, params string[] args) =>
		CliHostRunner.Run(new Dictionary<string, string>(environment) { ["NO_COLOR"] = "1" }, args);

	private static string Stdout(ProcNet.ProcessCaptureResult result) => ConsoleOutput.Normalize(CliHostRunner.StdoutText(result)).Trim();

	private static string FirstError(ProcNet.ProcessCaptureResult result) =>
		ConsoleOutput.Normalize(CliHostRunner.StderrText(result)).Split('\n')[0];

	private static string Succeeds(ProcNet.ProcessCaptureResult result)
	{
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		return Stdout(result);
	}

	private static string Fails(ProcNet.ProcessCaptureResult result)
	{
		result.ExitCode.Should().Be(2, CliHostRunner.StdoutText(result));
		return FirstError(result);
	}

	[Theory]
	[InlineData("--n=5", "n=5")]
	[InlineData("--n=", null)]
	[InlineData("--n=x", null)]
	[InlineData("--n= 7 ", "n=7")]
	[InlineData("--amount=1.5", "amount=1.5")]
	[InlineData("--amount=", null)]
	[InlineData("--amount=1,5", null)]
	[InlineData("--bit=yes", "bit=True")]
	[InlineData("--bit=", null)]
	[InlineData("--bit=maybe", null)]
	[InlineData("--color=blue", "color=Blue")]
	[InlineData("--color=", null)]
	[InlineData("--color=green", null)]
	[InlineData("--id=3f2504e0-4f89-11d3-9a0c-0305e82c3301", "id=3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
	[InlineData("--id=", null)]
	[InlineData("--id=nope", null)]
	[InlineData("--day=2024-01-02", "day=01/02/2024")]
	[InlineData("--day=", null)]
	[InlineData("--wait=5m", "wait=00:05:00")]
	[InlineData("--wait=1h30m", "wait=01:30:00")]
	[InlineData("--wait=10", null)]
	[InlineData("--wait=", null)]
	[InlineData("--wait=x", null)]
	[InlineData("--link=https://a.example/", "link=https://a.example/")]
	[InlineData("--link=", null)]
	[InlineData("--link=rel/x", null)]
	[InlineData("--file=a.txt", "file=a.txt")]
	[InlineData("--file=", null)]
	[InlineData("--at=1,2", "at=Point { X = 1, Y = 2 }")]
	[InlineData("--at=", null)]
	[InlineData("--at=1", null)]
	[InlineData("--name=x", "name=x")]
	[InlineData("--name=", "name=")]
	public void An_element_parses_exactly_as_a_single_value(string arg, string? expected)
	{
		var single = Run("collections", "one", arg);
		foreach (var command in new[] { "many", "many-maybe" })
		{
			var element = Run("collections", command, arg);
			element.ExitCode.Should().Be(single.ExitCode, $"{command} {arg}: {CliHostRunner.StderrText(element)}");
			if (expected is null)
				Fails(element).Should().Be(Fails(single), $"{command} {arg}");
			else
			{
				Succeeds(single).Should().Be(expected);
				Succeeds(element).Should().Be(expected, $"{command} {arg}");
			}
		}
	}

	[Theory]
	[InlineData("many")]
	[InlineData("many-maybe")]
	public void A_custom_parser_on_a_collection_parses_each_element(string command) =>
		Succeeds(Run("collections", command, "--at", "1,2", "--at", "3,4")).Should().Be("at=Point { X = 1, Y = 2 };Point { X = 3, Y = 4 }");

	[Fact]
	public void A_parser_for_the_whole_collection_still_parses_the_whole_value() =>
		Succeeds(Run("tag-set-parser", "--tags", "c,a,b")).Should().EndWith("tag-set-parser:a,b,c");

	[Theory]
	[InlineData("--ids=")]
	[InlineData("--ids=,,")]
	[InlineData("--ids=1,,3")]
	[InlineData("--ids=1,")]
	public void An_empty_element_of_a_required_int_list_is_an_error(string arg) =>
		Fails(Run("schema-separator-list", arg)).Should().Be("Error: invalid int for --ids: ''.");

	[Fact]
	public void A_required_separator_collection_must_be_given() =>
		Fails(Run("schema-separator-list")).Should().Be("Error: missing required flag --ids.");

	[Fact]
	public void Empty_string_elements_are_kept() =>
		Succeeds(Run("collections", "separated", "--names", ";a;;b;", "--tags", "", "--tags", "c"))
			.Should().Be("names=;a;;b; tags=;c");

	[Fact]
	public void A_separated_flag_given_again_adds_its_elements() =>
		Succeeds(Run("collections", "separated", "--ids", "1,2", "--ids=3")).Should().Be("ids=1;2;3");

	[Fact]
	public void Separator_collections_on_an_AsParameters_dto_keep_empty_elements_for_the_parser()
	{
		Succeeds(Run("as-params-collection-syntax", "--cs-tag-ids", "1", "--cs-labels", "a||b", "--cs-ports", "80"))
			.Should().EndWith("as-params-collection-syntax:1:a,,b:80");
		Fails(Run("as-params-collection-syntax", "--cs-tag-ids", "1,,2", "--cs-labels", "a", "--cs-ports", "80"))
			.Should().Be("Error: invalid int for --cs-tag-ids: ''.");
		Fails(Run("parsable-dto", "--order", "ORD-5", "--id", "3f2504e0-4f89-11d3-9a0c-0305e82c3301", "--batch", "ORD-1,"))
			.Should().Be("Error: invalid OrderId for --batch: ''.");
	}

	[Fact]
	public void An_empty_variadic_element_is_parsed_like_any_other()
	{
		Succeeds(Run("collections", "variadic", "1", "2")).Should().Be("values=1;2");
		Fails(Run("collections", "variadic", "1", "")).Should().Be("Error: invalid int for <values>: ''.");
	}

	[Fact]
	public void A_collection_reads_its_environment_variable_split_on_its_separator()
	{
		Succeeds(RunWithEnv(new() { ["ARGH_TEST_IDS"] = "4,5", ["ARGH_TEST_TAGS"] = "x,y" }, "collections", "separated"))
			.Should().Be("ids=4;5 tags=x,y");
		Succeeds(RunWithEnv(new() { ["ARGH_TEST_IDS"] = "4,5" }, "collections", "separated", "--ids", "6")).Should().Be("ids=6");
		Succeeds(RunWithEnv(new() { ["ARGH_TEST_IDS"] = "" }, "collections", "separated")).Should().BeEmpty();
	}

	[Fact]
	public void An_invalid_element_from_the_environment_names_the_variable()
	{
		var result = RunWithEnv(new() { ["ARGH_TEST_IDS"] = "4,,5" }, "collections", "separated");
		Fails(result).Should().Be("Error: invalid int for --ids: ''.");
		CliHostRunner.StderrText(result).Should().Contain("The value comes from the environment variable ARGH_TEST_IDS.");
	}

	[Fact]
	public void Help_shows_the_environment_variable_of_a_collection() =>
		Succeeds(Run("collections", "separated", "--help")).Should().Contain("[separated] Comma-separated ints, or ARGH_TEST_IDS. [env: ARGH_TEST_IDS]");

	[Theory]
	[InlineData("--small=11", "Error: --small: value must be between 1 and 10.")]
	[InlineData("--small=0", "Error: --small: value must be between 1 and 10.")]
	[InlineData("--letter=c", "Error: --letter: value must be one of: a, b.")]
	[InlineData("--site=mailto:x@example.com", "Error: --site: value is not a valid URL.")]
	[InlineData("--secure=http://x", "Error: --secure: URI scheme must be one of: https.")]
	[InlineData("--code=ABC", "Error: --code: value does not match required pattern ^[a-z]{2}$.")]
	public void A_value_constraint_on_a_collection_checks_each_element(string invalid, string error) =>
		Fails(Run("collections", "validated", invalid.Split('=')[0] + "=" + Valid(invalid), invalid)).Should().Be(error);

	private static string Valid(string arg) => arg.Split('=')[0] switch
	{
		"--small" => "5",
		"--letter" => "a",
		"--site" => "https://ok",
		"--secure" => "https://ok",
		_ => "ab",
	};

	[Fact]
	public void A_count_constraint_checks_the_whole_collection()
	{
		Fails(Run("collections", "validated", "--small", "1", "--small", "2", "--small", "3", "--small", "4"))
			.Should().Be("Error: --small: must have at most 3 items.");
		Succeeds(Run("collections", "validated", "--small", "1", "--small", "10", "--letter", "b", "--site", "http://x", "--secure", "https://y",
			"--code", "zz")).Should().Be("small=1;10 letter=b site=http://x/ secure=https://y/ code=zz");
	}
}
