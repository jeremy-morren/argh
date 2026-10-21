using System.Text.Json;
using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.ParseErrors;

/// <summary>
/// A <c>[Range]</c> bound at the parameter type's own <c>MinValue</c>/<c>MaxValue</c> means "no limit": help, errors and
/// <c>__schema</c> show only the bound that is left.
/// </summary>
public class UnboundedRangeTests
{
	private static Dictionary<string, string> Env(string? culture = null)
	{
		var env = new Dictionary<string, string>(StringComparer.Ordinal) { ["NO_COLOR"] = "1" };
		if (culture is not null)
			env["ARGH_TEST_CULTURE"] = culture;
		return env;
	}

	[Theory]
	[InlineData("--at-least", "[min: 4]")]
	[InlineData("--at-most", "[max: 10]")]
	[InlineData("--ratio", "[range: 0-10]")]
	[InlineData("--amount", "[range: 0.1-0.3]")]
	[InlineData("--price", "[min: 0]")]
	[InlineData("--fee", "[range: 1.5-2.25]")]
	[InlineData("--count", "[min: 1]")]
	[InlineData("--level", "[min: 1]")]
	[InlineData("--half", "[min: -1]")]
	[InlineData("--low", "[max: 100]")]
	[InlineData("--tiny", "[min: -10]")]
	[InlineData("--port", "[range: 10-20]")]
	[InlineData("--huge", "[min: 0]")]
	[InlineData("--whole", "[range: -5-5]")]
	[InlineData("--cap", "[min: 0]")]
	public void Help_shows_only_the_bounds_that_limit(string flag, string hint)
	{
		var result = CliHostRunner.Run(Env(), "numeric-ranges", "--help");
		result.ExitCode.Should().Be(0);
		HelpLine(result, flag).Should().EndWith(hint);
	}

	[Theory]
	[InlineData("--any")]
	[InlineData("--open")]
	public void Help_shows_no_hint_when_both_bounds_are_unbounded(string flag)
	{
		var result = CliHostRunner.Run(Env(), "numeric-ranges", "--help");
		HelpLine(result, flag).Should().NotContain("[range").And.NotContain("[min").And.NotContain("[max");
	}

	[Fact]
	public void Help_formats_bounds_in_the_parse_culture()
	{
		var result = CliHostRunner.Run(Env("de-DE"), "numeric-ranges", "--help");
		HelpLine(result, "--amount").Should().EndWith("[range: 0,1-0,3]");
		HelpLine(result, "--fee").Should().EndWith("[range: 1,5-2,25]");
	}

	[Theory]
	[InlineData("--at-least", "3", "Error: --at-least: value must be at least 4.")]
	[InlineData("--at-most", "11", "Error: --at-most: value must be at most 10.")]
	[InlineData("--ratio", "10.5", "Error: --ratio: value must be between 0 and 10.")]
	[InlineData("--amount", "0.30000000000000004", "Error: --amount: value must be between 0.1 and 0.3.")]
	[InlineData("--price", "-1", "Error: --price: value must be at least 0.")]
	[InlineData("--fee", "2.26", "Error: --fee: value must be between 1.5 and 2.25.")]
	[InlineData("--count", "0", "Error: --count: value must be at least 1.")]
	[InlineData("--level", "0", "Error: --level: value must be at least 1.")]
	[InlineData("--half", "-2", "Error: --half: value must be at least -1.")]
	[InlineData("--budget", "0", "Error: --budget: value must be at least 1.")]
	[InlineData("--low", "101", "Error: --low: value must be at most 100.")]
	[InlineData("--tiny", "-11", "Error: --tiny: value must be at least -10.")]
	[InlineData("--port", "21", "Error: --port: value must be between 10 and 20.")]
	[InlineData("--huge", "-1", "Error: --huge: value must be at least 0.")]
	[InlineData("--whole", "6", "Error: --whole: value must be between -5 and 5.")]
	[InlineData("--cap", "-0.0001", "Error: --cap: value must be at least 0.")]
	public void Out_of_range_value_names_the_bound_that_is_left(string flag, string value, string error)
	{
		var command = flag == "--budget" ? "numeric-defaults" : "numeric-ranges";
		var result = CliHostRunner.Run(Env(), command, flag, value);
		result.ExitCode.Should().Be(2);
		ConsoleOutput.Normalize(CliHostRunner.StderrText(result)).Should().Contain(error);
	}

	[Theory]
	[InlineData("--at-least", "1e30")]
	[InlineData("--at-most", "-9000000000000000000")]
	[InlineData("--amount", "0.3")]
	[InlineData("--amount", "0.1")]
	[InlineData("--fee", "2.25")]
	[InlineData("--any", "-1e308")]
	[InlineData("--count", "4294967295")]
	[InlineData("--level", "255")]
	[InlineData("--half", "65504")]
	[InlineData("--low", "-32768")]
	[InlineData("--tiny", "127")]
	[InlineData("--huge", "170141183460469231731687303715884105727")]
	[InlineData("--whole", "-5")]
	[InlineData("--cap", "79228162514264337593543950335")]
	[InlineData("--open", "-2147483648")]
	public void Value_at_or_past_an_unbounded_limit_is_accepted(string flag, string value)
	{
		var result = CliHostRunner.Run(Env(), "numeric-ranges", flag, value);
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
	}

	[Theory]
	[InlineData("--at-least", "3", "Error: --at-least: value must be at least 4.")]
	[InlineData("--amount", "0,05", "Error: --amount: value must be between 0,1 and 0,3.")]
	[InlineData("--fee", "1", "Error: --fee: value must be between 1,5 and 2,25.")]
	[InlineData("--price", "-0,5", "Error: --price: value must be at least 0.")]
	public void Errors_format_bounds_in_the_parse_culture(string flag, string value, string error)
	{
		var result = CliHostRunner.Run(Env("de-DE"), "numeric-ranges", flag, value);
		result.ExitCode.Should().Be(2);
		ConsoleOutput.Normalize(CliHostRunner.StderrText(result)).Should().Contain(error);
	}

	[Fact]
	public void Schema_range_carries_only_the_bounds_that_limit()
	{
		var result = CliHostRunner.Run(Env("de-DE"), "__schema");
		result.ExitCode.Should().Be(0);
		using var doc = JsonDocument.Parse(CliHostRunner.StdoutText(result));
		var parameters = FindCommand(doc.RootElement, "numeric-ranges")!.Value.GetProperty("parameters");

		RangeOf(parameters, "at-least").Should().Be(("4", null));
		RangeOf(parameters, "at-most").Should().Be((null, "10"));
		RangeOf(parameters, "amount").Should().Be(("0.1", "0.3"));
		RangeOf(parameters, "fee").Should().Be(("1.5", "2.25"));
		RangeOf(parameters, "price").Should().Be(("0", null));
		RangeOf(parameters, "low").Should().Be((null, "100"));
		RangeOf(parameters, "whole").Should().Be(("-5", "5"));
		RangeOf(parameters, "cap").Should().Be(("0", null));
		Parameter(parameters, "any").TryGetProperty("validations", out _).Should().BeFalse();
		Parameter(parameters, "open").TryGetProperty("validations", out _).Should().BeFalse();
	}

	// cli-schema v1's `constraint` definition: `kind` is required, `min` and `max` are optional strings, nothing else is allowed.
	[Fact]
	public void Every_range_constraint_matches_the_cli_schema_constraint_definition()
	{
		var result = CliHostRunner.Run(Env(), "__schema");
		using var doc = JsonDocument.Parse(CliHostRunner.StdoutText(result));
		var ranges = new List<JsonElement>();
		CollectRanges(doc.RootElement, ranges);

		ranges.Should().HaveCountGreaterThan(10);
		foreach (var range in ranges)
		{
			range.EnumerateObject().Select(p => p.Name).Should().BeSubsetOf(["kind", "min", "max"]);
			range.EnumerateObject().Where(p => p.Name != "kind").Should().NotBeEmpty()
				.And.OnlyContain(p => p.Value.ValueKind == JsonValueKind.String);
		}
	}

	private static void CollectRanges(JsonElement node, List<JsonElement> ranges)
	{
		if (node.ValueKind == JsonValueKind.Array)
		{
			foreach (var item in node.EnumerateArray())
				CollectRanges(item, ranges);
			return;
		}
		if (node.ValueKind != JsonValueKind.Object)
			return;
		if (node.TryGetProperty("kind", out var kind) && kind.GetString() == "range")
			ranges.Add(node);
		foreach (var property in node.EnumerateObject())
			CollectRanges(property.Value, ranges);
	}

	private static string HelpLine(ProcNet.ProcessCaptureResult result, string flag) =>
		ConsoleOutput.Normalize(CliHostRunner.StdoutText(result)).Split('\n')
			.Single(l => l.TrimStart().StartsWith(flag + " ", StringComparison.Ordinal)).TrimEnd();

	private static JsonElement? FindCommand(JsonElement node, string name)
	{
		if (node.TryGetProperty("name", out var n) && n.GetString() == name)
			return node;
		if (node.TryGetProperty("commands", out var commands))
			foreach (var c in commands.EnumerateArray())
				if (FindCommand(c, name) is { } found)
					return found;
		return null;
	}

	private static JsonElement Parameter(JsonElement parameters, string name) =>
		parameters.EnumerateArray().Single(p => p.GetProperty("name").GetString() == name);

	private static (string? Min, string? Max) RangeOf(JsonElement parameters, string name)
	{
		var range = Parameter(parameters, name).GetProperty("validations").EnumerateArray()
			.Single(v => v.GetProperty("kind").GetString() == "range");
		return (range.TryGetProperty("min", out var min) ? min.GetString() : null,
			range.TryGetProperty("max", out var max) ? max.GetString() : null);
	}
}
